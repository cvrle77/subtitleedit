using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Logic.Media;

/// <summary>
/// Turns an expressive TTS line (audio tags, CAPS emphasis, pause markers) into a clean
/// subtitle line. The TTS engine keeps the original text; only the copy that reaches the main
/// subtitle goes through here, so the SRT stays free of tag/emphasis noise.
/// </summary>
public static class TtsTextCleaner
{
    // Audio tags: [..], (..) and {..} - a bracket group whose content is a delivery/emotion
    // instruction. Multi-word, e.g. "[warm, conversational]".
    private static readonly Regex TagRegex = new Regex(
        @"[\[\(\{][^\[\]\(\)\{\}]*[\]\)\}]",
        RegexOptions.Compiled);

    // A stand-alone comma-pause: ",,," or longer runs. A single comma is normal punctuation.
    private static readonly Regex CommaPauseRegex = new Regex(
        @"\s*,{2,}\s*",
        RegexOptions.Compiled);

    // A word written entirely in CAPS (>= 2 letters), used as emphasis on the TTS side. The run may
    // not be followed by an apostrophe, so the caps part of a contraction ("DON'T") is left alone.
    private static readonly Regex CapsWordRegex = new Regex(
        @"\b[\p{Lu}]{2,}\b(?!['’])",
        RegexOptions.Compiled);

    // All-caps runs that are acronyms, not emphasis, so they are left as-is.
    private static readonly HashSet<string> Acronyms = new(StringComparer.Ordinal)
    {
        "USA", "US", "UK", "EU", "UN", "FBI", "CIA", "NASA", "NATO", "TV", "DVD", "CD",
        "USB", "GPS", "PDF", "HTML", "CPU", "RAM", "SMS", "DNA", "SUV", "BMW", "BBQ", "HD",
    };

    public static string Clean(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var text = input;

        // 1. Remove audio tags: [..] / (..) / {..}, including the surrounding whitespace.
        text = TagRegex.Replace(text, string.Empty);

        // 2. Remove stand-alone comma pauses (,,, ...). Keep single commas and "..." dots.
        text = CommaPauseRegex.Replace(text, " ");

        // 3. Normalize CAPS emphasis words to normal case, keeping a capital only when the word
        //    starts a sentence (or follows . ! ? ...) - so "BAKE the cake" -> "Bake the cake"
        //    and "gently FOLD" -> "gently fold".
        text = CapsWordRegex.Replace(text, m => ToSentenceCase(text, m));

        // 4. Collapse the gaps the removals may have left, but keep meaningful line structure.
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\s+([,.!?;:])", "$1");
        text = text.Trim();

        return text;
    }

    private static string ToSentenceCase(string full, Match m)
    {
        var word = m.Value;
        if (Acronyms.Contains(word))
        {
            return word;
        }

        var lower = word.ToLowerInvariant();
        return IsSentenceStart(full, m.Index) ? Capitalize(lower) : lower;
    }

    private static bool IsSentenceStart(string text, int index)
    {
        for (var i = index - 1; i >= 0; i--)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            // Previous non-space char: a terminal punctuation (or start of string) means a
            // sentence boundary, so this word gets the capital.
            return c is '.' or '!' or '?' or '…' or '\n' or '"';
        }

        return true; // start of the string
    }

    private static string Capitalize(string s)
    {
        if (s.Length == 0)
        {
            return s;
        }

        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
