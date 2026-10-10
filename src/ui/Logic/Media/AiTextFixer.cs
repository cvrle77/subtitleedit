using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic.Config;

namespace Nikse.SubtitleEdit.Logic.Media;

/// <summary>
/// Optional AI post-fix for a transcription: sends the whole transcript to an OpenAI-compatible chat
/// endpoint (e.g. OpenRouter) to add punctuation and fix spelling/dialect, then aligns the returned
/// tokens back onto the original words so the word timings are preserved. One request per run.
/// </summary>
public static class AiTextFixer
{
    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

    public static List<SmartBreakWord> Fix(
        IReadOnlyList<SmartBreakWord> words,
        string url,
        string apiKey,
        string model,
        string prompt)
    {
        if (words.Count == 0 || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
        {
            return words.ToList();
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var text = string.Join(" ", words.Select(w => w.Word.Trim()));
        var fixedText = Ask(url, apiKey, model, prompt, text);
        if (string.IsNullOrWhiteSpace(fixedText))
        {
            Se.WriteToolsLog($"AI fix: request failed after {watch.Elapsed.TotalSeconds:F1}s - text left unchanged");
            return words.ToList();
        }

        var tokens = fixedText
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var aligned = Align(words, tokens);
        Se.WriteToolsLog($"AI fix: 1 request, {watch.Elapsed.TotalSeconds:F1}s");
        return aligned;
    }

    private static string Ask(string url, string apiKey, string model, string prompt, string text)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model,
            temperature = 0,
            messages = new object[]
            {
                new { role = "user", content = prompt + "\n\n" + text },
            },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            using var response = Http.SendAsync(request).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                Se.WriteToolsLog("AI fix: HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);
                return string.Empty;
            }

            var json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?.Trim() ?? string.Empty;
        }
        catch (Exception exception)
        {
            SeLogger.Error(exception, "AI fix failed");
            return string.Empty;
        }
    }

    /// <summary>
    /// Maps the returned tokens onto the original words (1:1 when the count matches, else LCS with
    /// distribution of extra/missing tokens), keeping each word's timing.
    /// </summary>
    // Above this many words per side the DP table (n*m ints) would allocate hundreds of megabytes
    // on a long transcript, so the alignment is done in windows of this size instead.
    private const int MaxAlignWords = 1200;

    private static List<SmartBreakWord> Align(IReadOnlyList<SmartBreakWord> chunk, IReadOnlyList<string> toks)
    {
        var n = chunk.Count;
        var m = toks.Count;
        if (m == 0)
        {
            return chunk.ToList();
        }

        if ((long)n * m <= (long)MaxAlignWords * MaxAlignWords)
        {
            return AlignRange(chunk, 0, n, toks, 0, m);
        }

        // Long transcript: the model keeps word order, so splitting both sides proportionally gives
        // each window the matching words and keeps every DP table small.
        var result = new List<SmartBreakWord>(n);
        var wordStart = 0;
        while (wordStart < n)
        {
            var wordEnd = Math.Min(n, wordStart + MaxAlignWords);
            var tokenStart = (int)((long)wordStart * m / n);
            var tokenEnd = wordEnd == n ? m : (int)((long)wordEnd * m / n);
            result.AddRange(AlignRange(chunk, wordStart, wordEnd, toks, tokenStart, tokenEnd));
            wordStart = wordEnd;
        }

        return result;
    }

    // Aligns the words [wStart, wEnd) against the tokens [tStart, tEnd) and returns one word per
    // input word, keeping its timing.
    private static List<SmartBreakWord> AlignRange(IReadOnlyList<SmartBreakWord> chunk, int wStart, int wEnd,
        IReadOnlyList<string> toks, int tStart, int tEnd)
    {
        var n = wEnd - wStart;
        var m = tEnd - tStart;
        var result = new List<SmartBreakWord>(n);
        if (n == 0)
        {
            return result;
        }

        if (m == 0)
        {
            for (var i = wStart; i < wEnd; i++)
            {
                result.Add(chunk[i]);
            }

            return result;
        }

        if (n == m)
        {
            for (var i = 0; i < n; i++)
            {
                result.Add(new SmartBreakWord(toks[tStart + i], chunk[wStart + i].Start, chunk[wStart + i].End));
            }

            return result;
        }

        var dp = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                dp[i, j] = Norm(chunk[wStart + i].Word) == Norm(toks[tStart + j])
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var assigned = new List<string>[n];
        for (var i = 0; i < n; i++)
        {
            assigned[i] = new List<string>();
        }

        var a = 0;
        var b = 0;
        var lastOld = -1;
        while (a < n && b < m)
        {
            if (Norm(chunk[wStart + a].Word) == Norm(toks[tStart + b]))
            {
                assigned[a].Add(toks[tStart + b]);
                lastOld = a;
                a++;
                b++;
            }
            else if (dp[a + 1, b] >= dp[a, b + 1])
            {
                a++;
            }
            else
            {
                assigned[lastOld >= 0 ? lastOld : Math.Min(a, n - 1)].Add(toks[tStart + b]);
                b++;
            }
        }

        while (b < m)
        {
            assigned[lastOld >= 0 ? lastOld : n - 1].Add(toks[tStart + b]);
            b++;
        }

        for (var i = 0; i < n; i++)
        {
            var text = assigned[i].Count > 0 ? string.Join(" ", assigned[i]) : chunk[wStart + i].Word.Trim();
            result.Add(new SmartBreakWord(text, chunk[wStart + i].Start, chunk[wStart + i].End));
        }

        return result;
    }

    private static string Norm(string w)
    {
        var sb = new StringBuilder(w.Length);
        foreach (var c in w.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c switch
                {
                    'č' => 'c', 'ć' => 'c', 'ž' => 'z', 'š' => 's', 'đ' => 'd',
                    _ => c,
                });
            }
        }

        return sb.ToString();
    }
}
