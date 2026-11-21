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

    // Reported: edits made in a session that was exported to JSON and imported again never reached
    // the main window - the review could only diff against the imported text, which is the same.
    [Fact]
    public void ImportedSessionRowThatDiffersFromTheMainLine_IsAChange()
    {
        var main = new List<Paragraph>
        {
            new("Prvi red.", 0, 1000),
            new("Drugi red.", 1000, 2000),
        };
        var session = new List<TtsStepResult>
        {
            Row(0, 1000, "Prvi red."),
            Row(1000, 2000, "Izmenjeni red."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        var change = Assert.Single(result.TextChanges);
        Assert.Equal(1000, change.StartMs);
        Assert.Equal(2000, change.EndMs);
        Assert.Equal("Izmenjeni red.", change.NewText);
        Assert.Empty(result.Splits);
    }

    // A session row whose END differs from the main line is still that line: the review trims and
    // extends ends with the audio. This is the case that used to drop most edits of an imported
    // session (60 of 116 rows in a real one).
    [Fact]
    public void RowWithTheSameStartButADifferentEnd_IsStillTheLine()
    {
        var main = new List<Paragraph> { new("Prvi red.", 0, 1000) };
        var session = new List<TtsStepResult> { Row(0, 1400, "Izmenjeni red.") };

        var change = Assert.Single(TtsTextChanges.Build(session, main, clean: true).TextChanges);
        Assert.Equal(0, change.StartMs);
        Assert.Equal(1000, change.EndMs); // the main line's own end, not the session's
        Assert.Equal("Izmenjeni red.", change.NewText);
    }

    // The reported follow-up: the edited session had 116 lines, the grid 115 - one line had been
    // split in the review. Two rows inside one main line must produce a split, not one text edit.
    [Fact]
    public void TwoRowsInsideOneMainLine_ProduceASplit()
    {
        var main = new List<Paragraph> { new("Ovo je jedna linija.", 0, 2000) };
        var session = new List<TtsStepResult>
        {
            Row(0, 900, "Ovo je jedan deo."),
            Row(900, 2000, "A ovo je drugi."),
        };

        var result = TtsTextChanges.Build(session, main, clean: true);

        var split = Assert.Single(result.Splits);
        Assert.Equal(0, split.LineStartMs);
        Assert.Equal(2, split.Parts.Count);
        Assert.Equal("Ovo je jedan deo.", split.Parts[0].Text);
        Assert.Equal("A ovo je drugi.", split.Parts[1].Text);
        Assert.Equal(2000, split.Parts[1].EndMs); // the main line keeps its own end
        Assert.Empty(result.TextChanges);
    }

    // A review that nudges a cue's start (trim, waveform drag) must still find its line: the
    // reported session had four edited rows 200-730 ms off and they were lost as NO MATCH.
    [Fact]
    public void RowNudgedByAFewHundredMilliseconds_IsStillTheLine()
    {
        var main = new List<Paragraph> { new("Prvi red.", 0, 1000), new("Drugi red.", 2000, 3000) };
        var session = new List<TtsStepResult> { Row(2600, 3600, "Izmenjeni red.") };

        var change = Assert.Single(TtsTextChanges.Build(session, main, clean: true).TextChanges);
        Assert.Equal(2000, change.StartMs); // the main line's own start
        Assert.Equal(3000, change.EndMs);
        Assert.Equal("Izmenjeni red.", change.NewText);
    }

    // TTS markup alone must never overwrite a line.
    [Fact]
    public void MarkupOnlyDifference_IsNotAChange()
    {
        var main = new List<Paragraph> { new("Prvi red.", 0, 1000) };
        var session = new List<TtsStepResult> { Row(0, 1000, "[warm] Prvi red. ,,,") };

        Assert.Empty(TtsTextChanges.Build(session, main, clean: true).TextChanges);
    }

    // Line wrapping is not a text edit either.
    [Fact]
    public void DifferentLineWrapping_IsNotAChange()
    {
        var main = new List<Paragraph> { new("Prvi" + Environment.NewLine + "red.", 0, 1000) };
        var session = new List<TtsStepResult> { Row(0, 1000, "Prvi red.") };

        Assert.Empty(TtsTextChanges.Build(session, main, clean: true).TextChanges);
    }

    // A session imported for another subtitle must not touch anything.
    [Fact]
    public void RowWithNoMatchingMainLine_IsIgnored()
    {
        var main = new List<Paragraph> { new("Prvi red.", 0, 1000) };
        var session = new List<TtsStepResult> { Row(5000, 6000, "Druga emisija.") };

        var result = TtsTextChanges.Build(session, main, clean: true);
        Assert.Empty(result.TextChanges);
        Assert.Empty(result.Splits);
    }
}
