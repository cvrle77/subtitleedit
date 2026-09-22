using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Main;

// The Silero VAD speech map of the loaded video: one pass at load, cached next to the waveform
// peaks, so a split's leading silence can be trimmed to the next voice (see TrimSplitRightHalfSilence)
// with a plain lookup. The model is installed from Settings; without it nothing here runs and the
// trim falls back to the amplitude method.
public partial class MainViewModel
{
    private readonly List<(double Start, double End)> _speechSegments = new();
    private readonly Lock _speechSegmentsLock = new();
    private string? _speechSegmentsVideo;
    private string? _speechSegmentsAttempted;
    private int _speechSegmentsSequence;
    private int _speechSegmentsBuilding;

    /// <summary>
    /// Starts the speech map for the loaded video when it is missing and the option is on - so
    /// enabling the option (or installing the model) after the video was already loaded still
    /// builds it, and the next split uses the voice detection instead of the amplitude fallback.
    /// </summary>
    private void EnsureSpeechSegmentsBuilding()
    {
        if (string.IsNullOrEmpty(_videoFileName) ||
            !Se.Settings.General.TrimSilenceAfterSplit ||
            !Se.Settings.General.SplitTrimUseVoiceDetection ||
            !SileroVadModel.IsInstalled())
        {
            return;
        }

        if (_speechSegmentsVideo == _videoFileName || _speechSegmentsAttempted == _videoFileName)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _speechSegmentsBuilding, 1, 0) != 0)
        {
            return; // a pass is already running
        }

        var videoFileName = _videoFileName;
        var trackNumber = _audioTrack?.FfIndex ?? -1;
        var peakWaveFileName = WavePeakGenerator2.GetPeakWaveFileName(videoFileName, trackNumber);
        _ = BuildSpeechSegmentsAsync(null, videoFileName, trackNumber, peakWaveFileName)
            .ContinueWith(_ => Interlocked.Exchange(ref _speechSegmentsBuilding, 0));
    }

    private static string GetSpeechCacheFileName(string peakWaveFileName)
    {
        return peakWaveFileName + "_speech.json";
    }

    /// <summary>
    /// Makes sure the speech segments for the video are available: from the cache when it exists,
    /// otherwise from one VAD pass. <paramref name="waveFileToReuse"/> is the waveform extraction's
    /// temp wav when this runs right after an extraction (so no second ffmpeg pass is needed).
    /// </summary>
    private async Task BuildSpeechSegmentsAsync(string? waveFileToReuse, string videoFileName, int trackNumber, string peakWaveFileName)
    {
        if (!Se.Settings.General.TrimSilenceAfterSplit || !Se.Settings.General.SplitTrimUseVoiceDetection)
        {
            return;
        }

        var modelPath = SileroVadModel.GetModelPath();
        if (!File.Exists(modelPath))
        {
            return;
        }

        var cacheFile = GetSpeechCacheFileName(peakWaveFileName);
        if (File.Exists(cacheFile))
        {
            LoadSpeechSegmentsFromCache(cacheFile, videoFileName);
            return;
        }

        var sequence = Interlocked.Increment(ref _speechSegmentsSequence);
        string? tempWaveFileName = null;
        try
        {
            var waveFileName = waveFileToReuse;
            if (waveFileName == null || !File.Exists(waveFileName))
            {
                tempWaveFileName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");
                if (!await ExtractMono16kWaveAsync(videoFileName, trackNumber, tempWaveFileName))
                {
                    return;
                }

                waveFileName = tempWaveFileName;
            }

            var segments = await Task.Run(() =>
            {
                using var vad = new SileroVad(modelPath);
                return vad.DetectSpeech(waveFileName);
            });

            // A newer video superseded this run - do not publish its result.
            if (sequence != Volatile.Read(ref _speechSegmentsSequence))
            {
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(segments.Select(s => new[] { s.Start, s.End }).ToList());
                File.WriteAllText(cacheFile, json);
            }
            catch
            {
                // a missing cache just means the pass runs again next time
            }

            lock (_speechSegmentsLock)
            {
                _speechSegments.Clear();
                _speechSegments.AddRange(segments);
                _speechSegmentsVideo = videoFileName;
            }
        }
        catch (Exception exception)
        {
            Se.LogError(exception, "Silero VAD pass failed");
        }
        finally
        {
            _speechSegmentsAttempted = videoFileName;
            if (tempWaveFileName != null)
            {
                DeleteTempFile(tempWaveFileName);
            }
        }
    }

    private void LoadSpeechSegmentsFromCache(string cacheFile, string videoFileName)
    {
        try
        {
            var json = File.ReadAllText(cacheFile);
            var pairs = JsonSerializer.Deserialize<List<double[]>>(json);
            if (pairs == null)
            {
                return;
            }

            var segments = new List<(double, double)>();
            foreach (var pair in pairs)
            {
                if (pair.Length == 2)
                {
                    segments.Add((pair[0], pair[1]));
                }
            }

            lock (_speechSegmentsLock)
            {
                _speechSegments.Clear();
                _speechSegments.AddRange(segments);
                _speechSegmentsVideo = videoFileName;
            }
        }
        catch (Exception exception)
        {
            Se.LogError(exception, $"Discarding unreadable speech cache: {cacheFile}");
        }
    }

    private static async Task<bool> ExtractMono16kWaveAsync(string videoFileName, int trackNumber, string outputWaveFileName)
    {
        var ffmpegPath = Se.Settings.General.FfmpegPath;
        if (!File.Exists(ffmpegPath))
        {
            return false;
        }

        var map = trackNumber >= 0 ? $"-map 0:{trackNumber}? " : string.Empty;
        var arguments = $"-nostdin -y -i \"{videoFileName}\" {map}-vn -ac 1 -ar 16000 -f wav \"{outputWaveFileName}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode == 0 && File.Exists(outputWaveFileName);
    }

    /// <summary>
    /// The start of the first speech segment after <paramref name="positionSeconds"/>, or null when
    /// the video has no speech map (model not installed, pass not finished, or no speech follows).
    /// </summary>
    private double? FindSpeechStartAfter(double positionSeconds)
    {
        if (_speechSegmentsVideo != _videoFileName)
        {
            return null;
        }

        lock (_speechSegmentsLock)
        {
            foreach (var segment in _speechSegments)
            {
                if (segment.Start > positionSeconds)
                {
                    return segment.Start;
                }
            }
        }

        return null;
    }
}
