using System.Collections.Generic;
using System.Text;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.Engines;

/// <summary>
/// A run of consecutive cues synthesised as a single ElevenLabs request. The cues stay in their
/// original order, so the audio returned for the chunk can be split back into one clip per cue.
/// </summary>
public sealed record ElevenLabsChunk(IReadOnlyList<int> ParagraphIndexes, string Text);

/// <summary>
/// Groups the cues of a run into the largest possible chunks for the ElevenLabs timestamped
/// dialogue endpoint. The endpoint asks for at most ~2000 characters per request for reliable
/// generation, and a single bigger request is what gives the model the "one continuous
/// performance" the per-line requests cannot - so the goal is to fill each chunk as far as the
/// limit allows. The mapping back to individual cues uses the endpoint's per-character timings, so
/// a clean cut does not depend on where the chunk boundary falls.
/// </summary>
public static class ElevenLabsChunker
{
    // Slightly under the endpoint's ~2000-character guidance.
    public const int DefaultMaxChunkChars = 1800;

    public static List<ElevenLabsChunk> Build(
        IReadOnlyList<(int Index, string Text)> cues,
        int maxChars = DefaultMaxChunkChars)
    {
        var chunks = new List<ElevenLabsChunk>();
        var currentIndexes = new List<int>();
        var currentText = new StringBuilder();

        foreach (var (index, text) in cues)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            // A joining space is added between cues, so count it for every cue but the first.
            var additional = currentText.Length == 0 ? text.Length : text.Length + 1;
            if (currentIndexes.Count > 0 && currentText.Length + additional > maxChars)
            {
                chunks.Add(new ElevenLabsChunk(currentIndexes.ToArray(), currentText.ToString()));
                currentIndexes = new List<int>();
                currentText.Clear();
            }

            if (currentText.Length > 0)
            {
                currentText.Append(' ');
            }

            currentIndexes.Add(index);
            currentText.Append(text);
        }

        if (currentIndexes.Count > 0)
        {
            chunks.Add(new ElevenLabsChunk(currentIndexes.ToArray(), currentText.ToString()));
        }

        return chunks;
    }
}
