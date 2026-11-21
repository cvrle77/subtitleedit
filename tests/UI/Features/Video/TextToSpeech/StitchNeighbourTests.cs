using Nikse.SubtitleEdit.Features.Video.TextToSpeech;

namespace UITests.Features.Video.TextToSpeech;

public class StitchNeighbourTests
{
    // The regenerate now sends the neighbours as context, like the generate path: the previous
    // line's tail and the next line's head.
    [Fact]
    public void PreviousLine_ContributesItsTail()
    {
        var texts = new[] { "prvi red", "drugi red", "treci red" };

        Assert.Equal("drugi red", StitchNeighbour.Text(texts, 2, -1, fromEnd: true));
    }

    [Fact]
    public void NextLine_ContributesItsHead()
    {
        var texts = new[] { "prvi red", "drugi red", "treci red" };

        Assert.Equal("drugi red", StitchNeighbour.Text(texts, 0, +1, fromEnd: false));
    }

    // Excluded or blank lines are not in the audio, so the nearest spoken line is used.
    [Fact]
    public void BlankNeighbour_IsSkipped()
    {
        var texts = new[] { "prvi red", "", "treci red" };

        Assert.Equal("prvi red", StitchNeighbour.Text(texts, 2, -1, fromEnd: true));
    }

    [Fact]
    public void AtTheEdges_ThereIsNoContext()
    {
        var texts = new[] { "prvi red" };

        Assert.Equal(string.Empty, StitchNeighbour.Text(texts, 0, -1, fromEnd: true));
        Assert.Equal(string.Empty, StitchNeighbour.Text(texts, 0, +1, fromEnd: false));
    }

    // Long lines are capped so the request body stays the same size as the generate path's.
    [Fact]
    public void LongNeighbour_IsCapped()
    {
        var longText = new string('a', 150) + "KRAJ";
        var texts = new[] { longText, "sadasnji" };

        var tail = StitchNeighbour.Text(texts, 1, -1, fromEnd: true);
        Assert.Equal(StitchNeighbour.MaxChars, tail.Length);
        Assert.Equal("KRAJ", tail[^4..]);
    }
}
