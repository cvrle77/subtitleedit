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

        var changes = TtsTextChanges.Build(session, main, clean: true);

        var change = Assert.Single(changes);
        Assert.Equal(1000, change.StartMs);
        Assert.Equal(2000, change.EndMs);
        Assert.Equal("Izmenjeni red.", change.NewText);
    }

    // TTS markup alone must never overwrite a line.
    [Fact]
    public void MarkupOnlyDifference_IsNotAChange()
    {
        var main = new List<Paragraph> { new("Prvi red.", 0, 1000) };
        var session = new List<TtsStepResult> { Row(0, 1000, "[warm] Prvi red. ,,,") };

        Assert.Empty(TtsTextChanges.Build(session, main, clean: true));
    }

    // Line wrapping is not a text edit either.
    [Fact]
    public void DifferentLineWrapping_IsNotAChange()
    {
        var main = new List<Paragraph> { new("Prvi" + Environment.NewLine + "red.", 0, 1000) };
        var session = new List<TtsStepResult> { Row(0, 1000, "Prvi red.") };

        Assert.Empty(TtsTextChanges.Build(session, main, clean: true));
    }

    // A session imported for another subtitle must not touch anything.
    [Fact]
    public void RowWithNoMatchingMainLine_IsIgnored()
    {
        var main = new List<Paragraph> { new("Prvi red.", 0, 1000) };
        var session = new List<TtsStepResult> { Row(5000, 6000, "Druga emisija.") };

        Assert.Empty(TtsTextChanges.Build(session, main, clean: true));
    }
}
