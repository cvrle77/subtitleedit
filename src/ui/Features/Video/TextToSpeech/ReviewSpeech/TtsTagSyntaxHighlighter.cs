using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;

/// <summary>
/// Colors every "[...]" audio tag in the review edit box with its category's color (see
/// <see cref="TtsTagPalette"/>), so a used tag is recognisable right in the text. A group holding
/// several tags ("[warmly slowly]") is colored per tag - each tag its own category color - not as
/// one blob. Unknown tags fall back to the Pauses gray.
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

            var inner = lineText.Substring(i + 1, close - i - 1);
            var tokens = TtsTagPalette.TokenizeGroup(inner);
            if (tokens.Count == 0)
            {
                styler.Apply(i, close - i + 1, TtsTagPalette.ColorForTag(string.Empty), bold: true);
            }
            else
            {
                // The opening bracket takes the first tag's color and the closing one the last
                // tag's, so a multi-tag group is colored tag by tag.
                styler.Apply(i, 1, TtsTagPalette.ColorForTag(tokens[0].Tag), bold: true);
                foreach (var token in tokens)
                {
                    styler.Apply(i + 1 + token.Start, token.Length, TtsTagPalette.ColorForTag(token.Tag), bold: true);
                }

                styler.Apply(close, 1, TtsTagPalette.ColorForTag(tokens[^1].Tag), bold: true);
            }

            i = close + 1;
        }
    }
}
