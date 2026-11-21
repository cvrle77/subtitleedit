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

    // 1:1: every review row becomes exactly one main line - text and time codes verbatim.
    [Fact]
    public void EveryRowBecomesOneLine_WithItsOwnTimes()
    {
        var main = Main(("Prvi red.", 0, 1000), ("Drugi red.", 1000, 2000));
        var session = new List<TtsStepResult>
        {
            Row(0, 1000, "Prvi red."),
            Row(1000, 2500, "Izmenjeni red."), // end moved in the review
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(2, result.SessionLines.Count);
        Assert.Equal("Izmenjeni red.", result.SessionLines[1].Text);
        Assert.Equal(1000, result.SessionLines[1].StartMs);
        Assert.Equal(2500, result.SessionLines[1].EndMs); // the review's own end
    }

    // The reported loop: he edits line 9 and moves its start and end; every line keeps its text and
    // line 9 gets the edited text plus the moved times.
    [Fact]
    public void EditedNinthLine_WithMovedTimes_ComesOutEdited()
    {
        var main = Enumerable.Range(0, 12)
            .Select(i => new Paragraph($"Linija {i + 1}.", i * 1000, i * 1000 + 900))
            .ToList();

        var session = new List<TtsStepResult>();
        for (var i = 0; i < 12; i++)
        {
            var text = i == 8 ? "Izmenjena deveta" : $"Linija {i + 1}.";
            var start = i * 1000;
            var end = start + 900;
            if (i == 8)
            {
                start += 250;
                end += 400;
            }

            session.Add(Row(start, end, text));
        }

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(12, result.SessionLines.Count);
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(i == 8 ? "Izmenjena deveta" : $"Linija {i + 1}.", result.SessionLines[i].Text);
        }

        Assert.Equal(8250, result.SessionLines[8].StartMs);
        Assert.Equal(9300, result.SessionLines[8].EndMs);
    }

    // A split in the review gives two lines, each with the times the review gave it.
    [Fact]
    public void SplitPublishesBothHalvesWithTheirTimes()
    {
        var main = Main(("Ovo je jedna linija.", 0, 2000));
        var session = new List<TtsStepResult>
        {
            Row(0, 900, "Prvi deo."),
            Row(900, 2000, "Drugi deo."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        Assert.Equal(2, result.SessionLines.Count);
        Assert.Equal("Prvi deo.", result.SessionLines[0].Text);
        Assert.Equal(900, result.SessionLines[0].EndMs);
        Assert.Equal("Drugi deo.", result.SessionLines[1].Text);
        Assert.Equal(900, result.SessionLines[1].StartMs);
    }

    // A line missing from the session must not shift the following texts onto the wrong lines.
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

    // TTS markup is stripped on the way to the subtitle.
    [Fact]
    public void MarkupIsCleaned()
    {
        var main = Main(("Prvi red.", 0, 1000));
        var session = new List<TtsStepResult> { Row(0, 1000, "[warm] Prvi red. ,,,") };

        Assert.Equal("Prvi red.", Assert.Single(TtsTextChanges.Build(session, main, clean: true).SessionLines).Text);
    }
}
