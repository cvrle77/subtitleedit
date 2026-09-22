using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nikse.SubtitleEdit.Logic.Media;

/// <summary>
/// Silero VAD (voice activity detection) over a video's extracted audio, so the leading silence a
/// split leaves can be trimmed to the next <i>voice</i> instead of to whatever sound the amplitude
/// waveform shows first (a spoon, a clink, rustle). The model is installed from the app (Settings),
/// not shipped with the build.
/// </summary>
public sealed class SileroVad : IDisposable
{
    public const string ModelFileName = "silero_vad.onnx";
    public const string ModelUrl = "https://raw.githubusercontent.com/snakers4/silero-vad/master/src/silero_vad/data/silero_vad.onnx";
    public const string ModelSizeText = "2 MB";

    private const int SampleRate = 16000;
    private const int FrameSize = 512;   // 32 ms at 16 kHz
    private const int StateSize = 128;

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _stateInputName;
    private readonly string? _sampleRateInputName;
    private readonly string _outputName;
    private readonly string _stateOutputName;

    public SileroVad(string modelPath)
    {
        _session = new InferenceSession(modelPath);
        _inputName = FindInput("input");
        _stateInputName = FindInput("state", "h");
        _sampleRateInputName = _session.InputMetadata.Keys.FirstOrDefault(k => k == "sr");
        _outputName = _session.OutputMetadata.Keys.FirstOrDefault(k => k == "output") ?? _session.OutputMetadata.Keys.First();
        _stateOutputName = _session.OutputMetadata.Keys.FirstOrDefault(k => k == "stateN") ?? _session.OutputMetadata.Keys.Last();
    }

    private string FindInput(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (_session.InputMetadata.Keys.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Silero VAD model has no expected input ({string.Join("/", candidates)}).");
    }

    /// <summary>
    /// Runs the model over a 16-bit PCM wav and returns the speech segments, in seconds.
    /// </summary>
    public List<(double Start, double End)> DetectSpeech(string waveFileName, double threshold = 0.5,
        double minSpeechSeconds = 0.25, double minSilenceSeconds = 0.1)
    {
        var samples = ReadMono16k(waveFileName);
        if (samples == null || samples.Length < FrameSize)
        {
            return new List<(double, double)>();
        }

        var probabilities = GetProbabilities(samples);
        return ToSegments(probabilities, threshold, minSpeechSeconds, minSilenceSeconds);
    }

    private float[] GetProbabilities(float[] samples)
    {
        var frameCount = samples.Length / FrameSize;
        var probabilities = new float[frameCount];
        var state = new float[2 * StateSize];
        var stateDims = new[] { 2, 1, StateSize };

        for (var frame = 0; frame < frameCount; frame++)
        {
            var input = new DenseTensor<float>(new[] { 1, FrameSize });
            var offset = frame * FrameSize;
            for (var i = 0; i < FrameSize; i++)
            {
                input[0, i] = samples[offset + i];
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, input),
                NamedOnnxValue.CreateFromTensor(_stateInputName, new DenseTensor<float>(state, stateDims)),
            };

            if (_sampleRateInputName != null)
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor(_sampleRateInputName,
                    new DenseTensor<long>(new[] { (long)SampleRate }, new[] { 1 })));
            }

            using var results = _session.Run(inputs);
            var output = results.First(r => r.Name == _outputName).AsTensor<float>();
            probabilities[frame] = output.GetValue(0);

