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
    public const int MaxLen = 43;
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

    private static readonly Regex NumRe = new(@"^[0-9]+([.,][0-9]+)?$", RegexOptions.Compiled);

    private static string Bare(string w) => new string(w.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

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
        return bare.Length > 1 && !Abbrev.Contains(bare) && !Units.Contains(bare);
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
        return bare.Length > 1 && !Abbrev.Contains(bare) && !Units.Contains(bare);
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
            if (end < n)
            {
                var last = LastChar(words[end - 1].Word);
                var nxtc = LastChar(words[end].Word);
                if ((last == '\0' || ",;:.!?…".IndexOf(last) < 0) && nxtc is ',' or ';' or ':')
                {
                    end++;
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

    private static readonly HashSet<string> ClauseStarters = new(StringComparer.OrdinalIgnoreCase)
    {
        "sad", "sada", "onda", "tada", "zatim", "zato", "tako", "posle", "poslije", "pre", "prije",
        "opet", "uvek", "uvijek", "nikad", "nikada", "dalje", "prvo", "potom", "konačno", "naposletku",
        "pošto", "posto", "nakon",
    };

    /// <summary>
    /// A clause boundary that is not marked by punctuation: "i/a/pa/ili/te" followed by a
    /// clause-starting adverb ("i sad", "i onda", "a onda"...). Enumeration ("so i biber",
    /// "jedan i dva") is followed by a noun/number, so it is left alone.
    /// </summary>
    private static bool SoftClauseStart(IReadOnlyList<SmartBreakWord> w, int j)
    {
        if (j <= 0 || j >= w.Count || j + 1 >= w.Count)
        {
            return false;
        }

        var cur = Bare(w[j].Word);
        if (cur is not ("i" or "a" or "pa" or "ili" or "te"))
        {
            return false;
        }

        return ClauseStarters.Contains(Bare(w[j + 1].Word));
    }

    /// <summary>
    /// Cuts a block into cue-sized groups that end at a clause boundary (comma/period) whenever
    /// possible, so a cue never breaks in the middle of a phrase. Capacity is two wrapped lines.
    /// </summary>
    private static List<List<SmartBreakWord>> SplitIntoCues(List<SmartBreakWord> block, IReadOnlyList<(double Start, double End)> runs)
    {
        var cues = new List<List<SmartBreakWord>>();
        var i = 0;
        var n = block.Count;
        while (i < n)
        {
            var len = 0;
            var lastPunct = -1;
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
                var clause = IsClauseEnd(block[j - 1].Word) || SoftClauseStart(block, j);
                if (clause)
                {
                    lastPunct = j;

                    // A real pause right after a comma ends the cue: enumeration spoken with a gap
                    // ("50 ml ulja, <pause> jedno celo jaje") should not stay in one cue.
                    if (j < n && IsClauseEnd(block[j - 1].Word))
                    {
                        var gap = block[j].Start - EffEnd(runs, block[j - 1]);
                        if (gap >= PauseAtComma)
                        {
                            break;
                        }
                    }
                }
            }

            int end;
            if (j >= n)
            {
                end = n;
            }
            else if (lastPunct > i)
            {
                end = lastPunct;
            }
            else
            {
                var lines = BuildLines(block.GetRange(i, n - i));
                var take = lines.Take(MaxLines).Sum(l => l.Count);
                end = Math.Min(n, i + Math.Max(1, take));
            }

            var groupLines = BuildLines(block.GetRange(i, end - i));
            if (groupLines.Count > MaxLines)
            {
                end = i + Math.Max(1, groupLines.Take(MaxLines).Sum(l => l.Count));
            }

            cues.Add(block.GetRange(i, end - i));
            i = end;
        }

        return cues;
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
    public static List<(double Start, double End, List<string> Lines)> Build(
        List<SmartBreakWord> words,
        IReadOnlyList<(double Start, double End)> runs)
    {
        if (words.Count == 0)
        {
            return new List<(double, double, List<string>)>();
        }

        // split the word stream into blocks at long effective gaps / sentence ends
        var blocks = new List<List<SmartBreakWord>>();
        var cur = new List<SmartBreakWord> { words[0] };
        for (var idx = 0; idx < words.Count - 1; idx++)
        {
            var a = words[idx];
            var b = words[idx + 1];
            var endsSentence = EndsSentence(a.Word);
            var gap = b.Start - EffEnd(runs, a);
            // A long pause breaks even where grammar would normally keep the words together, so the
            // cue does not sit on screen through the silence.
            var bigGap = gap > Gap && (!Forbidden(words, idx + 1) || gap > 2.5);
            if (bigGap || endsSentence)
            {
                blocks.Add(cur);
                cur = new List<SmartBreakWord> { b };
            }
            else
            {
                cur.Add(b);
            }
        }

        blocks.Add(cur);

        (double Start, double End, List<string> Lines) MakeCue(List<List<SmartBreakWord>> group)
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

            return (s, e, group.Select(Text).ToList());
        }

        var cues = new List<(double Start, double End, List<string> Lines)>();
        foreach (var block in blocks)
        {
            foreach (var group in SplitIntoCues(block, runs))
            {
                cues.Add(MakeCue(BuildLines(group)));
            }
        }

        // merge a short filler fragment into the following cue
        static bool IsFiller(List<string> lines)
        {
            var t = string.Join(" ", lines).TrimEnd('„', '"', '»', ')', ']').TrimEnd();
            return t.Length <= 12 && (t.Length == 0 || ".!?…".IndexOf(t[^1]) < 0);
        }

        var merged = new List<(double Start, double End, List<string> Lines)>();
        for (var i = 0; i < cues.Count; i++)
        {
            // Do not merge across a real pause: that would put the short fragment's text back on
            // screen through the silence the split just removed.
            if (IsFiller(cues[i].Lines) && i + 1 < cues.Count && cues[i + 1].Start - cues[i].End <= 0.4)
            {
                var flat = cues[i].Lines.Concat(cues[i + 1].Lines)
                    .SelectMany(l => l.Split(' ')).Where(s => s.Length > 0)
                    .Select(w => new SmartBreakWord(w, 0, 0)).ToList();
                // rebuild from the text is lossy for timing, so just concatenate the lines instead
                var joined = new List<string>();
                joined.AddRange(cues[i].Lines.Take(1));
                var nxt = cues[i + 1].Lines.ToList();
                if (joined.Count > 0 && nxt.Count > 0)
                {
                    joined[^1] = joined[^1] + " " + nxt[0];
                    joined.AddRange(nxt.Skip(1));
                }
                merged.Add((cues[i].Start, cues[i + 1].End, joined));
                i++;
            }
            else
            {
                merged.Add(cues[i]);
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

        var timed = new List<(double Start, double End, List<string> Lines)>();
        for (var i = 0; i < merged.Count; i++)
        {
            var (_, e, lines) = merged[i];
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

            timed.Add((s2, end, lines));
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
                timed[i] = (timed[i].Start, timed[i].End, lines);
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
