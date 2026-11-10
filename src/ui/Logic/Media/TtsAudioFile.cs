using System;
using System.IO;

namespace Nikse.SubtitleEdit.Logic.Media;

// How long a generated clip is, in seconds - 0 when the file holds no audio at all.
//
// This is what tells an "empty" clip apart from a real one. ffmpeg writes a complete WAV header
// even when its filters produced no samples (an all-silent input trimmed to nothing), so the file
// is ~78 bytes and non-zero: every `new FileInfo(f).Length > 0` check accepted it, and it became
// the row's audio and was exported as a real clip.
public static class TtsAudioFile
{
    public static double GetLengthSeconds(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
        {
            return 0;
        }

        try
        {
            using var stream = File.OpenRead(fileName);
            var header = new WaveHeader2(stream);
            if (header.ChunkId == "RIFF" && header.Format == "WAVE" && header.BytesPerSecond > 0)
            {
                return header.LengthInSeconds;
            }
        }
        catch
        {
            // Not a WAV - ElevenLabs (and the chunk cuts) produce MP3, and WaveHeader2 throws on it.
        }

        return Mp3DurationReader.TryGetDurationSeconds(fileName) ?? 0;
    }

    // True when the clip really holds audio. 10 ms is the floor: no real cue is that short, and
    // anything below it is a header-only file.
    public static bool HasAudio(string? fileName) => GetLengthSeconds(fileName) > 0.01;
}
