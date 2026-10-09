using Avalonia.Media;

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
