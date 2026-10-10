using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Logic.Media;

/// <summary>
/// One word with its spoken start/end, parsed from an engine's word-level output.
/// </summary>
public sealed record SmartBreakWord(string Word, double Start, double End);

/// <summary>
/// Rebreaks a speech-to-text result into subtitle lines/cues using the engine's word-level
/// timings and Silero VAD, instead of the engine's own (often paragraph-sized) segmentation.
///
/// Rules (Netflix Serbian style, generalised): break after punctuation, before conjunctions and
/// prepositions; never end a line on a preposition/clitic/conjunction; keep abbreviations and
/// number+unit together; move a lone word between a comma and the break onto the comma; split a
/// sentence once after its first comma when the first clause is longer than four words; keep a cue
/// up to two lines; cut cues at real pauses and end them at the real end of speech (Silero).
///
/// The word list is all this needs, so it works for any engine that emits word timings
/// (WhisperX, faster-whisper / Purfview, whisper.cpp, whisper-ctranslate2, ...).
/// </summary>
public static class SmartBreak
{
    public const int MaxLen = 46;
    public const int MaxLines = 2;
    public const int MinLine = 13;         // avoid a one-word orphan line when a longer break exists
    public const double Gap = 2.0;          // a real pause that starts a new cue (short pauses stay in)
    public const double Tail = 0.0;         // extra hold after the last spoken sound (0 = cut at voice)
    public const double Lead = 0.10;        // fallback start shift when Silero has no run
    public const double MaxWord = 0.85;     // a word span longer than this absorbed trailing silence
    public const double MaxExt = 1.5;       // never extend a cue more than this past its last word
    public const double MinCue = 0.8;
    public const double PauseAtComma = 1.5;  // a pause after a comma ends the cue (enumeration with a gap)

    private static readonly HashSet<string> Strong = new(StringComparer.OrdinalIgnoreCase)
    {
        "već", "vec", "jer", "ali", "dok", "kako", "kada", "kad", "ako", "mada", "premda",
        "nego", "pošto", "posto", "čim", "cim", "iako", "zato", "dakle", "međutim", "medjutim",
        "budući", "buduci", "no", "pak", "što", "sto", "gde", "gdje", "koji", "koja", "koje",
        "čiji", "ciji", "odnosno", "naime", "stoga", "takođe", "takodje", "zapravo",
    };

    private static readonly HashSet<string> Weak = new(StringComparer.OrdinalIgnoreCase)
    {
        "i", "a", "pa", "ili", "te", "niti", "ni",
    };

    private static readonly HashSet<string> Clitics = new(StringComparer.OrdinalIgnoreCase)
    {
        "bi", "bih", "bismo", "biste", "je", "su", "sam", "si", "smo", "ste",
        "ću", "cu", "ćeš", "ces", "će", "ce", "se", "li", "ga", "ih", "im",
        "mu", "joj", "me", "te", "nas", "vas", "nj", "nju", "ne",
    };

    private static readonly HashSet<string> Prepositions = new(StringComparer.OrdinalIgnoreCase)
    {
        "u", "na", "o", "ob", "po", "do", "od", "iz", "sa", "s", "bez", "za",
        "pod", "nad", "pred", "preko", "kroz", "uz", "niz", "kod", "ka", "prema",
        "protiv", "osim", "umesto", "mesto", "pomoću", "pomocu", "posredstvom",
        "putem", "tokom", "zbog", "radi", "između", "izmedju", "među", "medju",
        "iznad", "ispod", "blizu", "oko", "pored", "pokraj", "duž", "duz",
        "poput", "nalik", "pre", "pri", "kraj", "vrh", "sred", "kao",
    };

    private static readonly HashSet<string> Abbrev = new(StringComparer.OrdinalIgnoreCase)
    {
        "bl", "bl.", "dipl", "dipl.", "dr", "dr.", "g", "g.", "gđa", "gđica",
        "gđice", "gđici", "gđicu", "gđu", "god.", "gosp", "gosp.", "ing.", "mr.",
        "prof", "prof.", "sv", "sv.", "vlč.", "vlc.", "gr", "gr.", "br", "br.", "itd", "itd.",
    };

