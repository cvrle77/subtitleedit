using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;
using Nikse.SubtitleEdit.Logic.Media;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// Turns a TTS session into edits for the main window by matching every row with the line the main
/// subtitle has at the same START. The review trims and extends ends with the audio (and a split
/// moves them), so the end must not be part of the match - an exact start+end match used to drop
/// half of a real session. A row that has no start of its own but falls inside a main line belongs
/// to that line: two or more such rows mean the review split it, which the grid then has to follow.
/// Rows whose cleaned text matches the main line are left alone, so tags, "(,,,)" pauses, spacing
/// and CAPS emphasis alone never overwrite a line.
/// </summary>
public static class TtsTextChanges
{
    public sealed class Result
    {
        public List<ReviewTextChange> TextChanges { get; } = new();
        public List<ReviewSplit> Splits { get; } = new();
    }

    public static Result Build(IEnumerable<TtsStepResult> results, IEnumerable<Paragraph> mainLines, bool clean)
    {
        var result = new Result();
        var lines = mainLines.ToList();
        var rows = results.ToList();
        var matched = 0;
        var noMatch = 0;
        var perLine = new List<TtsStepResult>[lines.Count];

        foreach (var item in rows)
        {
            var p = item.Paragraph;
            var index = lines.FindIndex(o =>
                Math.Abs(o.StartTime.TotalMilliseconds - p.StartTime.TotalMilliseconds) < 0.5);
            if (index < 0)
            {
                // No start of its own: it may be the second half of a line the review split.
                index = lines.FindIndex(o =>
                    p.StartTime.TotalMilliseconds > o.StartTime.TotalMilliseconds + 0.5 &&
                    p.StartTime.TotalMilliseconds < o.EndTime.TotalMilliseconds - 0.5);
            }

            if (index < 0)
            {
                noMatch++;
                var nearest = lines.Count == 0 ? null : lines.OrderBy(o => Math.Abs(o.StartTime.TotalMilliseconds - p.StartTime.TotalMilliseconds)).First();
                var delta = nearest == null ? 0 : nearest.StartTime.TotalMilliseconds - p.StartTime.TotalMilliseconds;
                Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
                    $"TTS changes: NO MATCH {p.StartTime.TotalMilliseconds:F0}-{p.EndTime.TotalMilliseconds:F0} nearestDelta={delta:F0}ms \"{Short(item.Text)}\"");
                continue;
            }

            matched++;
            (perLine[index] ??= new List<TtsStepResult>()).Add(item);
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var group = perLine[i];
            if (group == null || group.Count == 0)
            {
                continue;
            }

            var ordered = group.OrderBy(g => g.Paragraph.StartTime.TotalMilliseconds).ToList();
            var line = lines[i];

            if (ordered.Count > 1)
            {
                var parts = new List<ReviewSplitPart>();
                for (var k = 0; k < ordered.Count; k++)
                {
                    var text = TextOf(ordered[k], clean);
                    if (parts.Count > 0 && Normalize(text) == Normalize(parts[^1].Text))
                    {
                        continue; // an empty second half adds nothing
                    }

                    parts.Add(new ReviewSplitPart(
                        k == 0 ? line.StartTime.TotalMilliseconds : ordered[k].Paragraph.StartTime.TotalMilliseconds,
                        k == ordered.Count - 1 ? line.EndTime.TotalMilliseconds : ordered[k].Paragraph.StartTime.TotalMilliseconds,
                        text));
                }

                if (parts.Count > 1)
                {
                    result.Splits.Add(new ReviewSplit(line.StartTime.TotalMilliseconds, parts));
                    continue;
                }
            }

            var only = ordered[0];
            var cleaned = TtsTextCleaner.Clean(only.Text);
            if (Normalize(cleaned) == Normalize(TtsTextCleaner.Clean(line.Text)))
            {
                continue; // same words - only TTS markup, spacing or line wrapping differs
            }

            result.TextChanges.Add(new ReviewTextChange(
                line.StartTime.TotalMilliseconds,
                line.EndTime.TotalMilliseconds,
                clean ? cleaned : only.Text));
        }

        Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
            $"TTS changes: rows={rows.Count} matched={matched} text={result.TextChanges.Count} splits={result.Splits.Count} noMatch={noMatch} mainLines={lines.Count}");

        return result;
    }

    private static string TextOf(TtsStepResult row, bool clean)
    {
        return clean ? TtsTextCleaner.Clean(row.Text) : row.Text;
    }

    // Same words compare equal no matter how the line was wrapped or spaced.
    private static string Normalize(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string Short(string text)
    {
        var oneLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return oneLine.Length <= 50 ? oneLine : oneLine.Substring(0, 50) + "...";
    }
}
