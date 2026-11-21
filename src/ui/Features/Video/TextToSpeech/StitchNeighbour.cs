using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech;

/// <summary>
/// The words a neighbouring line contributes to a request's stitching context: the tail of the
/// previous line or the head of the next one, capped the same way the generate path caps it. Empty
/// slots (excluded or blank lines) are skipped so the nearest spoken neighbour is used.
/// </summary>
public static class StitchNeighbour
{
    public const int MaxChars = 100;

    public static string Text(IReadOnlyList<string> texts, int index, int direction, bool fromEnd)
    {
        if (index < 0)
        {
            return string.Empty;
        }

        for (var i = index + direction; i >= 0 && i < texts.Count; i += direction)
        {
            var text = (texts[i] ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                continue;
            }

            return text.Length <= MaxChars ? text : fromEnd ? text[^MaxChars..] : text[..MaxChars];
        }

        return string.Empty;
    }
}