            var nextState = results.First(r => r.Name == _stateOutputName).AsTensor<float>();
            var index = 0;
            foreach (var value in nextState)
            {
                state[index++] = value;
            }
        }

        return probabilities;
    }

    private static List<(double Start, double End)> ToSegments(IReadOnlyList<float> probabilities,
        double threshold, double minSpeechSeconds, double minSilenceSeconds)
    {
        var frameSeconds = (double)FrameSize / SampleRate;

        var runs = new List<(int Start, int End)>();
        var inSpeech = false;
        var start = 0;
        for (var i = 0; i < probabilities.Count; i++)
        {
            if (probabilities[i] >= threshold)
            {
                if (!inSpeech)
                {
                    inSpeech = true;
                    start = i;
                }
            }
            else if (inSpeech)
            {
                inSpeech = false;
                runs.Add((start, i));
            }
        }

        if (inSpeech)
        {
            runs.Add((start, probabilities.Count));
        }

        // A pause shorter than minSilenceSeconds is a breath or a stop consonant, not the end.
        var minSilenceFrames = (int)Math.Round(minSilenceSeconds / frameSeconds);
        var merged = new List<(int Start, int End)>();
        foreach (var run in runs)
        {
            if (merged.Count > 0 && run.Start - merged[^1].End < minSilenceFrames)
            {
                merged[^1] = (merged[^1].Start, run.End);
            }
            else
            {
                merged.Add(run);
            }
        }

        var minSpeechFrames = (int)Math.Round(minSpeechSeconds / frameSeconds);
        var segments = new List<(double Start, double End)>();
        foreach (var run in merged)
        {
            if (run.End - run.Start >= minSpeechFrames)
            {
                segments.Add((run.Start * frameSeconds, run.End * frameSeconds));
            }
        }

        return segments;
    }

    /// <summary>
    /// The start of the first speech segment that begins after <paramref name="positionSeconds"/>,
    /// or null when there is none. This is what the split trim uses: a plain lookup, no per-split work.
    /// </summary>
    public static double? FindSpeechStartAfter(IReadOnlyList<(double Start, double End)> segments, double positionSeconds)
    {
        foreach (var segment in segments)
        {
            if (segment.Start > positionSeconds)
            {
                return segment.Start;
            }
        }

        return null;
    }

    private static float[]? ReadMono16k(string waveFileName)
    {
        using var stream = new FileStream(waveFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
        var header = new WaveHeader2(stream);
        if (header.AudioFormat != WaveHeader2.AudioFormatPcm || header.BitsPerSample != 16 ||
            header.NumberOfChannels < 1 || header.SampleRate <= 0)
        {
            return null;
        }

        var channels = header.NumberOfChannels;
        var sourceRate = header.SampleRate;
        var bytesPerFrame = 2 * channels;
        var totalFrames = (int)(header.DataChunkSize / bytesPerFrame);
        if (totalFrames <= 0)
        {
            return null;
        }

        var mono = new float[totalFrames];
        var buffer = new byte[bytesPerFrame * 4096];
        stream.Position = header.DataStartPosition;

        var frameIndex = 0;
        var remaining = (long)header.DataChunkSize;
        while (remaining >= bytesPerFrame && frameIndex < totalFrames)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            toRead -= toRead % bytesPerFrame;
            var read = ReadFully(stream, buffer, toRead);
            if (read <= 0)
            {
                break;
            }

            remaining -= read;
            for (var i = 0; i + bytesPerFrame <= read && frameIndex < totalFrames; i += bytesPerFrame)
            {
                var sum = 0;
                for (var c = 0; c < channels; c++)
                {
                    sum += (short)(buffer[i + c * 2] | (buffer[i + c * 2 + 1] << 8));
                }

                mono[frameIndex++] = sum / (float)channels / 32768f;
            }
        }

        if (frameIndex == 0)
        {
            return null;
        }

        if (frameIndex < mono.Length)
        {
            Array.Resize(ref mono, frameIndex);
        }

        if (sourceRate == SampleRate)
        {
            return mono;
        }

        // Linear resample to 16 kHz - good enough for a VAD.
        var outputLength = (int)((long)mono.Length * SampleRate / sourceRate);
        var output = new float[outputLength];
        var ratio = (double)sourceRate / SampleRate;
        for (var i = 0; i < outputLength; i++)
        {
            var position = i * ratio;
            var i0 = (int)position;
            var i1 = Math.Min(i0 + 1, mono.Length - 1);
            var fraction = (float)(position - i0);
            output[i] = mono[i0] * (1 - fraction) + mono[i1] * fraction;
        }

        return output;
    }

    private static int ReadFully(Stream stream, byte[] buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = stream.Read(buffer, total, count - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
