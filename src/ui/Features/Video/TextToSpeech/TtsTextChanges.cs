using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Media;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// The TTS review's state, ready to be written over the main subtitle. Each row is placed on the
/// line its own start belongs to (containment first, then the nearest start), because a session may
/// not contain every line - mapping by position made every text after a missing line land one line
/// too early. A row the review moved far away keeps the previous row's line instead of jumping the
/// queue, and the grid's time codes are never taken from the review.
/// </summary>
public static class TtsTextChanges
{
    public sealed class Result
    {
        public List<ReviewSplitPart> SessionLines { get; } = new();
        public double CoveredStartMs { get; set; }
        public int CoveredCount { get; set; }
        public int Rows { get; set; }
    }

    // The review trims and nudges the time codes; a start this close to a line's start still belongs
    // to that line.
    private const double NearestStartToleranceMs = 2000;

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

        var perLine = new List<TtsStepResult>[lines.Count];
        var claimed = new bool[lines.Count];
        var cursor = 0;

        foreach (var row in rows)
        {
            var startMs = row.Paragraph.StartTime.TotalMilliseconds;
            var index = -1;

            // 1. the line whose span contains this start (a split half, or an exact line)
            for (var i = 0; i < lines.Count; i++)
            {
                if (startMs >= lines[i].StartTime.TotalMilliseconds - 0.5 &&
                    startMs <= lines[i].EndTime.TotalMilliseconds + 0.5)
                {
                    index = i;
                    break;
                }
            }

            // 2. the nearest line start (a trimmed or nudged cue)
            if (index < 0)
            {
                var bestDelta = double.MaxValue;
                for (var i = 0; i < lines.Count; i++)
                {
                    var delta = Math.Abs(lines[i].StartTime.TotalMilliseconds - startMs);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        index = i;
                    }
                }

                if (bestDelta > NearestStartToleranceMs)
                {
                    index = -1;
                }
            }

            // 3. moved far away: take the next free line in order instead of a random one
            if (index < 0)
            {
                for (var i = cursor; i < lines.Count; i++)
                {
                    if (!claimed[i])
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    index = lines.Count - 1;
                }
            }

            cursor = Math.Max(cursor, index);
            claimed[index] = true;
            (perLine[index] ??= new List<TtsStepResult>()).Add(row);
        }

        var first = Array.FindIndex(perLine, g => g != null);
        var last = Array.FindLastIndex(perLine, g => g != null);
        if (first < 0 || last < first)
        {
            return result;
        }

        result.CoveredStartMs = lines[first].StartTime.TotalMilliseconds;
        result.CoveredCount = last - first + 1;

        for (var i = first; i <= last; i++)
        {
            var group = perLine[i] ?? new List<TtsStepResult>();
            if (i == first && i == last && group.Count == 0)
            {
                continue;
            }

            if (group.Count == 0)
            {
                // The session has no row for this line: it is not part of the review's state, and
                // publishing it would add a line the review does not show.
                continue;
            }

            var lineStart = lines[i].StartTime.TotalMilliseconds;
            var span = lines[i].EndTime.TotalMilliseconds - lineStart;
            for (var k = 0; k < group.Count; k++)
            {
                result.SessionLines.Add(new ReviewSplitPart(
                    lineStart + span * k / group.Count,
                    lineStart + span * (k + 1) / group.Count,
                    clean ? TtsTextCleaner.Clean(group[k].Text) : group[k].Text));
            }
        }

        Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
            $"TTS session: rows={rows.Count} matchedLines={claimed.Count(c => c)} coveredLines={result.CoveredCount} from={result.CoveredStartMs:F0} publishLines={result.SessionLines.Count}");

        return result;
    }
}
