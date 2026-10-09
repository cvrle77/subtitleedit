using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;

/// <summary>One palette group: a header, its tags, and the color that identifies the group.</summary>
public sealed record TtsTagCategory(string Header, string[] Tags, Color Color);

/// <summary>
/// The audio-tag palette of the review window and the color that each tag's category paints with.
/// The header label, the active palette chip and the tag's own text in the edit box all use the
/// same color, so a used tag is recognisable at a glance. "Pauses" is last (just above "Remove all
/// tags") because a pause is inserted mid-text, unlike the delivery tags that lead the line.
/// </summary>
public static class TtsTagPalette
{
    private static Color Rgb(uint hex) =>
        Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);

    public static readonly TtsTagCategory[] Categories =
    [
        new("Intro", ["[happy]", "[excited]", "[welcoming]", "[cheerful]", "[warmly]", "[friendly]", "[enthusiastic]", "[upbeat]", "[delighted]"], Rgb(0x1565C0)),
        new("Steps", ["[thoughtful]", "[measured]", "[calm]", "[matter-of-factly]", "[informative]", "[patiently]", "[clearly]", "[precise]", "[methodical]", "[detailed]"], Rgb(0x00695C)),
        new("Advice", ["[reassuring]", "[encouraging]", "[gently]", "[kindly]", "[helpfully]", "[supportive]", "[caring]", "[attentive]"], Rgb(0x2E7D32)),
        new("Important", ["[serious]", "[firmly]", "[with emphasis]", "[earnestly]", "[confident]", "[determined]", "[stern]"], Rgb(0xC62828)),
        new("Fun", ["[chuckles]", "[laughs]", "[playful]", "[amused]", "[light-hearted]", "[cheeky]", "[impish]", "[jesting]"], Rgb(0x6A1B9A)),
        new("Emphasis", ["[emphatic]", "[slowly]", "[quickly]", "[drawn out]", "[loudly]"], Rgb(0x283593)),
        new("ASMR", ["[whispers]", "[softly]", "[breathy]", "[gentle]", "[close]", "[warm]"], Rgb(0x00838F)),
        new("Reaction", ["[surprised]", "[impressed]", "[curious]", "[relieved]", "[proud]", "[thinking]", "[tasting]"], Rgb(0xE65100)),
        new("Ending", ["[satisfied]", "[pleased]", "[proudly]", "[content]", "[gratified]", "[triumphant]", "[concluding]"], Rgb(0x4E342E)),
        new("Accent", ["[American accent]", "[British accent]", "[strong American accent]", "[South African accent]", "[Serbian accent]", "[warm, conversational]", "[narrator]"], Rgb(0xAD1457)),
        new("Pauses", ["[short pause]", "[pause]", "[long pause]", "[sighs]", "[exhales]", "[inhales]", "[clears throat]", "[breath]"], Rgb(0x455A64)),
    ];

    /// <summary>The color of the category a bare tag (no brackets) belongs to, or the Pauses gray.</summary>
    public static Color ColorForTag(string bareTag)
    {
        foreach (var category in Categories)
        {
            foreach (var tag in category.Tags)
            {
                if (string.Equals(tag.Trim().TrimStart('[').TrimEnd(']').Trim(), bareTag, System.StringComparison.OrdinalIgnoreCase))
                {
                    return category.Color;
                }
            }
        }

        return Categories[^1].Color;
    }

    // Multi-word palette tags (e.g. "drawn out", "short pause", "American accent"), derived from the
    // palette so a new multi-word tag is recognised without a second list to keep in sync. Longest
    // first when matching, so a multi-word tag wins over its parts.
    public static readonly HashSet<string> MultiWordTags = BuildMultiWordTags();

    private static HashSet<string> BuildMultiWordTags()
    {
        var set = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var category in Categories)
        {
            foreach (var tag in category.Tags)
            {
                var bare = tag.Trim().TrimStart('[').TrimEnd(']').Trim();
                if (bare.Contains(' '))
                {
                    set.Add(bare);
                }
            }
        }

        return set;
    }

    /// <summary>
    /// Splits the inside of a "[...]" group into tag tokens with their character offsets (relative
    /// to <paramref name="inner"/>). A known multi-word tag stays one token; anything else is one
    /// token per word.
    /// </summary>
    public static List<(int Start, int Length, string Tag)> TokenizeGroup(string inner)
    {
        var result = new List<(int Start, int Length, string Tag)>();
        if (string.IsNullOrWhiteSpace(inner))
        {
            return result;
        }

        var words = new List<(int Start, string Word)>();
        var i = 0;
        while (i < inner.Length)
        {
            while (i < inner.Length && inner[i] == ' ')
            {
                i++;
            }

            if (i >= inner.Length)
            {
                break;
            }

            var start = i;
            while (i < inner.Length && inner[i] != ' ')
            {
                i++;
            }

            words.Add((start, inner.Substring(start, i - start)));
        }

        var w = 0;
        while (w < words.Count)
        {
            var matched = false;
            for (var len = System.Math.Min(3, words.Count - w); len >= 2; len--)
            {
                var candidate = string.Join(" ", words.Skip(w).Take(len).Select(x => x.Word));
                if (MultiWordTags.Contains(candidate))
                {
                    var start = words[w].Start;
                    var end = words[w + len - 1].Start + words[w + len - 1].Word.Length;
                    result.Add((start, end - start, candidate));
                    w += len;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                result.Add((words[w].Start, words[w].Word.Length, words[w].Word));
                w++;
            }
        }

        return result;
    }

    // Light tint of a category color, used as the active chip's background so the label stays
    // readable on both themes.
    public static Color Pastel(Color color)
    {
        return Color.FromRgb(
            (byte)(color.R + (255 - color.R) * 0.78),
            (byte)(color.G + (255 - color.G) * 0.78),
            (byte)(color.B + (255 - color.B) * 0.78));
    }

    // Dark text for a pastel chip background: the same hue, darkened.
    public static Color DarkText(Color color)
    {
        return Color.FromRgb(
            (byte)(color.R * 0.45),
            (byte)(color.G * 0.45),
            (byte)(color.B * 0.45));
    }
}
