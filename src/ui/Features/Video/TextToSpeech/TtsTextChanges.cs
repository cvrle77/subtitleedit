using System;
using System.Collections.Generic;
using System.Linq;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;
using Nikse.SubtitleEdit.Logic.Media;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// Turns a TTS session into text edits for the main window by comparing every row with the line the
/// main subtitle has at the same time codes. The review window can only diff against the text the
/// session started with, and an imported JSON *is* that starting text - so an imported session's
/// edits (or a session that never opened the review) used to reach the main window as nothing at all.
/// Rows whose cleaned text matches the main line are left alone, so tags, "(,,,)" pauses and CAPS
/// emphasis alone never overwrite a line.
/// </summary>
public static class TtsTextChanges
{
    public static List<ReviewTextChange> Build(
        IEnumerable<TtsStepResult> results,
        IEnumerable<Paragraph> mainLines,
        bool clean)
    {
        var changes = new List<ReviewTextChange>();
        var lines = mainLines.ToList();
        var rows = results.ToList();
        var matched = 0;
        var noMatch = 0;
        foreach (var item in rows)
        {
            var p = item.Paragraph;
            // Match by START only: the review trims/extends ends with the audio and a split moves
            // them, so an exact start+end match misses most rows of an imported session (a real one
            // had 60 of 116 unmatched for that reason). Same start plus the closest end wins when
            // two lines share a start.
            var original = lines
                .Where(o => Math.Abs(o.StartTime.TotalMilliseconds - p.StartTime.TotalMilliseconds) < 0.5)
                .OrderBy(o => Math.Abs(o.EndTime.TotalMilliseconds - p.EndTime.TotalMilliseconds))
                .FirstOrDefault();
            if (original == null)
            {
                noMatch++;
                var nearest = lines.Count == 0 ? null : lines.OrderBy(o => Math.Abs(o.StartTime.TotalMilliseconds - p.StartTime.TotalMilliseconds)).First();
                var delta = nearest == null ? 0 : nearest.StartTime.TotalMilliseconds - p.StartTime.TotalMilliseconds;
                Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
                    $"TTS changes: NO MATCH {p.StartTime.TotalMilliseconds:F0}-{p.EndTime.TotalMilliseconds:F0} nearestDelta={delta:F0}ms \"{Short(item.Text)}\"");
                continue; // no such line in the main subtitle (e.g. an imported session for another one)
            }

            matched++;
            var cleaned = TtsTextCleaner.Clean(item.Text);
            if (Normalize(cleaned) == Normalize(TtsTextCleaner.Clean(original.Text)))
            {
                continue; // same words - only TTS markup, spacing or line wrapping differs
            }

            changes.Add(new ReviewTextChange(
                original.StartTime.TotalMilliseconds,
                original.EndTime.TotalMilliseconds,
                clean ? cleaned : item.Text));
        }

        Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
            $"TTS changes: rows={rows.Count} matched={matched} differ={changes.Count} noMatch={noMatch} mainLines={lines.Count}" +
            (lines.Count > 0
                ? $" first={lines[0].StartTime.TotalMilliseconds:F0}-{lines[0].EndTime.TotalMilliseconds:F0}"
                : string.Empty));

        return changes;
    }

    private static string Short(string text)
    {
        var oneLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return oneLine.Length <= 50 ? oneLine : oneLine.Substring(0, 50) + "...";
    }

    // Same words compare equal no matter how the line was wrapped or spaced.
    private static string Normalize(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }
}
