using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// One segment of the TTS review's state: the text and the time codes as the review has them. The
/// main subtitle is rebuilt from these, so a split, a merge or a nudged cue arrives exactly as the
/// review shows it.
/// </summary>
public class ReviewSplitPart
{
    public double StartMs { get; }
    public double EndMs { get; }
    public string Text { get; }

    /// <summary>Index of the main line this segment came from, or -1 when unknown.</summary>
    public int SourceLineIndex { get; }

    public ReviewSplitPart(double startMs, double endMs, string text, int sourceLineIndex = -1)
    {
        StartMs = startMs;
        EndMs = endMs;
        Text = text;
        SourceLineIndex = sourceLineIndex;
    }
}
