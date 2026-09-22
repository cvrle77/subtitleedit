using Nikse.SubtitleEdit.Logic.Media;
using System.Collections.Generic;
using Xunit;

namespace UITests.Logic.Media;

public class SileroVadTests
{
    [Fact]
    public void FindSpeechStartAfter_ReturnsTheNextSegmentStart()
    {
        var segments = new List<(double Start, double End)> { (1, 3), (9, 11) };

        Assert.Equal(9, SileroVad.FindSpeechStartAfter(segments, 5));
    }

    [Fact]
    public void FindSpeechStartAfter_NoSegmentAfter_ReturnsNull()
    {
        var segments = new List<(double Start, double End)> { (1, 3), (9, 11) };

        Assert.Null(SileroVad.FindSpeechStartAfter(segments, 20));
    }

    [Fact]
    public void FindSpeechStartAfter_SegmentStartingAtPosition_IsSkipped()
    {
        var segments = new List<(double Start, double End)> { (9, 11), (15, 17) };

        // A cut exactly on a segment start belongs to that segment, not the next one.
        Assert.Equal(15, SileroVad.FindSpeechStartAfter(segments, 9));
    }
}
