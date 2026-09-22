using Nikse.SubtitleEdit.Logic.Media;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace UITests.Logic.Media;

public class SileroVadTests
{
    [Fact]
    public void FindSpeechStartAfter_ReturnsTheNextSegmentStart()
    {
        var segments = new List<(double Start, double End)> { (1, 3), (9, 11) };

        Assert.Equal(9, SileroVad.FindSpeechStartAfter(segments, 5));
    }

    [Fact]
    public void FindSpeechStartAfter_NoSegmentAfter_ReturnsNull()
    {
        var segments = new List<(double Start, double End)> { (1, 3), (9, 11) };

        Assert.Null(SileroVad.FindSpeechStartAfter(segments, 20));
    }

    [Fact]
    public void FindSpeechStartAfter_SegmentStartingAtPosition_IsSkipped()
    {
        var segments = new List<(double Start, double End)> { (9, 11), (15, 17) };

        // A cut exactly on a segment start belongs to that segment, not the next one.
        Assert.Equal(15, SileroVad.FindSpeechStartAfter(segments, 9));
    }

    // Runs the real ONNX model when it is installed on this machine, so the input/output names,
    // tensor shapes and the 64-sample context handling are exercised instead of only found out on a
    // split. A tone is not speech, so only that it runs is asserted here.
    [Fact]
    public void DetectSpeech_RunsTheModel_WhenInstalled()
    {
        var modelPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Subtitle Edit",
            SileroVad.ModelFileName);
        if (!File.Exists(modelPath))
        {
            return; // model not installed here
        }

        var wavPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
        try
        {
            WriteWav(wavPath, 16000, 1, 3.0, t => t < 1.0 ? 0.0 : 0.6 * Math.Sin(2 * Math.PI * 200 * t));

            using var vad = new SileroVad(modelPath);
            var segments = vad.DetectSpeech(wavPath);

            Assert.NotNull(segments);
            Assert.InRange(vad.LastMaxProbability, 0f, 1f);
        }
        finally
        {
            if (File.Exists(wavPath))
            {
                File.Delete(wavPath);
            }
        }
    }

    private static void WriteWav(string path, int sampleRate, int channels, double seconds, Func<double, double> sample)
    {
        var count = (int)(sampleRate * seconds);
        var dataSize = count * channels * 2;
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + dataSize);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(dataSize);
        for (var i = 0; i < count; i++)
        {
            var value = (short)(Math.Clamp(sample((double)i / sampleRate), -1.0, 1.0) * 32767);
            for (var c = 0; c < channels; c++)
            {
                writer.Write(value);
            }
        }
    }
}
