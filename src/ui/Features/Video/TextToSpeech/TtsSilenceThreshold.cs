using Nikse.SubtitleEdit.Logic.Media;
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// Silence thresholds for the TTS audio pipeline, derived from the clip's own peak level.
/// </summary>
/// <remarks>
/// The silence trim, the VAD pause compression and the pro-chain noise gate all used a fixed
/// -40 dBFS threshold. Voice cloning engines reproduce the loudness of their reference, and a
/// reference cut from film dialogue often peaks at -20 to -30 dBFS - so the soft final consonant
/// of a quiet clone fell under the fixed threshold and was trimmed off as "silence", cutting the
/// last word of the line (#14480). Measured on a real clip: at a -21 dBFS peak the final "s" was
/// gone, at -33 dBFS the whole line was considered silence. Making the threshold relative to the
/// peak (40 dB below it) gives every clip the same trim a full-scale one gets.
/// <para>
/// Why 40 dB and not less: a word-final "s" peaks 20-25 dB under the clip's loudest vowel, and
/// "f"/"th" sit another ~10 dB lower, so a narrower window starts eating endings again. The
/// price is paid on clips whose noise floor is within 40 dB of the peak (some VibeVoice/Qwen3
/// output): up to a few hundred ms of hiss ~30 dB under the speech can stay at the ends. Over
/// ~250 real clips from eight engines the median difference to the old trim was 0 ms at both
/// ends, which is the point - loud clips are trimmed exactly as before.
/// </para>
/// </remarks>
public static partial class TtsSilenceThreshold
{
    /// <summary>How far below the clip's peak a sample still counts as speech.</summary>
    public const double BelowPeakDb = 40.0;

    /// <summary>
    /// Never listen below this: neural vocoders leave a noise floor around -60 dBFS, and a
    /// threshold under it would stop trimming trailing silence on very quiet clips.
    /// </summary>
    public const double FloorDbfs = -70.0;

    /// <summary>The threshold every stage used before the peak was measured: 0.01 = -40 dBFS.</summary>
    public const double LegacyThresholdDbfs = -40.0;

    /// <summary>
    /// Threshold in dBFS for a clip with the given peak. A null peak (ffmpeg could not measure
    /// the file) falls back to the legacy fixed threshold.
    /// </summary>
    public static double ThresholdDbfs(double? peakDbfs)
    {
        if (peakDbfs == null || double.IsNaN(peakDbfs.Value) || double.IsInfinity(peakDbfs.Value))
        {
            return LegacyThresholdDbfs;
        }

        // Float WAVs can peak above 0 dBFS; a threshold above the legacy one would trim more
        // aggressively than before, never less, so cap the peak at full scale.
        var peak = Math.Min(0.0, peakDbfs.Value);
        return Math.Max(FloorDbfs, peak - BelowPeakDb);
    }

    /// <summary>Threshold as a linear amplitude (0..1), the form silenceremove/agate take.</summary>
    public static double Amplitude(double? peakDbfs)
    {
        return Math.Pow(10.0, ThresholdDbfs(peakDbfs) / 20.0);
    }

    /// <summary>Threshold as an ffmpeg dB literal, e.g. "-52.3dB".</summary>
    public static string DbLiteral(double? peakDbfs)
    {
        return ThresholdDbfs(peakDbfs).ToString("0.0", CultureInfo.InvariantCulture) + "dB";
    }

    /// <summary>
    /// Peak level in dBFS of a 16-bit PCM WAV, read straight from its samples - no ffmpeg process.
    /// Returns null for anything else (float/non-WAV), so the caller falls back to ffmpeg. Used by
    /// the adjust-speed step, where one ffmpeg spawn per segment for this probe was a large share of
    /// the runtime. The dBFS value matches volumedetect's convention: 20*log10(peak/32768).
    /// </summary>
    public static double? MeasurePeakDbfsFromWav(string fileName)
    {
        try
        {
            if (!File.Exists(fileName))
            {
                return null;
            }

            using var stream = File.OpenRead(fileName);
            var header = new WaveHeader2(stream);
            if (header.ChunkId != "RIFF" || header.Format != "WAVE" ||
                header.AudioFormat != WaveHeader2.AudioFormatPcm || header.BitsPerSample != 16 ||
                header.NumberOfChannels <= 0 || header.LengthInSamples <= 0)
            {
                return null;
            }

            stream.Position = header.DataStartPosition;
            const int bufferSamples = 65536;
            var buffer = new byte[bufferSamples * 2];
            var remaining = (long)header.LengthInSamples * header.BlockAlign;
            var maxAbs = 0;
            while (remaining > 0)
            {
                var want = (int)Math.Min(remaining, buffer.Length);
                var read = stream.Read(buffer, 0, want);
                if (read <= 0)
                {
                    break;
                }

                remaining -= read;
                for (var i = 0; i + 1 < read; i += 2)
                {
                    var v = BitConverter.ToInt16(buffer, i);
                    var abs = v >= 0 ? v : -v; // -short.MinValue == 32768, fits an int
                    if (abs > maxAbs)
                    {
                        maxAbs = abs;
                    }
                }
            }

            if (maxAbs <= 0)
            {
                return double.NegativeInfinity; // digital silence
            }

            return 20.0 * Math.Log10(maxAbs / 32768.0);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Peak level of an audio file in dBFS: read from a 16-bit PCM WAV's samples directly (no
    /// process), else via ffmpeg's volumedetect. Null when neither works (callers then fall back to
    /// the legacy threshold).
    /// </summary>
    public static async Task<double?> MeasurePeakDbfsAsync(string fileName, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        var fromWav = MeasurePeakDbfsFromWav(fileName);
        if (fromWav != null)
        {
            return fromWav;
        }

        var lines = new List<string>();
        var gate = new object();

        void OnLine(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            lock (gate)
            {
                lines.Add(e.Data);
            }
        }

        try
        {
            using var process = FfmpegGenerator.MeasurePeakVolume(fileName, OnLine);
            if (timeout.HasValue)
            {
                await process.StartAndWaitAsync(cancellationToken, timeout.Value);
            }
            else
            {
                await process.StartAndWaitAsync(cancellationToken);
            }

            // Wait for the async stream readers to deliver everything before parsing.
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }

        lock (gate)
        {
            return ParsePeakDbfs(lines);
        }
    }

    /// <summary>Finds volumedetect's "max_volume: -2.8 dB" in ffmpeg's output.</summary>
    public static double? ParsePeakDbfs(IEnumerable<string> ffmpegOutputLines)
    {
        foreach (var line in ffmpegOutputLines)
        {
            var match = MaxVolumeRegex().Match(line);
            if (match.Success &&
                double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var peak))
            {
                return peak;
            }
        }

        return null;
    }

    [GeneratedRegex(@"max_volume:\s*(-?\d+(?:\.\d+)?)\s*dB", RegexOptions.IgnoreCase)]
    private static partial Regex MaxVolumeRegex();
}
