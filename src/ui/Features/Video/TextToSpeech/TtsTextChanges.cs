using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Media;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// The TTS review's state, ready to be written over the main subtitle. The review is the authority
/// and the rows are in the same order as the subtitle, so the mapping is by ORDER, not by time:
/// walking both lists once puts each row on the line it belongs to, whatever the review did to the
/// time codes (audio trim, cue moves). Each row becomes exactly one main line; a line the review
/// split is divided evenly across that line's own span, and the grid's time codes are never taken
/// from the review.
/// </summary>
public static class TtsTextChanges
{
    public sealed class Result
    {
        /// <summary>Every session row in order, as it must appear in the subtitle.</summary>
        public List<ReviewSplitPart> SessionLines { get; } = new();

        /// <summary>Start of the first main line the session covers (0 when nothing was covered).</summary>
        public double CoveredStartMs { get; set; }

        /// <summary>How many main lines the session replaces.</summary>
        public int CoveredCount { get; set; }

        public int Rows { get; set; }
    }

    public static Result Build(IEnumerable<TtsStepResult> results, IEnumerable<Paragraph> mainLines, bool clean)
    {
        var result = new Result();
        var lines = mainLines.ToList();
        var rows = results.ToList();
        result.Rows = rows.Count;

        if (lines.Count == 0 || rows.Count == 0)
        {
            return result;
        }

        // The row list and the subtitle are the same list: row i belongs to line (offset + i). No
        // time matching at all - the review's times are audio times and a cue moved there would
        // otherwise drag its text onto a different line.
        var firstRowStart = rows[0].Paragraph.StartTime.TotalMilliseconds;
        var offset = lines.FindIndex(l => Math.Abs(l.StartTime.TotalMilliseconds - firstRowStart) <= 1500);
        if (offset < 0)
        {
            offset = 0;
        }

        var perLine = new List<TtsStepResult>[lines.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var lineIndex = Math.Min(offset + i, lines.Count - 1);
            (perLine[lineIndex] ??= new List<TtsStepResult>()).Add(rows[i]);
        }

        var first = offset;
        var lastMapped = Math.Min(offset + rows.Count - 1, lines.Count - 1);
        // A session that starts at the top of the subtitle is the whole subtitle: the grid must end
        // up with exactly the rows the review has (a line with no row was dropped in the review).
        var last = offset == 0 ? lines.Count - 1 : lastMapped;
        result.CoveredStartMs = lines[first].StartTime.TotalMilliseconds;
        result.CoveredCount = last - first + 1;

        for (var i = first; i <= last; i++)
        {
            var group = perLine[i];
            if (group == null)
            {
                // A line the session has no row for is not part of the review's state: the grid must
                // hold exactly the rows the review holds (155 rows in, 155 lines out).
                continue;
            }

            var lineStart = lines[i].StartTime.TotalMilliseconds;
            var span = lines[i].EndTime.TotalMilliseconds - lineStart;
            for (var k = 0; k < group.Count; k++)
            {
                var start = lineStart + span * k / group.Count;
                var end = lineStart + span * (k + 1) / group.Count;
                result.SessionLines.Add(new ReviewSplitPart(
                    start,
                    end,
                    clean ? TtsTextCleaner.Clean(group[k].Text) : group[k].Text));
            }
        }

        Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
            $"TTS session: rows={rows.Count} coveredLines={result.CoveredCount} from={result.CoveredStartMs:F0} publishLines={result.SessionLines.Count}");

        return result;
    }
}
