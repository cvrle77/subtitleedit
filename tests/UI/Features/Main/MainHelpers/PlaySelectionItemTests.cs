using Nikse.SubtitleEdit.Features.Main;
using Nikse.SubtitleEdit.Features.Main.MainHelpers;

namespace UITests.Features.Main.MainHelpers;

public class PlaySelectionItemTests
{
    [Fact]
    public void GetNextSubtitle_AtCurrentEnd_AdvancesToNextSubtitle()
    {
        var first = MakeSubtitle(1, 2);
        var second = MakeSubtitle(3, 4);
        var item = new PlaySelectionItem([first, second], first.EndTime, true);

        var next = item.GetNextSubtitle(first.EndTime.TotalSeconds);

        Assert.Same(second, next);
        Assert.Equal(1, item.Index);
        Assert.Equal(second.EndTime.TotalSeconds, item.EndSeconds);
    }

    [Fact]
    public void GetNextSubtitle_AfterCurrentEnd_SkipsSubtitlesAlreadyPassed()
    {
        var first = MakeSubtitle(1, 2);
        var second = MakeSubtitle(3, 4);
        var third = MakeSubtitle(5, 6);
        var item = new PlaySelectionItem([first, second, third], first.EndTime, false);

        var next = item.GetNextSubtitle(4.5);

        Assert.Same(third, next);
        Assert.Equal(2, item.Index);
        Assert.Equal(third.EndTime.TotalSeconds, item.EndSeconds);
    }

    [Fact]
    public void GetNextSubtitle_AfterLastSubtitle_LoopsToFirst()
    {
        var first = MakeSubtitle(1, 2);
        var second = MakeSubtitle(3, 4);
        var item = new PlaySelectionItem([first, second], first.EndTime, true);

        Assert.Same(second, item.GetNextSubtitle(first.EndTime.TotalSeconds));

        var next = item.GetNextSubtitle(second.EndTime.TotalSeconds);

        Assert.Same(first, next);
        Assert.Equal(0, item.Index);
        Assert.Equal(first.EndTime.TotalSeconds, item.EndSeconds);
    }

    [Fact]
    public void FindSubtitleAtOrAfter_PositionInGap_AnchorsToNextSubtitle()
    {
        var first = MakeSubtitle(1, 3);
        var second = MakeSubtitle(9, 11);
        var item = new PlaySelectionItem([first, second], first.EndTime, false);

        var next = item.FindSubtitleAtOrAfter(5);

        Assert.Same(second, next);
        Assert.Equal(1, item.Index);
        Assert.Equal(second.EndTime.TotalSeconds, item.EndSeconds);
    }

    [Fact]
    public void FindSubtitleAtOrAfter_PositionInsideSubtitle_AnchorsToThatSubtitle()
    {
        var first = MakeSubtitle(1, 3);
        var second = MakeSubtitle(9, 11);
        var item = new PlaySelectionItem([first, second], first.EndTime, false);

        var next = item.FindSubtitleAtOrAfter(10);

        Assert.Same(second, next);
        Assert.Equal(1, item.Index);
    }

    [Fact]
    public void FindSubtitleAtOrAfter_PastTheEnd_ReturnsNull()
    {
        var first = MakeSubtitle(1, 3);
        var second = MakeSubtitle(9, 11);
        var item = new PlaySelectionItem([first, second], first.EndTime, false);

        Assert.Null(item.FindSubtitleAtOrAfter(20));
    }

    private static SubtitleLineViewModel MakeSubtitle(double startSeconds, double endSeconds) =>
        new()
        {
            StartTime = TimeSpan.FromSeconds(startSeconds),
            EndTime = TimeSpan.FromSeconds(endSeconds),
        };
}
