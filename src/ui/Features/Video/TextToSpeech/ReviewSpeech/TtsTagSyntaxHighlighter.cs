using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;

/// <summary>
/// Colors every "[...]" audio tag in the review edit box with its category's color (see
/// <see cref="TtsTagPalette"/>), so a tag that is used in the line is recognisable right in the
/// text, not only on the palette. Unknown tags fall back to the Pauses gray.
/// </summary>
public sealed class TtsTagSyntaxHighlighter : ISourceSyntaxHighlighter
{
    public void HighlightLine(string lineText, SourceSyntaxLineStyler styler)
    {
        var i = 0;
        while (i < lineText.Length)
        {
            if (lineText[i] != '[')
            {
                i++;
                continue;
            }

            var close = lineText.IndexOf(']', i + 1);
            if (close < 0)
            {
                break;
            }

            var bare = lineText.Substring(i + 1, close - i - 1).Trim();
            styler.Apply(i, close - i + 1, TtsTagPalette.ColorForTag(bare), bold: true);
            i = close + 1;
        }
    }
}
