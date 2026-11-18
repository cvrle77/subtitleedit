using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// A main line that one session row alone cannot represent: the review split it into two or more
/// rows (dub timing), so the grid has to become two or more lines as well. Keyed by the main line's
/// start time; the parts carry the session's texts and their own start times.
/// </summary>
public class ReviewSplit
{
    public double LineStartMs { get; }
    public List<ReviewSplitPart> Parts { get; }

    public ReviewSplit(double lineStartMs, List<ReviewSplitPart> parts)
    {
        LineStartMs = lineStartMs;
        Parts = parts;
    }
}

public class ReviewSplitPart
{
    public double StartMs { get; }
    public double EndMs { get; }
    public string Text { get; }

    public ReviewSplitPart(double startMs, double endMs, string text)
    {
        StartMs = startMs;
        EndMs = endMs;
        Text = text;
    }
}
