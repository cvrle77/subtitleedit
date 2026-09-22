using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Features.Main.MainHelpers;

public class PlaySelectionItem
{
    public double EndSeconds { get; set; } 
    public int Index { get; set; } 
    public bool Loop { get; set; }
    public List<SubtitleLineViewModel> Subtitles { get; set; }

    public PlaySelectionItem(List<SubtitleLineViewModel> subtitles, TimeSpan endTime, bool loop)
    {
        Subtitles = subtitles;
        EndSeconds = endTime.TotalSeconds;
        Index = 0;
        Loop = loop;
    }

    public SubtitleLineViewModel? GetNextSubtitle(double playerPositionInSeconds)
    {
        // Start after the current subtitle and require the candidate to extend beyond the playhead.
        // At an exact end boundary, selecting the current subtitle again can seek back to its start.
        var nextIndex = Subtitles.FindIndex(Index + 1, s => s.EndTime.TotalSeconds > playerPositionInSeconds);
        if (nextIndex >= 0)
        {
            Index = nextIndex;
            var s = Subtitles[Index];
            EndSeconds = s.EndTime.TotalSeconds;
            return s;
        }
        
        if (Loop)
        {
            Index = 0;
            var s = Subtitles[Index];
            EndSeconds = s.EndTime.TotalSeconds;
            return s;
        }

        return null;
    }

    public double GetCurrentStartSeconds()
    {
        if (Index < 0 || Index >= Subtitles.Count)
        {
            return 0;
        }

        return Subtitles[Index].StartTime.TotalSeconds;
    }

    public SubtitleLineViewModel? GetCurrentSubtitle()
    {
        return Index >= 0 && Index < Subtitles.Count ? Subtitles[Index] : null;
    }

    /// <summary>
    /// Moves the selection onto the first subtitle that contains the playhead or starts after it,
    /// so resuming after the playhead was moved (a click in a gap, a scrub) hooks onto the next
    /// subtitle instead of continuing from a stale index. Null when the playhead is past the last
    /// subtitle of the selection.
    /// </summary>
    public SubtitleLineViewModel? FindSubtitleAtOrAfter(double playerPositionInSeconds)
    {
        for (var i = 0; i < Subtitles.Count; i++)
        {
            if (Subtitles[i].EndTime.TotalSeconds > playerPositionInSeconds)
            {
                Index = i;
                EndSeconds = Subtitles[i].EndTime.TotalSeconds;
                return Subtitles[i];
            }
        }

        return null;
    }

    public bool HasGapOrIsFirst()
    {
        if (Index < 1)
        {
            return true;
        }

        var previousEnd = Subtitles[Index-1].EndTime.TotalMilliseconds;
        var currentStart = Subtitles[Index].StartTime.TotalMilliseconds;
        
        return currentStart - previousEnd > 100;
    }
}
