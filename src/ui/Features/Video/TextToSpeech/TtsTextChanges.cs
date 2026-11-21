using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Media;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// The TTS review's state, ready to be written over the main subtitle. The review is the authority:
/// every row it has (text, its edits, its splits and merges) is published, and the main lines those
/// rows cover are replaced by exactly those rows. Diffing row-by-row against the main text was the
/// wrong model - it needed the time codes to line up and lost anything the review had moved.
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
        public int NoMatch { get; set; }
    }

    // A cue nudged by the review keeps its line as long as its start stays inside this window.
    private const double NearestStartToleranceMs = 1500;

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

        foreach (var item in rows)
        {
            var index = FindLine(lines, claimed, item.Paragraph.StartTime.TotalMilliseconds);
            if (index < 0)
            {
                result.NoMatch++;
                Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
                    $"TTS session: NO LINE for {item.Paragraph.StartTime.TotalMilliseconds:F0}-{item.Paragraph.EndTime.TotalMilliseconds:F0} \"{Short(item.Text)}\"");
                continue; // a row the main subtitle has no place for (e.g. a session for another one)
            }

            claimed[index] = true;
            (perLine[index] ??= new List<TtsStepResult>()).Add(item);
        }

        var first = Array.IndexOf(claimed, true);
        var last = Array.LastIndexOf(claimed, true);
        if (first < 0 || last < first)
        {
            return result;
        }

        result.CoveredStartMs = lines[first].StartTime.TotalMilliseconds;
        result.CoveredCount = last - first + 1;

        for (var i = first; i <= last; i++)
        {
            var group = perLine[i];
            if (group == null)
            {
                // A main line the session has no row for is not part of the review's state: the
                // session is what gets published, one segment per row (155 rows in, 155 lines out).
                continue;
            }

            var ordered = group.OrderBy(g => g.Paragraph.StartTime.TotalMilliseconds).ToList();
            var lineStart = lines[i].StartTime.TotalMilliseconds;
            var span = lines[i].EndTime.TotalMilliseconds - lineStart;
            for (var k = 0; k < ordered.Count; k++)
            {
                // Text and the number of segments come from the review; the time codes stay the main
                // line's own. The review trims ends with the audio and moves cues around, and letting
                // those times through put cues on top of each other and pushed them outside their
                // line. A line the review split is divided evenly across its own span.
                var start = lineStart + span * k / ordered.Count;
                var end = lineStart + span * (k + 1) / ordered.Count;

                result.SessionLines.Add(new ReviewSplitPart(
                    start,
                    end,
                    clean ? TtsTextCleaner.Clean(ordered[k].Text) : ordered[k].Text));
            }
        }

        Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
            $"TTS session: rows={rows.Count} coveredLines={result.CoveredCount} from={result.CoveredStartMs:F0} publishLines={result.SessionLines.Count} noLine={result.NoMatch}");

        return result;
    }

    // The line a row belongs to: its own start, the line that contains a split half's start, or the
    // nearest free line within the nudge window (a review trim moves a start by a fraction of a
    // second, and an exact match used to throw those rows away).
    private static int FindLine(IReadOnlyList<Paragraph> lines, bool[] claimed, double startMs)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (Math.Abs(lines[i].StartTime.TotalMilliseconds - startMs) < 0.5)
            {
                return i;
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (startMs > lines[i].StartTime.TotalMilliseconds + 0.5 &&
                startMs < lines[i].EndTime.TotalMilliseconds - 0.5)
            {
                return i;
            }
        }

        var best = -1;
        var bestDelta = double.MaxValue;
        for (var i = 0; i < lines.Count; i++)
        {
            if (claimed[i])
            {
                continue;
            }

            var delta = Math.Abs(lines[i].StartTime.TotalMilliseconds - startMs);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }

        if (best >= 0 && bestDelta <= NearestStartToleranceMs)
        {
            Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
                $"TTS session: near start {startMs:F0} -> line {lines[best].StartTime.TotalMilliseconds:F0} (delta={bestDelta:F0}ms)");
            return best;
        }

        return -1;
    }

    private static string Short(string text)
    {
        var oneLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return oneLine.Length <= 50 ? oneLine : oneLine.Substring(0, 50) + "...";
    }
}