    private static readonly HashSet<string> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        "ml", "l", "g", "kg", "mg", "dkg", "cm", "mm", "m", "km", "min", "s",
        "°c", "c", "°", "%", "kašika", "kašike", "kašiku", "kašičica", "kašičice",
        "kašičicu", "gram", "grama", "grami", "mililitar", "mililitara", "militara",
        "mililitre", "litra", "litara", "litre", "decilitar", "decilitara", "dcl", "dl",
        "sekundi", "sekunde", "minuta", "minute", "sati", "sata",
    };

    private static readonly HashSet<string> NoBreakAfter = BuildNoBreakAfter();

    private static HashSet<string> BuildNoBreakAfter()
    {
        var set = new HashSet<string>(Prepositions, StringComparer.OrdinalIgnoreCase);
        set.UnionWith(Clitics);
        set.UnionWith(Strong);
        set.UnionWith(Weak);
        set.UnionWith(Abbrev);
        set.Add("da");
        set.Add("li");
        return set;
    }

    // Words that must not be the last word of a cue: conjunctions, prepositions, clitics. The pause
    // that ends a block often falls right after one of these ("... brasna, | i sad ..."), which
    // leaves it dangling on the previous cue; the cue post-pass moves it to the next cue. This is
    // NoBreakAfter without the abbreviations, so a cue that ends on "g." is left alone.
    private static readonly HashSet<string> DanglingEnd = BuildDanglingEnd();

    private static HashSet<string> BuildDanglingEnd()
    {
        var set = new HashSet<string>(Prepositions, StringComparer.OrdinalIgnoreCase);
        set.UnionWith(Clitics);
        set.UnionWith(Strong);
        set.UnionWith(Weak);
        set.Add("da");
        set.Add("li");
        return set;
    }

    private static bool IsDanglingEnd(string word)
    {
        var bare = Bare(word);
        return bare.Length > 0 && DanglingEnd.Contains(bare);
    }

    private static readonly Regex NumRe = new(@"^[0-9]+([.,][0-9]+)?$", RegexOptions.Compiled);

    private static string Bare(string w) => new string(w.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    // Folds a word for the diacritic-free phrase tables (ClauseStartPhrases): like Bare but with the
    // Serbian diacritics replaced (č/ć->c, š->s, ž->z, đ->dj), so "konačno" matches the stored
    // "konacno". Bare keeps the diacritics (the other sets store both spellings).
    private static string Fold(string w)
    {
        var sb = new System.Text.StringBuilder(w.Length);
        foreach (var c in w.ToLowerInvariant())
        {
            switch (c)
            {
                case 'č' or 'ć':
                    sb.Append('c');
                    break;
                case 'š':
                    sb.Append('s');
                    break;
                case 'ž':
                    sb.Append('z');
                    break;
                case 'đ':
                    sb.Append("dj");
                    break;
                default:
                    if (char.IsLetterOrDigit(c))
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    private static string Rstrip(string w) => w.TrimEnd('„', '"', '»', ')', ']');

    private static char LastChar(string w)
    {
        var s = Rstrip(w);
        return s.Length > 0 ? s[^1] : '\0';
    }

    private static double SoundEnd(SmartBreakWord w) => Math.Min(w.End, w.Start + MaxWord);

    /// <summary>
    /// True when the word ends a sentence. A trailing "." after an abbreviation or unit (e.g. "gr.",
    /// "g.", "ml.") is not a sentence end, so the AI's "800 gr." does not split the phrase.
    /// </summary>
    private static bool EndsSentence(string word)
    {
        var s = Rstrip(word);
        if (s.Length == 0 || ".!?…".IndexOf(s[^1]) < 0)
        {
            return false;
        }

        var bare = Bare(s);
        return bare.Length > 1 && !Abbrev.Contains(bare);
    }

    private static bool LineEndsSentence(string line)
    {
        var s = line.TrimEnd('„', '"', '»', ')', ']').TrimEnd();
        if (s.Length == 0 || ".!?…".IndexOf(s[^1]) < 0)
        {
            return false;
        }

        var space = s.LastIndexOf(' ');
        var lastToken = space >= 0 ? s.Substring(space + 1) : s;
        var bare = Bare(lastToken);
        return bare.Length > 1 && !Abbrev.Contains(bare);
    }

    /// <summary>
    /// Where the word's sound really ends: the chained Silero speech run it starts in. Runs are
    /// chained through gaps shorter than 0.15 s so a Silero split inside a word does not look like
    /// a pause, while a real pause stops the chain. Also stops a stretched word end (WhisperX align
    /// can give a word a multi-second end over silence) from hiding a pause.
    /// </summary>
    private static double EffEnd(IReadOnlyList<(double Start, double End)> runs, SmartBreakWord w)
    {
        double? cur = null;
        foreach (var (a, b) in runs)
        {
            if (cur == null)
            {
                if (a - 0.08 <= w.Start && w.Start <= b + 0.08)
                {
                    cur = b;
                }
            }
            else if (a <= cur.Value + 0.15)
            {
                if (b > cur.Value)
                {
                    cur = b;
                }
            }
            else
            {
                break;
            }
        }

        return cur ?? SoundEnd(w);
    }

    private static int Nat(IReadOnlyList<SmartBreakWord> words, int b)
    {
        if (b >= words.Count)
        {
            return 100;
        }

        var prev = Rstrip(words[b - 1].Word);
        var nxt = Bare(words[b].Word);
        var s = 0;
        if (prev.Length > 0 && ".!?…".IndexOf(prev[^1]) >= 0)
        {
            s += 100;
        }
        else if (prev.Length > 0 && ",;:".IndexOf(prev[^1]) >= 0)
        {
            s += 60;
        }

        if (Strong.Contains(nxt))
        {
            s += 70;
        }
        else if (Weak.Contains(nxt))
        {
            s += 60;
        }

        return s;
    }

    private static int LLen(IReadOnlyList<SmartBreakWord> words, int i, int b)
    {
        var len = b - i - 1;
        for (var k = i; k < b; k++)
        {
            len += words[k].Word.Length;
        }

        return len;
    }

    private static bool Forbidden(IReadOnlyList<SmartBreakWord> words, int b)
    {
        if (b >= words.Count)
        {
            return false;
        }

        var nxt = Bare(words[b].Word);
        var prev = Bare(words[b - 1].Word);
        if (Clitics.Contains(nxt) || NoBreakAfter.Contains(prev))
        {
            return true;
        }

        if (NumRe.IsMatch(prev) && Units.Contains(nxt))
        {
            return true;
        }

        return false;
    }

    private static List<List<SmartBreakWord>> BuildLines(IReadOnlyList<SmartBreakWord> words)
    {
        var lines = new List<List<SmartBreakWord>>();
        int i = 0, n = words.Count;
        while (i < n)
        {
            var rem = LLen(words, i, n);
            if (rem <= MaxLen)
            {
                lines.Add(words.Skip(i).ToList());
                break;
            }

            var k = (rem + MaxLen - 1) / MaxLen;
            var target = (double)rem / k;

            var j = i;
            while (j < n && LLen(words, i, j + 1) <= MaxLen)
            {
                j++;
            }

            if (j <= i)
            {
                // A single word longer than MaxLen (a URL, a long compound): take it whole so i
                // advances - otherwise no break candidate exists and this loop never ends.
                j = i + 1;
            }

            var cands = new List<(int B, int Len)>();
            for (var b = i + 1; b <= j; b++)
            {
                if (!Forbidden(words, b))
                {
                    cands.Add((b, LLen(words, i, b)));
                }
            }

            if (cands.Count == 0)
            {
                // No allowed break in range: take the largest one that still does not leave a
                // preposition/clitic dangling at the line end.
                var b = j;
                while (b > i + 1 && Forbidden(words, b))
                {
                    b--;
                }

                cands.Add((b, LLen(words, i, b)));
            }
            else
            {
                // Prefer a break that fills the line: a 1-word orphan ("Mekano,") only wins when
                // nothing else is available.
                var longer = cands.Where(c => c.Len >= MinLine).ToList();
                if (longer.Count > 0)
                {
                    cands = longer;
                }
            }

            var end = cands
                .OrderByDescending(t => -Math.Abs(t.Len - target) + Nat(words, t.B) * 0.15)
                .ThenByDescending(t => t.Len)
                .First().B;

            // if the current line does not end on punctuation and the next word is a single word
            // followed by a comma, pull that word+comma onto this line, then break after it
            if (end < n && LLen(words, i, end + 1) <= MaxLen)
            {
                var last = LastChar(words[end - 1].Word);
                var nxtc = LastChar(words[end].Word);
                if ((last == '\0' || ",;:.!?…".IndexOf(last) < 0) && nxtc is ',' or ';' or ':')
                {
                    end++;
                }
            }

            lines.Add(words.Skip(i).Take(end - i).ToList());

            // Diagnostic: a line must not end on a conjunction/particle.
            if (end - 1 >= 0 && Weak.Contains(Bare(words[end - 1].Word)))
            {
                Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
                    $"DIAG line ends on conjunction: \"{string.Join(" ", words.Skip(i).Take(end - i).Select(x => x.Word.Trim()))}\" | next=\"{words[Math.Min(end, words.Count - 1)].Word.Trim()}\"");
            }

            i = end;
        }

        // The balanced split can pick a short first line (a comma) and force an extra line. Never
        // exceed the minimum possible: ceil(chars / MaxLen). If it did, refill greedily.
        var total = LLen(words, 0, words.Count);
        var minLines = Math.Max(1, (total + MaxLen - 1) / MaxLen);
        if (lines.Count > minLines)
        {
            lines = GreedyLines(words);

            // Greedy can still be forced over the budget by the "no dangling clitic" rule (a run of
            // short words like "... bajadera je da vam ..." has no allowed break inside the first
            // line). Exceeding the line budget is worse than a clitic at the line end, so refill
            // ignoring Forbidden as a last resort - otherwise SplitIntoCues has to trim the cue and
            // the split lands mid-phrase instead of on the comma.
            if (lines.Count > minLines)
            {
                lines = RelaxedGreedyLines(words);
            }
        }

        return lines;
    }

    /// <summary>
    /// Greedy fill that ignores <see cref="Forbidden"/>: each line takes as many words as fit under
    /// <see cref="MaxLen"/>. Used only when the forbidden-break rules would force more lines than
    /// the text actually needs.
    /// </summary>
    private static List<List<SmartBreakWord>> RelaxedGreedyLines(IReadOnlyList<SmartBreakWord> words)
    {
        var lines = new List<List<SmartBreakWord>>();
        int i = 0, n = words.Count;
        while (i < n)
        {
            var j = i;
            while (j < n && LLen(words, i, j + 1) <= MaxLen)
            {
                j++;
            }

            if (j <= i)
            {
                j = i + 1;
            }

            lines.Add(words.Skip(i).Take(j - i).ToList());
            i = j;
        }

        return lines;
    }

    private static List<List<SmartBreakWord>> GreedyLines(IReadOnlyList<SmartBreakWord> words)
    {
        var lines = new List<List<SmartBreakWord>>();
        int i = 0, n = words.Count;
        while (i < n)
        {
            var j = i;
            while (j < n && LLen(words, i, j + 1) <= MaxLen)
            {
                j++;
            }

            if (j <= i)
            {
                j = i + 1;
            }

            var end = j;
            for (var b = j; b > i + 1; b--)
            {
                if (!Forbidden(words, b))
                {
                    end = b;
                    break;
                }
            }

            lines.Add(words.Skip(i).Take(end - i).ToList());
            i = end;
        }

        return lines;
    }

    private static string Text(IEnumerable<SmartBreakWord> ws) => string.Join(" ", ws.Select(w => w.Word.Trim()));

    private static bool IsClauseEnd(string w)
    {
        var c = LastChar(w);
        if (c is ',' or ';' or ':' or '!' or '?' or '…')
        {
            return true;
        }

        // "." counts only for a real sentence end, not after an abbreviation/unit ("gr.", "ml.")
        return c == '.' && EndsSentence(w);
    }

    // Phrases that, right after "i/a/pa/ili/te", start a new clause. Stored without diacritics
    // (Bare() strips them). Matched longest-first, up to 4 words.
    private static readonly HashSet<string> ClauseStartPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        // 1. time / moment
        "sada", "sad", "onda", "tada", "kada", "kad", "zatim", "potom", "posle", "kasnije", "ranije", "najpre",
        "prvo", "konacno", "naposletku", "na kraju", "u medjuvremenu", "do tada", "od tada",
        "do sada", "od sada", "pre toga", "posle toga", "nakon toga", "pre svega", "tek tada",
        "tek sada", "tek onda", "upravo tada", "upravo sada", "upravo onda", "vec tada", "vec sada",
        "vec onda", "jos tada", "jos sada", "jos onda", "ovog puta", "ovoga puta", "sledeci put",
        "svaki put", "svakog puta", "istovremeno", "u pocetku", "na pocetku", "na samom pocetku",
        "na kraju krajeva", "na kraju svega", "na kraju svega toga", "od tog trenutka", "od tog dana",
        "od tog casa", "od tog trenutka nadalje", "od tada nadalje", "sve do tada", "sve do sada",
        "sve do tog trenutka",
        // 2. repetition / continuation
        "opet", "ponovo", "iznova", "nanovo", "jos jednom", "ponovo iz pocetka", "opet iz pocetka",
        "dalje", "nadalje", "dalje na isti nacin", "dalje isto", "dalje tako", "jos", "jos uvek",
        "jos uvek tako", "jos uvek isto", "ponovo isto", "opet isto", "opet tako", "iznova isto",
        "iznova tako", "svakoga puta", "svaki put iznova", "svaki put ponovo", "ponovo sve isto",
        "opet sve isto", "opet iznova",
        // 3. cause / consequence
        "zato", "stoga", "zbog toga", "usled toga", "usled svega toga", "zbog svega toga", "upravo zato",
        "bas zato", "samo zato", "samim tim", "prema tome", "shodno tome", "s obzirom na to", "otuda",
        "iz toga", "iz svega toga", "time", "tim povodom", "kao rezultat toga", "kao posledica toga",
        "posledicno", "dakle", "tako", "tako je", "tako onda", "upravo zbog toga", "upravo zbog svega toga",
        // 4. contrast
        "ipak", "medjutim", "ipak tada", "ipak sada", "ipak onda", "naprotiv", "obrnuto", "zapravo",
        "u stvari", "istina", "doduse", "svejedno", "bez obzira na to", "uprkos tome", "nasuprot tome",
        "umesto toga", "za razliku od toga", "s druge strane", "sa druge strane", "po svemu sudeci",
        "pored toga", "pored svega", "pored svega toga", "uprkos svemu", "uprkos svemu tome",
        // 5. adding information
        "pritom", "pri tome", "uz to", "uz sve to", "osim toga", "povrh toga", "preko toga", "dodatno",
        "takodje", "isto tako", "isto tako sada", "isto tako tada", "isto tako onda", "cak", "stavise",
        "vise od toga", "jos vise", "narocito", "posebno", "povrh svega", "iznad svega", "najzad",
        // 6. explanation / clarification
        "naime", "drugim recima", "preciznije", "tacnije", "konkretnije", "jednostavno",
        "jednostavno receno", "ukratko", "da budemo precizni", "da budem precizan", "da budemo iskreni",
        "da budem iskren", "realno", "prakticno", "sustinski", "u sustini", "u osnovi", "uglavnom",
        "generalno", "nacelno",
        // 7. speaker's stance / evaluation
        "naravno", "svakako", "sigurno", "nesumnjivo", "verovatno", "moguce", "mozda", "ocigledno",
        "jasno", "zaista", "doista", "bez sumnje", "izgleda", "izgleda da", "cini se", "cini se da",
        "srecom", "na srecu", "nazalost", "na zalost", "na moje iznenadenje", "na njegovo iznenadenje",
        "na nase iznenadenje",
        // 8. condition / circumstance
        "u tom slucaju", "u takvom slucaju", "u svakom slucaju", "u svakom slucaju tada", "u suprotnom",
        "u protivnom", "pod tim uslovom", "pod tim okolnostima", "u tim okolnostima", "bez obzira na sve",
        "bez obzira na sve to", "kada je to potrebno", "kada dodje vreme", "kada za to dodje vreme",
        // 9. combinations with sada/onda/tada/...
        "sada kada", "sada kad", "sada ako", "sada dok", "sada cim", "sada posto", "sada nakon sto",
        "sada pre nego sto",
        "onda kada", "onda kad", "onda ako", "onda dok", "onda cim", "onda posto", "onda nakon sto",
        "onda pre nego sto",
        "tada kada", "tada kad", "tada ako", "tada dok", "tada cim", "tada posto", "tada nakon sto",
        "tada pre nego sto",
        "zatim kada", "zatim kad", "zatim ako", "zatim dok", "zatim cim",
        "potom kada", "potom kad", "potom ako", "potom dok", "potom cim",
        "tek tada kada", "tek tada kad", "tek tada ako", "tek tada cim",
        "tek onda kada", "tek onda kad", "tek onda ako", "tek onda cim",
        "upravo tada kada", "upravo tada kad", "upravo tada ako",
        "upravo sada kada", "upravo sada kad", "upravo sada ako",
        // svaki/sve/svi group (re-added: "i svaki deo testa...")
        "svaki", "svaka", "svako", "svake", "svakog", "svakom", "svaku", "sve", "svi", "sva",
        "dobro",
        "dobijem", "dobiješ", "dobije", "dobijemo", "dobijete", "dobiju",
        // imperative/present verbs often used with "i"
        "podeli", "podelim", "podeliš", "podelimo", "podelite", "podele", "podeliti",
        "ostavi", "ostavim", "ostaviš", "ostavimo", "ostavite", "ostave", "ostaviti",
        "kreni", "krenem", "kreneš", "krene", "krenemo", "krenete", "krenu", "krenuti",
        "stavi", "stavim", "staviš", "stavimo", "stavite", "stave", "staviti",
        "pokreni", "pokrenem", "pokreneš", "pokrene", "pokrenemo", "pokrenete", "pokrenu", "pokrenuti",
        "deli", "delim", "deliš", "delimo", "delite", "dele", "deliti",
        "ovim", "ovom", "ovoga", "ovog", "ovoj", "ovo", "ova", "ovaj",
    };

    private const int MaxClausePhraseWords = 4;

    /// <summary>
    /// A clause boundary that is not marked by punctuation: "i/a/pa/ili/te" followed by one of the
    /// clause-starting words/phrases ("i sad", "i onda", "i sve do tog trenutka"...). Match is
    /// longest-first (up to 4 words); enumeration is left alone.
    /// </summary>
    private static bool SoftClauseStart(IReadOnlyList<SmartBreakWord> w, int j)
    {
        if (j <= 0 || j >= w.Count || j + 1 >= w.Count)
        {
            return false;
        }

        var cur = Fold(w[j].Word);
        if (cur is not ("i" or "a" or "pa" or "ili" or "te"))
        {
            return false;
        }

        var sb = new System.Text.StringBuilder();
        for (var k = j + 1; k < w.Count && k <= j + MaxClausePhraseWords; k++)
        {
            if (k > j + 1)
            {
                sb.Append(' ');
            }

            sb.Append(Fold(w[k].Word));
            if (ClauseStartPhrases.Contains(sb.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Cuts a block into cue-sized groups that end at a clause boundary (comma/period) whenever
    /// possible, so a cue never breaks in the middle of a phrase. Capacity is two wrapped lines.
    /// </summary>
    private static List<(List<SmartBreakWord> Words, bool Risky)> SplitIntoCues(List<SmartBreakWord> block, IReadOnlyList<(double Start, double End)> runs)
    {
        var cues = new List<(List<SmartBreakWord> Words, bool Risky)>();
        var i = 0;
        var n = block.Count;
        while (i < n)
        {
            var len = 0;
            var lastHard = -1; // last punctuation clause boundary (exclusive end index)
            var lastSoft = -1; // last "i ..." clause boundary
            var boundaries = new List<int>(); // every punctuation/soft boundary seen in range
            var j = i;
            while (j < n)
            {
                var wl = block[j].Word.Trim().Length + (j > i ? 1 : 0);
                if (len + wl > MaxLen * MaxLines && j > i)
                {
                    break;
                }

                len += wl;
                j++;
                var hard = IsClauseEnd(block[j - 1].Word);
                if (hard)
                {
                    lastHard = j;
                    boundaries.Add(j);

                    // a real pause right after a comma ends the cue
                    if (j < n)
                    {
                        var gap = block[j].Start - EffEnd(runs, block[j - 1]);
                        if (gap >= PauseAtComma)
                        {
                            break;
                        }
                    }
                }
                else if (SoftClauseStart(block, j))
                {
                    lastSoft = j;
                    boundaries.Add(j);
                }
            }

            int end;
            var risky = false;
            if (j >= n)
            {
                end = n;
            }
            else if (lastHard > i)
            {
                end = lastHard; // a comma/sentence boundary wins over soft and pause
            }
            else if (lastSoft > i)
            {
                end = lastSoft;
                risky = true;
            }
            else
            {
                // No punctuation/soft boundary in range: use the longest pause inside the capacity
                // window as an "imaginary comma", else fall back to plain line wrapping.
                risky = true;
                var bestK = -1;
                var bestPause = -1.0;
                for (var k = i + 1; k <= j && k < n; k++)
                {
                    var pause = block[k].Start - EffEnd(runs, block[k - 1]);
                    if (pause > bestPause)
                    {
                        bestPause = pause;
                        bestK = k;
                    }
                }

                if (bestK > i && bestPause >= 0.15)
                {
                    end = bestK;
                }
                else
                {
                    var lines = BuildLines(block.GetRange(i, n - i));
                    var take = lines.Take(MaxLines).Sum(l => l.Count);
                    end = Math.Min(n, i + Math.Max(1, take));
                }
            }

            // Two-line limit is absolute: if the chosen group still needs more lines, step back to
            // the largest punctuation/soft boundary that fits, else trim to two lines.
            if (BuildLines(block.GetRange(i, end - i)).Count > MaxLines)
            {
                var best = -1;
                foreach (var b in boundaries)
                {
                    if (b > i && b <= end && b > best && BuildLines(block.GetRange(i, b - i)).Count <= MaxLines)
                    {
                        best = b;
                    }
                }

                if (best > i)
                {
                    end = best;
                    risky = false;
                }
                else
                {
                    end = i + Math.Max(1, BuildLines(block.GetRange(i, end - i)).Take(MaxLines).Sum(l => l.Count));
                }
            }

            var group = block.GetRange(i, end - i);

            Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
                $"DIAG split: first=\"{block[i].Word.Trim()}\" i={i} j={j} end={end} hard={lastHard} soft={lastSoft} text=\"{string.Join(" ", group.Select(x => x.Word.Trim()))}\"");

            // A split at a soft "i ..." boundary reads better when the upper block ends with a
            // comma, so append one (unless it already ends with punctuation).
            if (end == lastSoft && group.Count > 0)
            {
                var lastIdx = group.Count - 1;
                var w = group[lastIdx];
                if (LastChar(w.Word) is not (',' or ';' or ':' or '.' or '!' or '?' or '…'))
                {
                    group[lastIdx] = new SmartBreakWord(w.Word.TrimEnd() + ",", w.Start, w.End);
                }
            }

            cues.Add((group, risky));
            i = end;
        }

        // A sentence that already spans two or more cues, where one cue has a line filled to the
        // limit and a comma, gets split further at the comma nearest the middle (shorter lines).
        if (cues.Count >= 2)
        {
            var refined = new List<(List<SmartBreakWord> Words, bool Risky)>();
            foreach (var cue in cues)
            {
                RefineDense(cue, refined);
            }

            cues = refined;
        }

        return cues;
    }

    private static bool IsDense(List<SmartBreakWord> cue)
    {
        var lines = BuildLines(cue);
        return lines.Any(l => LLen(l, 0, l.Count) >= MaxLen);
    }

    private static bool HasComma(List<SmartBreakWord> cue)
    {
        for (var k = 1; k < cue.Count; k++)
        {
            if (LastChar(cue[k - 1].Word) == ',')
            {
                return true;
            }
        }

        return false;
    }

    private static void RefineDense((List<SmartBreakWord> Words, bool Risky) cue, List<(List<SmartBreakWord> Words, bool Risky)> outCues)
    {
        if (cue.Words.Count > 1 && HasComma(cue.Words) && IsDense(cue.Words))
        {
            var best = -1;
            var bestDist = double.MaxValue;
            var mid = cue.Words.Count / 2.0;
            for (var k = 1; k < cue.Words.Count; k++)
            {
                if (LastChar(cue.Words[k - 1].Word) == ',')
                {
                    var d = Math.Abs(k - mid);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = k;
                    }
                }
            }

            if (best > 0 && best < cue.Words.Count)
            {
                Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog(
                    $"DIAG dense: split \"{string.Join(" ", cue.Words.Select(x => x.Word.Trim()))}\" at comma #{best}");
                RefineDense((cue.Words.GetRange(0, best), true), outCues);
                RefineDense((cue.Words.GetRange(best, cue.Words.Count - best), true), outCues);
                return;
            }
        }

        outCues.Add(cue);
    }

    private static double? RunEnd(IReadOnlyList<(double Start, double End)> runs, double t, double tol = 0.08)
    {
        foreach (var (a, b) in runs)
        {
            if (a - tol <= t && t <= b + tol)
            {
                return b;
            }
        }

        return null;
    }

    private static double? RunStart(IReadOnlyList<(double Start, double End)> runs, double t, double tol = 0.05)
    {
        foreach (var (a, b) in runs)
        {
            if (a - tol <= t && t <= b + tol)
            {
                return a;
            }
        }

        return null;
    }

    /// <summary>
    /// Word timings from an engine's word-level JSON: WhisperX / faster-whisper / stable-ts
    /// (<c>segments[].words[]</c> or <c>word_segments[]</c>), each word <c>{word,start,end}</c>.
    /// </summary>
    public static List<SmartBreakWord> ParseWords(string jsonPath)
    {
        var words = new List<SmartBreakWord>();
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;

        void TakeWords(JsonElement arr)
        {
            foreach (var w in arr.EnumerateArray())
            {
                if (!w.TryGetProperty("word", out var wordEl))
                {
                    continue;
                }

                var word = wordEl.GetString();
                if (word == null || !w.TryGetProperty("start", out var sEl) || sEl.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                // faster-whisper (Purfview) emits each word with a leading space (" šećera");
                // joining the tokens with a space would then double every gap.
                word = word.Trim();
                if (word.Length == 0)
                {
                    continue;
                }

                var start = sEl.GetDouble();
                var end = w.TryGetProperty("end", out var eEl) && eEl.ValueKind == JsonValueKind.Number ? eEl.GetDouble() : start;
                words.Add(new SmartBreakWord(word, start, end));
            }
        }

        if (root.TryGetProperty("word_segments", out var ws) && ws.ValueKind == JsonValueKind.Array && ws.GetArrayLength() > 0)
        {
            TakeWords(ws);
        }
        else if (root.TryGetProperty("segments", out var segs) && segs.ValueKind == JsonValueKind.Array)
        {
            foreach (var seg in segs.EnumerateArray())
            {
                if (seg.TryGetProperty("words", out var sw) && sw.ValueKind == JsonValueKind.Array)
                {
                    TakeWords(sw);
                }
            }
        }

        return words;
    }

    /// <summary>
    /// Builds cues (start, end, lines) from word timings and Silero speech runs (already padded).
    /// </summary>
    public static List<(double Start, double End, List<string> Lines, bool Risky)> Build(
        List<SmartBreakWord> words,
        IReadOnlyList<(double Start, double End)> runs)
    {
        if (words.Count == 0)
        {
            return new List<(double, double, List<string>, bool)>();
        }

        // First step: put a comma before every clause connector ("i sad", "i onda", "a onda",
        // "i nakon toga"...). Then every later rule (split/line break) sees a real comma instead of
        // a hidden "soft" boundary, so the choice of where to cut is uniform and larger.
        for (var k = 1; k < words.Count; k++)
        {
            if (SoftClauseStart(words, k))
            {
                var prev = words[k - 1];
                if (LastChar(prev.Word) is not (',' or ';' or ':' or '.' or '!' or '?' or '…'))
                {
                    words[k - 1] = new SmartBreakWord(prev.Word.TrimEnd() + ",", prev.Start, prev.End);
                }
            }
        }

        // split the word stream into blocks at long effective gaps / sentence ends
        var blocks = new List<(List<SmartBreakWord> Words, bool EndedByPause)>();
        var cur = new List<SmartBreakWord> { words[0] };
        var curEndedByPause = false;
        for (var idx = 0; idx < words.Count - 1; idx++)
        {
            var a = words[idx];
            var b = words[idx + 1];
            var endsSentence = EndsSentence(a.Word);
            var gap = b.Start - EffEnd(runs, a);
            // A long pause breaks even where grammar would normally keep the words together, so the
            // cue does not sit on screen through the silence.
            var bigGap = gap > Gap && (!Forbidden(words, idx + 1) || gap > 2.5);
            // A pause starts a new block, but never leave a one-word block unless that word really
            // ends a sentence ("Opa."). A window/mis-timed single word ("Ovaj") joins what follows.
            var stranding = cur.Count == 1 && !EndsSentence(cur[0].Word);
            if (endsSentence || (bigGap && !stranding))
            {
                if (bigGap)
                {
                    Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog($"DIAG bigGap: after \"{a.Word.Trim()}\" gap={gap:F2}s -> \"{b.Word.Trim()}\"");
                }

                blocks.Add((cur, bigGap));
                cur = new List<SmartBreakWord> { b };
                curEndedByPause = false;
            }
            else
            {
                cur.Add(b);
            }
        }

        blocks.Add((cur, curEndedByPause));

        foreach (var (wb, _) in blocks)
        {
            if (wb.Count == 1)
            {
                Nikse.SubtitleEdit.Logic.Config.Se.WriteToolsLog($"DIAG 1-word block: \"{wb[0].Word.Trim()}\" {wb[0].Start:F2}-{wb[0].End:F2}");
            }
        }

        (double Start, double End, List<string> Lines, bool Risky) MakeCue(List<List<SmartBreakWord>> group, bool risky)
        {
            var flat = group.SelectMany(g => g).ToList();
            var first = flat[0];
            var last = flat[^1];
            var s = first.Start;

            // End the cue where the voice actually stops (see EffEnd).
            var e = EffEnd(runs, last);
            if (e < last.Start)
            {
                e = SoundEnd(last);
            }

            return (s, e, group.Select(Text).ToList(), risky);
        }

        var cues = new List<(double Start, double End, List<string> Lines, bool Risky)>();
        foreach (var (block, endedByPause) in blocks)
        {
            var groups = SplitIntoCues(block, runs);
            for (var gi = 0; gi < groups.Count; gi++)
            {
                var (groupWords, groupRisky) = groups[gi];
                // A block ended by a long pause makes its last cue a judgment call too.
                var risky = groupRisky || (endedByPause && gi == groups.Count - 1);
                cues.Add(MakeCue(BuildLines(groupWords), risky));
            }
        }

        // merge a short filler fragment into the following cue
        static bool IsFiller(List<string> lines)
        {
            var t = string.Join(" ", lines).TrimEnd('„', '"', '»', ')', ']').TrimEnd();
            return t.Length <= 12 && (t.Length == 0 || ".!?…".IndexOf(t[^1]) < 0);
        }

        var merged = new List<(double Start, double End, List<string> Lines, bool Risky)>();
        for (var i = 0; i < cues.Count; i++)
        {
            // Do not merge across a real pause: that would put the short fragment's text back on
            // screen through the silence the split just removed.
            if (IsFiller(cues[i].Lines) && i + 1 < cues.Count && cues[i + 1].Start - cues[i].End <= 0.4)
            {
                // Rebuild from the text is lossy for timing, so just concatenate the lines instead.
                var joined = new List<string>();
                joined.AddRange(cues[i].Lines.Take(1));
                var nxt = cues[i + 1].Lines.ToList();
                if (joined.Count > 0 && nxt.Count > 0)
                {
                    joined[^1] = joined[^1] + " " + nxt[0];
                    joined.AddRange(nxt.Skip(1));
                }
                merged.Add((cues[i].Start, cues[i + 1].End, joined, cues[i + 1].Risky));
                i++;
            }
            else
            {
                merged.Add(cues[i]);
            }
        }

        // Never leave a cue ending on a lone connector: the pause that ended the block can fall right
        // after it ("Treba jos malo brasna, | i sad ..."), so move it onto the next cue and mark the
        // newly exposed clause end with a comma (the same convention the soft split uses below).
        // The Lines lists are mutated in place - the tuples hold the same references.
        for (var ci = 0; ci + 1 < merged.Count; ci++)
        {
            var lines = merged[ci].Lines;
            if (lines.Count == 0)
            {
                continue;
            }

            var lastLineIndex = lines.Count - 1;
            var lastLine = lines[lastLineIndex].TrimEnd();
            var space = lastLine.LastIndexOf(' ');
            if (space < 0)
            {
                continue; // a one-word cue keeps its word, dangling or not
            }

            var lastWord = lastLine[(space + 1)..];
            if (!IsDanglingEnd(lastWord))
            {
                continue;
            }

            var head = lastLine[..space].TrimEnd();
            if (head.Length > 0 && LastChar(head) is not (',' or ';' or ':' or '.' or '!' or '?' or '…'))
            {
                head += ",";
            }

            lines[lastLineIndex] = head;

            var nextLines = merged[ci + 1].Lines;
            if (nextLines.Count == 0)
            {
                nextLines.Add(lastWord);
            }
            else
            {
                nextLines[0] = lastWord + " " + nextLines[0];
            }
        }

        // lead-in: prefer the real Silero speech onset, else the word start minus a small lead
        var starts = new double[merged.Count];
        for (var i = 0; i < merged.Count; i++)
        {
            var s = Math.Max(0.0, merged[i].Start - Lead);
            var firstWordStart = merged[i].Start;
            var rs = RunStart(runs, firstWordStart);
            if (rs.HasValue && firstWordStart - rs.Value is >= 0.0 and <= 0.4)
            {
                s = rs.Value;
            }

            starts[i] = s;
        }

        var timed = new List<(double Start, double End, List<string> Lines, bool Risky)>();
        for (var i = 0; i < merged.Count; i++)
        {
            var (_, e, lines, risky) = merged[i];
            var s2 = starts[i];
            if (i > 0)
            {
                s2 = Math.Max(s2, timed[i - 1].End + 0.02);
            }

            var end = Math.Max(e + Tail, s2 + MinCue);
            if (i + 1 < merged.Count)
            {
                end = Math.Min(end, starts[i + 1] - 0.04);
            }

            if (s2 >= end)
            {
                s2 = Math.Max(0.0, end - 0.2);
            }

            timed.Add((s2, end, lines, risky));
        }

        // capitalize a cue that starts a new sentence
        for (var i = 0; i < timed.Count; i++)
        {
            var newSentence = i == 0;
            if (i > 0)
            {
                var t = timed[i - 1].Lines[^1].TrimEnd('„', '"', '»', ')', ']').TrimEnd();
                if (LineEndsSentence(t))
                {
                    newSentence = true;
                }
            }

            if (newSentence)
            {
                var lines = timed[i].Lines.ToList();
                lines[0] = CapitalizeFirst(lines[0]);
                timed[i] = (timed[i].Start, timed[i].End, lines, timed[i].Risky);
            }
        }

        return timed;
    }

    private static string CapitalizeFirst(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsLetter(s[i]))
            {
                return string.Concat(s.AsSpan(0, i), char.ToUpper(s[i], CultureInfo.InvariantCulture).ToString(), s.AsSpan(i + 1));
            }

            if (char.IsDigit(s[i]))
            {
                return s;
            }
        }

        return s;
    }
}
