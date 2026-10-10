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
    private const int ContextSize = 64;  // 16 kHz context prepended to each frame
    private const int StateSize = 128;

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _stateInputName;
    private readonly string? _sampleRateInputName;
    private readonly string _outputName;
    private readonly string _stateOutputName;

    /// <summary>Largest speech probability seen in the last <see cref="DetectSpeech"/> run (diagnostics).</summary>
    public float LastMaxProbability { get; private set; }

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
        var probabilities = GetProbabilities(waveFileName);
        if (probabilities.Length == 0)
        {
            return new List<(double, double)>();
        }
        var max = 0f;
        foreach (var probability in probabilities)
        {
            if (probability > max)
            {
                max = probability;
            }
        }

        LastMaxProbability = max;
        return ToSegments(probabilities, threshold, minSpeechSeconds, minSilenceSeconds);
    }

    private float[] GetProbabilities(string waveFileName)
    {
        using var stream = new FileStream(waveFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        var header = new WaveHeader2(stream);
        if (header.AudioFormat != WaveHeader2.AudioFormatPcm || header.BitsPerSample != 16 ||
            header.NumberOfChannels < 1 || header.SampleRate <= 0)
        {
            return Array.Empty<float>();
        }

        var channels = header.NumberOfChannels;
        var sourceRate = header.SampleRate;
        var bytesPerFrame = 2 * channels;
        var totalSourceFrames = header.DataChunkSize / bytesPerFrame;
        if (totalSourceFrames <= 0)
        {
            return Array.Empty<float>();
        }

        // Read and resample a block at a time instead of the whole track: a two-hour stereo source is
        // over a gigabyte of floats once materialized, while the model only needs 512 samples at a
        // time. Only the (small) per-frame probabilities array is kept.
        var ratio = (double)sourceRate / SampleRate;
        var outSamples = (long)totalSourceFrames * SampleRate / sourceRate;
        var frameCount = (int)(outSamples / FrameSize);
        var probabilities = new float[frameCount];
        if (frameCount == 0)
        {
            return probabilities;
        }

        var state = new float[2 * StateSize];
        var stateDims = new[] { 2, 1, StateSize };
        var context = new float[ContextSize];
        var frame = new float[FrameSize];
        var frameFill = 0;
        var probIndex = 0;

        const int blockFrames = 1024;
        var blockSamples = blockFrames * FrameSize; // 16 kHz samples per block
        var readBuffer = new byte[(int)Math.Ceiling(blockSamples * ratio + 2) * bytesPerFrame];
        var totalOutSamples = (long)frameCount * FrameSize;
        for (var outStart = 0L; outStart < totalOutSamples && probIndex < frameCount; outStart += blockSamples)
        {
            var count = (int)Math.Min(blockSamples, totalOutSamples - outStart);
            var block = ReadResampledBlock(stream, header, bytesPerFrame, channels, ratio, outStart, count, readBuffer);
            for (var i = 0; i < count; i++)
            {
                frame[frameFill++] = block[i];
                if (frameFill == FrameSize)
                {
                    probabilities[probIndex++] = RunFrame(frame, state, stateDims, context);
                    frameFill = 0;
                }
            }
        }

        return probabilities;
    }

    // Runs one 512-sample frame through the model, carrying the recurrent state and the 64-sample
    // context (the model wants the previous 64 samples prepended to each frame, or every probability
    // reads as noise).
    private float RunFrame(float[] frame, float[] state, int[] stateDims, float[] context)
    {
        var input = new DenseTensor<float>(new[] { 1, ContextSize + FrameSize });
        for (var i = 0; i < ContextSize; i++)
        {
            input[0, i] = context[i];
        }

        for (var i = 0; i < FrameSize; i++)
        {
            input[0, ContextSize + i] = frame[i];
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, input),
            NamedOnnxValue.CreateFromTensor(_stateInputName, new DenseTensor<float>(state, stateDims)),
        };

        if (_sampleRateInputName != null)
        {
            // The model's "sr" input is a scalar (0-dimensional), not a [1] tensor.
            var sampleRate = new DenseTensor<long>(new[] { (long)SampleRate }, new int[0]);
            inputs.Add(NamedOnnxValue.CreateFromTensor(_sampleRateInputName, sampleRate));
        }

        using var results = _session.Run(inputs);
        var output = results.First(r => r.Name == _outputName).AsTensor<float>();
        var probability = output.GetValue(0);

        var nextState = results.First(r => r.Name == _stateOutputName).AsTensor<float>();
        var index = 0;
        foreach (var value in nextState)
        {
            state[index++] = value;
        }

        Array.Copy(frame, FrameSize - ContextSize, context, 0, ContextSize);
        return probability;
    }

    // Reads the source frames covering the 16 kHz samples [outStart, outStart + count) and linearly
    // resamples them, reading one extra source sample for the interpolation's right neighbour.
    private static float[] ReadResampledBlock(Stream stream, WaveHeader2 header, int bytesPerFrame, int channels,
        double ratio, long outStart, int count, byte[] buffer)
    {
        var sourceStart = (long)(outStart * ratio);
        var sourceEnd = (long)((outStart + count - 1) * ratio) + 1; // inclusive right neighbour
        var sourceCount = (int)Math.Max(1, sourceEnd - sourceStart + 1);

        stream.Position = header.DataStartPosition + sourceStart * bytesPerFrame;
        var remainingBytes = header.DataChunkSize - sourceStart * bytesPerFrame;
        var bytesToRead = (int)Math.Min((long)sourceCount * bytesPerFrame, Math.Max(0, remainingBytes));
        var read = ReadFully(stream, buffer, bytesToRead);
        var framesRead = read / bytesPerFrame;

        var source = new float[sourceCount];
        for (var f = 0; f < framesRead; f++)
        {
            var offset = f * bytesPerFrame;
            var sum = 0;
            for (var c = 0; c < channels; c++)
            {
                sum += (short)(buffer[offset + c * 2] | (buffer[offset + c * 2 + 1] << 8));
            }

            source[f] = sum / (float)channels / 32768f;
        }

        // End of data: repeat the last real sample so interpolation never reads past the end.
        for (var f = framesRead; f < sourceCount; f++)
        {
            source[f] = framesRead > 0 ? source[framesRead - 1] : 0f;
        }

        var output = new float[count];
        for (var i = 0; i < count; i++)
        {
            var position = (outStart + i) * ratio - sourceStart;
            var i0 = (int)position;
            if (i0 >= sourceCount - 1)
            {
                i0 = Math.Max(0, sourceCount - 2);
            }

            var i1 = Math.Min(i0 + 1, sourceCount - 1);
            var fraction = (float)(position - i0);
            output[i] = source[i0] * (1 - fraction) + source[i1] * fraction;
        }

        return output;
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

            if (segment.Start < positionSeconds && segment.End > positionSeconds)
            {
                // The cut is already inside this speech segment: there is no leading silence to trim,
                // so leave the line alone instead of jumping forward and dropping the speech between.
                return null;
            }
        }

        return null;
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
