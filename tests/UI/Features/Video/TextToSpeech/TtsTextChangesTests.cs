using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech;

namespace UITests.Features.Video.TextToSpeech;

public class TtsTextChangesTests
{
    private static TtsStepResult Row(double start, double end, string text) => new()
    {
        Paragraph = new Paragraph(text, start, end),
        Text = text,
    };

    private static List<Paragraph> Main(params (string Text, double Start, double End)[] lines) =>
        lines.Select(l => new Paragraph(l.Text, l.Start, l.End)).ToList();

    // The review is the authority: every segment it has is published, in order, over the lines it
    // covers. Reported: edits from an imported session never reached the grid.
    [Fact]
    public void SessionSegments_ArePublishedInOrder()
    {
        var main = Main(("Prvi red.", 0, 1000), ("Drugi red.", 1000, 2000));
        var session = new List<TtsStepResult>
        {
            Row(0, 1000, "Prvi red."),
            Row(1000, 2000, "Izmenjeni red."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(0, result.CoveredStartMs);
        Assert.Equal(2, result.CoveredCount);
        Assert.Equal(2, result.SessionLines.Count);
        Assert.Equal("Izmenjeni red.", result.SessionLines[1].Text);
        Assert.Equal(1000, result.SessionLines[1].StartMs);
    }

    // The review trims ends with the audio, but the published segment keeps the main line's own
    // span so cues never end up on top of each other.
    [Fact]
    public void PublishedSegment_KeepsTheMainLineSpan()
    {
        var main = Main(("Prvi red.", 0, 1000));
        var session = new List<TtsStepResult> { Row(0, 1400, "Izmenjeni red.") };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(1, result.CoveredCount);
        var part = Assert.Single(result.SessionLines);
        Assert.Equal(0, part.StartMs);
        Assert.Equal(1000, part.EndMs);
        Assert.Equal("Izmenjeni red.", part.Text);
    }

    // A review split publishes two segments for the one line it covered.
    [Fact]
    public void TwoRowsInsideOneMainLine_PublishTwoSegments()
    {
        var main = Main(("Ovo je jedna linija.", 0, 2000));
        var session = new List<TtsStepResult>
        {
            Row(0, 900, "Ovo je jedan deo."),
            Row(900, 2000, "A ovo je drugi."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(1, result.CoveredCount);
        Assert.Equal(2, result.SessionLines.Count);
        Assert.Equal("Ovo je jedan deo.", result.SessionLines[0].Text);
        Assert.Equal("A ovo je drugi.", result.SessionLines[1].Text);
    }

    // Only the session's own rows are published: a main line inside the range that has no row is
    // not part of the review's state (155 imported rows must give 155 lines, not more).
    [Fact]
    public void UnspokenLineInsideTheRange_IsNotPublished()
    {
        var main = Main(("Prvi red.", 0, 1000), ("Drugi red.", 1000, 2000), ("Treci red.", 2000, 3000));
        var session = new List<TtsStepResult> { Row(0, 1000, "Prvi red."), Row(2000, 3000, "Treci red.") };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(3, result.CoveredCount);
        Assert.Equal(2, result.SessionLines.Count);
        Assert.Equal("Treci red.", result.SessionLines[1].Text);
    }

    // A nudge of a few hundred milliseconds (trim, waveform drag) is still the same line.
    [Fact]
    public void RowNudgedByAFewHundredMilliseconds_KeepsItsLine()
    {
        var main = Main(("Prvi red.", 0, 1000), ("Drugi red.", 2000, 3000));
        var session = new List<TtsStepResult> { Row(2600, 3600, "Izmenjeni red.") };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(2000, result.CoveredStartMs);
        Assert.Equal("Izmenjeni red.", Assert.Single(result.SessionLines).Text);
    }

    // TTS markup is stripped on the way to the subtitle.
    [Fact]
    public void MarkupIsCleaned()
    {
        var main = Main(("Prvi red.", 0, 1000));
        var session = new List<TtsStepResult> { Row(0, 1000, "[warm] Prvi red. ,,,") };

        Assert.Equal("Prvi red.", Assert.Single(TtsTextChanges.Build(session, main, clean: true).SessionLines).Text);
    }

    // A session imported for another subtitle publishes nothing.
    [Fact]
    public void RowWithNoMatchingMainLine_IsIgnored()
    {
        var main = Main(("Prvi red.", 0, 1000));
        var session = new List<TtsStepResult> { Row(5000, 6000, "Druga emisija.") };

        var result = TtsTextChanges.Build(session, main, clean: true);
        Assert.Empty(result.SessionLines);
        Assert.Equal(0, result.CoveredCount);
        Assert.Equal(1, result.NoMatch);
    }
}
