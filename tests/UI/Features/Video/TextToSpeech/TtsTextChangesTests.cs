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

    // 1:1: every row becomes exactly one main line, with the review's text.
    [Fact]
    public void EveryRowBecomesOneLine()
    {
        var main = Main(("Prvi red.", 0, 1000), ("Drugi red.", 1000, 2000));
        var session = new List<TtsStepResult>
        {
            Row(0, 1000, "Prvi red."),
            Row(1000, 2000, "Izmenjeni red."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(2, result.SessionLines.Count);
        Assert.Equal("Prvi red.", result.SessionLines[0].Text);
        Assert.Equal("Izmenjeni red.", result.SessionLines[1].Text);
    }

    // The grid keeps its own time codes: the review's audio times never move a line.
    [Fact]
    public void GridKeepsItsOwnTimes()
    {
        var main = Main(("Prvi red.", 0, 1000));
        var session = new List<TtsStepResult> { Row(0, 1400, "Izmenjeni red.") };

        var part = Assert.Single(TtsTextChanges.Build(session, main, clean: true).SessionLines);

        Assert.Equal(0, part.StartMs);
        Assert.Equal(1000, part.EndMs);
        Assert.Equal("Izmenjeni red.", part.Text);
    }

    // The reported catastrophe: the session has one row fewer than the subtitle (a line was
    // dropped), and mapping by position moved every text after it one line too early.
    [Fact]
    public void MissingLineInTheSession_DoesNotShiftTheTexts()
    {
        var main = Main(("Prvi red.", 0, 1000), ("Drugi red.", 1000, 2000),
                        ("Treci red.", 2000, 3000), ("Cetvrti red.", 3000, 4000));
        var session = new List<TtsStepResult>
        {
            Row(0, 1000, "Prvi red."),
            Row(2000, 3000, "Treci red."),
            Row(3000, 4000, "Cetvrti red."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(3, result.SessionLines.Count);
        Assert.Equal("Prvi red.", result.SessionLines[0].Text);
        Assert.Equal("Treci red.", result.SessionLines[1].Text);
        Assert.Equal("Cetvrti red.", result.SessionLines[2].Text);
    }

    // A row the review moved far away takes the next free line, never the previous text's line.
    [Fact]
    public void MovedRowTakesTheNextFreeLine()
    {
        var main = Main(("Prvi red.", 0, 1000), ("Drugi red.", 1000, 2000),
                        ("Treci red.", 2000, 3000), ("Cetvrti red.", 3000, 4000));
        var session = new List<TtsStepResult>
        {
            Row(0, 1000, "Prvi red."),
            Row(2000, 3000, "Treci red."),
            Row(60000, 61000, "Pomereni red."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(3, result.SessionLines.Count);
        Assert.Equal("Pomereni red.", result.SessionLines[2].Text);
    }

    // A review split publishes two segments for the one line, split evenly across its span.
    [Fact]
    public void SplitLineIsDividedEvenly()
    {
        var main = Main(("Ovo je jedna linija.", 0, 2000));
        var session = new List<TtsStepResult>
        {
            Row(0, 900, "Prvi deo."),
            Row(900, 2000, "Drugi deo."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(2, result.SessionLines.Count);
        Assert.Equal(0, result.SessionLines[0].StartMs);
        Assert.Equal(1000, result.SessionLines[0].EndMs);
        Assert.Equal(1000, result.SessionLines[1].StartMs);
        Assert.Equal(2000, result.SessionLines[1].EndMs);
    }

    // TTS markup is stripped on the way to the subtitle.
    [Fact]
    public void MarkupIsCleaned()
    {
        var main = Main(("Prvi red.", 0, 1000));
        var session = new List<TtsStepResult> { Row(0, 1000, "[warm] Prvi red. ,,,") };

        Assert.Equal("Prvi red.", Assert.Single(TtsTextChanges.Build(session, main, clean: true).SessionLines).Text);
    }
}
