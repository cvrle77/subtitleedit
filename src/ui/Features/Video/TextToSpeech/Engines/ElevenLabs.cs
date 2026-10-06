using Avalonia.Platform;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.Voices;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Download;
using Nikse.SubtitleEdit.Logic.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.Engines;

public class ElevenLabs : ITtsEngine
{
    /// <summary>
    /// Concurrent-request limit for a subscription tier, per the ElevenLabs docs:
    /// https://help.elevenlabs.io/hc/en-us/articles/14312733311761
    /// The table's lower column ("all other models", e.g. eleven_multilingual_v2 / eleven_v3) is
    /// used, because that is what a subtitle dub runs by default. Unknown/absent tiers fall back to
    /// 2 - the free-tier "other models" figure - so a failed subscription lookup can never exceed
    /// what even a free plan allows.
    /// </summary>
    public static int ConcurrencyForTier(string? tier)
    {
        return tier?.ToLowerInvariant() switch
        {
            "free" or "trial" or "grant" or "grant_tier_1_2025_07_23" or "grant_tier_2_2025_07_23" => 2,
            "starter" or "go" => 3,
            "creator" => 5,
            "pro" => 10,
            "scale" or "scale_2024_08_10" or "growing_business" or "business" => 15,
            "enterprise" => 15, // elevated in reality; 15 is the highest published figure
            _ => 2,
        };
    }

    public string Name => "ElevenLabs";
    public string Description => "pay/fast/good";
    public bool HasLanguageParameter => true;
    public bool HasApiKey => true;
    public bool HasRegion => false;
    public bool HasModel => true;
    public bool HasKeyFile => false;
    public bool SupportsVoiceCloning => false;
    public bool SupportsPerLineVoiceCloning => false;

    public Task<bool> IsInstalled(string? region)
    {
        return Task.FromResult(!string.IsNullOrEmpty(Se.Settings.Video.TextToSpeech.ElevenLabsApiKey));
    }

    private const string JsonFileName = "ElevenLabsVoices.json";
    private readonly ITtsDownloadService _ttsDownloadService;

    public ElevenLabs(ITtsDownloadService ttsDownloadService)
    {
        _ttsDownloadService = ttsDownloadService;
    }

    public override string ToString()
    {
        return $"{Name}";
    }

    public Task<Voice[]> GetVoices(string language)
    {
        var elevenLabsFolder = GetSetElevenLabsFolder();

        var voiceFileName = Path.Combine(elevenLabsFolder, JsonFileName);
        if (!File.Exists(voiceFileName))
        {
            var uri = new Uri("avares://SubtitleEdit/Assets/TextToSpeech/ElevenLabsVoices.json");
            using var stream = AssetLoader.Open(uri);
            using var fileStream = File.Create(voiceFileName);
            stream.CopyTo(fileStream);
        }

        return Task.FromResult(Map(voiceFileName));
    }

    private static Voice[] Map(string voiceFileName)
    {
        if (!File.Exists(voiceFileName))
        {
            return [];
        }

        var result = new List<Voice>();
        var json = File.ReadAllText(voiceFileName);
        var parser = new SeJsonParser();
        var voices = parser.GetArrayElementsByName(json, "voices");
        foreach (var voice in voices)
        {
            var name = parser.GetFirstObject(voice, "name");
            var voiceId = parser.GetFirstObject(voice, "voice_id");
            var gender = parser.GetFirstObject(voice, "gender");
            var description = parser.GetFirstObject(voice, "description");
            var accent = parser.GetFirstObject(voice, "accent");
            var useCase = parser.GetFirstObject(voice, "use case");
            result.Add(new Voice(new ElevenLabVoice(string.Empty, name, gender, description, useCase, accent, voiceId)));
        }

        return result.ToArray();
    }

    private static string GetSetElevenLabsFolder()
    {
        if (!Directory.Exists(Se.TextToSpeechFolder))
        {
            Directory.CreateDirectory(Se.TextToSpeechFolder);
        }

        var elevenLabsFolder = Path.Combine(Se.TextToSpeechFolder, "ElevenLabs");
        if (!Directory.Exists(elevenLabsFolder))
        {
            Directory.CreateDirectory(elevenLabsFolder);
        }

        return elevenLabsFolder;
    }

    public bool IsVoiceInstalled(Voice voice)
    {
        return true;
    }

    public Task<TtsLanguage[]> GetLanguages(Voice voice, string? model)
    {
        // see https://help.elevenlabs.io/hc/en-us/articles/17883183930129-What-models-do-you-offer-and-what-is-the-difference-between-them

        var languages = new List<TtsLanguage>();

        // Eleven v4 family supports 90+ languages (a superset of v3's 70+), including Serbian.
        // Reuse the v3 list until a dedicated one is needed.
        if (model is "eleven_v3" or "eleven_v4" or "eleven_v4_turbo")
        {
            languages = new List<TtsLanguage>
            {
                new("Afrikaans", "af"),
                new("Arabic", "ar"),
                new("Armenian", "hy"),
                new("Assamese", "as"),
                new("Azerbaijani", "az"),
                new("Belarusian", "be"),
                new("Bengali", "bn"),
                new("Bosnian", "bs"),
                new("Bulgarian", "bg"),
                new("Catalan", "ca"),
                new("Cebuano", "ceb"),
                new("Chichewa", "ny"),
                new("Croatian", "hr"),
                new("Czech", "cs"),
                new("Danish", "da"),
                new("Dutch", "nl"),
                new("English", "en"),
                new("Estonian", "et"),
                new("Filipino", "fil"),
                new("Finnish", "fi"),
                new("French", "fr"),
                new("Galician", "gl"),
                new("Georgian", "ka"),
                new("German", "de"),
                new("Greek", "el"),
                new("Gujarati", "gu"),
                new("Hausa", "ha"),
                new("Hebrew", "he"),
                new("Hindi", "hi"),
                new("Hungarian", "hu"),
                new("Icelandic", "is"),
                new("Indonesian", "id"),
                new("Irish", "ga"),
                new("Italian", "it"),
                new("Japanese", "ja"),
                new("Javanese", "jv"),
                new("Kannada", "kn"),
                new("Kazakh", "kk"),
                new("Kyrgyz", "ky"),
                new("Korean", "ko"),
                new("Latvian", "lv"),
                new("Lingala", "ln"),
                new("Lithuanian", "lt"),
                new("Luxembourgish", "lb"),
                new("Macedonian", "mk"),
                new("Malay", "ms"),
                new("Malayalam", "ml"),
                new("Mandarin Chinese", "zh"),
                new("Marathi", "mr"),
                new("Nepali", "ne"),
                new("Norwegian", "no"),
                new("Pashto", "ps"),
                new("Persian", "fa"),
                new("Polish", "pl"),
                new("Portuguese", "pt"),
                new("Punjabi", "pa"),
                new("Romanian", "ro"),
                new("Russian", "ru"),
                new("Serbian", "sr"),
                new("Sindhi", "sd"),
                new("Slovak", "sk"),
                new("Slovenian", "sl"),
                new("Somali", "so"),
                new("Spanish", "es"),
                new("Swahili", "sw"),
                new("Swedish", "sv"),
                new("Tamil", "ta"),
                new("Telugu", "te"),
                new("Thai", "th"),
                new("Turkish", "tr"),
                new("Ukrainian", "uk"),
                new("Urdu", "ur"),
                new("Vietnamese", "vi"),
                new("Welsh", "cy"),
            };
        }

        if (model is "eleven_v4" or "eleven_v4_turbo")
        {
            // v4 and v4 Turbo share the same 90+ language list
            languages = new List<TtsLanguage>
            {
                new("Afrikaans", "af"),
                new("Amharic", "am"),
                new("Arabic", "ar"),
                new("Armenian", "hy"),
                new("Assamese", "as"),
                new("Asturian", "ast"),
                new("Azerbaijani", "az"),
                new("Belarusian", "be"),
                new("Bengali", "bn"),
                new("Bosnian", "bs"),
                new("Bulgarian", "bg"),
                new("Burmese", "my"),
                new("Cantonese", "yue"),
                new("Catalan", "ca"),
                new("Cebuano", "ceb"),
                new("Croatian", "hr"),
                new("Czech", "cs"),
                new("Danish", "da"),
                new("Dutch", "nl"),
                new("English", "en"),
                new("Estonian", "et"),
                new("Filipino", "fil"),
                new("Finnish", "fi"),
                new("French", "fr"),
                new("Fula", "ff"),
                new("Galician", "gl"),
                new("Georgian", "ka"),
                new("German", "de"),
                new("Greek", "el"),
                new("Gujarati", "gu"),
                new("Hausa", "ha"),
                new("Hebrew", "he"),
                new("Hindi", "hi"),
                new("Hungarian", "hu"),
                new("Icelandic", "is"),
                new("Indonesian", "id"),
                new("Italian", "it"),
                new("Japanese", "ja"),
                new("Javanese", "jv"),
                new("Kamba", "kam"),
                new("Kannada", "kn"),
                new("Kazakh", "kk"),
                new("Korean", "ko"),
                new("Kyrgyz", "ky"),
                new("Lao", "lo"),
                new("Latvian", "lv"),
                new("Lingala", "ln"),
                new("Lithuanian", "lt"),
                new("Luganda", "lg"),
                new("Luxembourgish", "lb"),
                new("Macedonian", "mk"),
                new("Malay", "ms"),
                new("Malayalam", "ml"),
                new("Maltese", "mt"),
                new("Mandarin Chinese", "zh"),
                new("Maori", "mi"),
                new("Marathi", "mr"),
                new("Mongolian", "mn"),
                new("Nepali", "ne"),
                new("Norwegian", "no"),
                new("Occitan", "oc"),
                new("Odia", "or"),
                new("Pashto", "ps"),
                new("Persian", "fa"),
                new("Polish", "pl"),
                new("Portuguese", "pt"),
                new("Punjabi", "pa"),
                new("Romanian", "ro"),
                new("Russian", "ru"),
                new("Serbian", "sr"),
                new("Shona", "sn"),
                new("Sindhi", "sd"),
                new("Slovak", "sk"),
                new("Slovenian", "sl"),
                new("Somali", "so"),
                new("Sorani Kurdish", "ckb"),
                new("Spanish", "es"),
                new("Swahili", "sw"),
                new("Swedish", "sv"),
                new("Tajik", "tg"),
                new("Tamil", "ta"),
                new("Telugu", "te"),
                new("Thai", "th"),
                new("Turkish", "tr"),
                new("Ukrainian", "uk"),
                new("Urdu", "ur"),
                new("Uzbek", "uz"),
                new("Vietnamese", "vi"),
                new("Welsh", "cy"),
                new("Wolof", "wo"),
                new("Zulu", "zu"),
            };
        }

        if (model is "eleven_multilingual_v2" or "eleven_turbo_v2_5")
        {
            languages = new List<TtsLanguage>
            {
                new("Arabic", "ar"),
                new("Bulgarian", "bg"),
                new("Chinese", "zh"),
                new("Croatian", "hr"),
                new("Czech", "cs"),
                new("Danish", "da"),
                new("Dutch", "nl"),
                new("English", "en"),
                new("Filipino", "fil"),
                new("Finnish", "fi"),
                new("French", "fr"),
                new("German", "de"),
                new("Greek", "el"),
                new("Hindi", "hi"),
                new("Indonesian", "id"),
                new("Italian", "it"),
                new("Japanese", "ja"),
                new("Korean", "ko"),
                new("Malay", "ms"),
                new("Polish", "pl"),
                new("Portuguese", "pt"),
                new("Romanian", "ro"),
                new("Russian", "ru"),
                new("Slovak", "sk"),
                new("Spanish", "es"),
                new("Swedish", "sv"),
                new("Tamil", "ta"),
                new("Turkish", "tr"),
                new("Ukrainian", "uk"),
            };

            if (model == "eleven_turbo_v2_5")
            {
                // Fall through to the sorted return below - an early return here put these
                // three at the bottom of the language combo instead of in alphabetical order.
                languages.Add(new TtsLanguage("Hungarian", "hu"));
                languages.Add(new TtsLanguage("Norwegian", "no"));
                languages.Add(new TtsLanguage("Vietnamese", "vi"));
            }
        }

        if (model == "eleven_turbo_v2")
        {
            languages = new List<TtsLanguage>
            {
                new("English", "en"),
            };
        }

        if (model == "eleven_multilingual_v1")
        {
            languages = new List<TtsLanguage>
            {
                new("English", "en"),
                new("German", "de"),
                new("Polish", "pl"),
                new("Spanish", "es"),
                new("Italian", "it"),
                new("French", "fr"),
                new("Hindi", "hi"),
                new("Portuguese", "pt"),
            };
        }

        return Task.FromResult(languages.OrderBy(p => p.Name).ToArray());
    }

    public async Task<Voice[]> RefreshVoices(string language, CancellationToken cancellationToken)
    {
        var ms = new MemoryStream();
        await _ttsDownloadService.DownloadElevenLabsVoiceList(ms, null, cancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(GetSetElevenLabsFolder(), JsonFileName), ms.ToArray(), cancellationToken);
        return await GetVoices(language);
    }

    public Task<TtsResult> Speak(
        string text,
        string outputFolder,
        Voice voice,
        TtsLanguage? language,
        string? region,
        string? model,
        CancellationToken cancellationToken)
    {
        return SpeakInternal(text, outputFolder, voice, language, model, null, cancellationToken);
    }

    /// <summary>
    /// Like <see cref="Speak"/>, but carries the request-stitching context for the surrounding
    /// lines so this line's delivery continues the previous one (ElevenLabs "request stitching").
    /// Only the Eleven v4 family honours it: v3 has no request stitching, and the older models are
    /// conditioned differently, so for them the context is dropped and the call is identical to
    /// <see cref="Speak"/>.
    /// </summary>
    public Task<TtsResult> SpeakStitched(
        string text,
        string outputFolder,
        Voice voice,
        TtsLanguage? language,
        string? region,
        string? model,
        TtsStitchContext stitch,
        CancellationToken cancellationToken)
    {
        return SpeakInternal(text, outputFolder, voice, language, model, stitch, cancellationToken);
    }

    private async Task<TtsResult> SpeakInternal(
        string text,
        string outputFolder,
        Voice voice,
        TtsLanguage? language,
        string? model,
        TtsStitchContext? stitch,
        CancellationToken cancellationToken)
    {
        if (voice.EngineVoice is not ElevenLabVoice elevenLabVoice)
        {
            throw new ArgumentException("Voice is not an ElevenLabVoice");
        }

        // Callers pass null when this engine is not the globally selected one (per-actor cast
        // rows, cast-dialog voice test) - fall back to the saved/default model instead of
        // throwing, which aborted the whole generation run at the first ElevenLabs row.
        if (string.IsNullOrEmpty(model))
        {
            model = Se.Settings.Video.TextToSpeech.ElevenLabsModel;
        }

        if (string.IsNullOrEmpty(model))
        {
            model = "eleven_v4_turbo";
        }

        // Request stitching is a v4-only feature (v3 explicitly does not support it). Keep the
        // context out of every other model so their request body is byte-for-byte what it was.
        var isV4 = model is "eleven_v4" or "eleven_v4_turbo";
        var previousText = isV4 ? stitch?.PreviousText ?? string.Empty : string.Empty;
        var nextText = isV4 ? stitch?.NextText ?? string.Empty : string.Empty;
        var previousRequestIds = isV4 ? stitch?.PreviousRequestIds : null;

        Se.WriteToolsLog($"ElevenLabs: voice={elevenLabVoice.Voice}, voiceId={elevenLabVoice.VoiceId}, model={model}, textLen={text.Length}, stitchIds={previousRequestIds?.Count ?? 0}");

        // A configured general accent is applied invisibly to every line (v4/v3, which read audio
        // tags): prepend it to the leading tag group so "[warmly] text" becomes
        // "[American accent warmly] text". Empty setting -> untouched.
        var accentedText = ApplyGeneralAccent(text, model);

        var ms = new MemoryStream();
        var (ok, error, requestId) = await _ttsDownloadService.DownloadElevenLabsVoiceSpeak(accentedText, elevenLabVoice, model, Se.Settings.Video.TextToSpeech.ElevenLabsApiKey, language?.Code ?? string.Empty, ms, null, cancellationToken, previousText, nextText, previousRequestIds);
        if (!ok)
        {
            // Forced: a failed API call must land in the tools log even when the setting is off,
            // so a bug report shows which segments failed and why (#12093).
            Se.WriteToolsLog($"ElevenLabs: request failed (voice={elevenLabVoice.Voice}, textLen={text.Length}): {error}", true);
            return new TtsResult { Text = text, FileName = string.Empty, Error = true, ErrorMessage = error };
        }

        var fileName = Path.Combine(TtsOutputFolder.Resolve(outputFolder, GetSetElevenLabsFolder), Guid.NewGuid() + ".mp3");
        await File.WriteAllBytesAsync(fileName, ms.ToArray(), cancellationToken);
        return new TtsResult { Text = text, FileName = fileName, RequestId = requestId };
    }

    /// <summary>
    /// Synthesises a whole run of cues as one ElevenLabs request (the timestamped dialogue
    /// endpoint) and cuts the returned audio into one clip per cue. This is what gives the model the
    /// continuous context that per-line requests lack; the per-character timings place each cut in
    /// the middle of the pause between cues, so the first phoneme is never clipped and neighbouring
    /// clips never overlap. Returns one result per cue, in order - or an empty list plus an error
    /// the caller can fall back from (per-line).
    /// </summary>
    public async Task<(List<TtsResult> Results, string Error)> SpeakCueTextsAsChunkAsync(
        IReadOnlyList<string> cueTexts,
        string outputFolder,
        Voice voice,
        TtsLanguage? language,
        string? model,
        CancellationToken cancellationToken)
    {
        if (voice.EngineVoice is not ElevenLabVoice elevenLabVoice)
        {
            throw new ArgumentException("Voice is not an ElevenLabVoice");
        }

        if (string.IsNullOrEmpty(model))
        {
            model = Se.Settings.Video.TextToSpeech.ElevenLabsModel;
        }

        // Build the chunk text exactly as it will be sent - the general accent once at the front,
        // each cue's break tags translated for v3/v4, then the cues joined by single spaces - and
        // keep each cue's character range so the alignment can be mapped back to it.
        var converted = new string[cueTexts.Count];
        for (var i = 0; i < cueTexts.Count; i++)
        {
            converted[i] = TtsDownloadService.ConvertBreakTagsToV3AudioTags(Utilities.UnbreakLine(cueTexts[i] ?? string.Empty));
        }

        var joined = new StringBuilder();
        var starts = new int[cueTexts.Count];
        var ends = new int[cueTexts.Count];
        for (var i = 0; i < converted.Length; i++)
        {
            if (i > 0)
            {
                joined.Append(' ');
            }

            starts[i] = joined.Length;
            joined.Append(converted[i]);
            ends[i] = joined.Length - 1;
        }

        var accentTag = BuildAccentTag(model);
        var text = accentTag + joined.ToString();

        var (ok, error, timed) = await _ttsDownloadService.DownloadElevenLabsDialogueWithTimestamps(
            text, elevenLabVoice, model, Se.Settings.Video.TextToSpeech.ElevenLabsApiKey,
            language?.Code ?? string.Empty, cancellationToken);
        if (!ok || timed == null)
        {
            Se.WriteToolsLog($"ElevenLabs chunk: request failed ({cueTexts.Count} cues): {error}", true);
            return (new List<TtsResult>(), error);
        }

        var alignCount = Math.Min(timed.StartTimes.Length, timed.EndTimes.Length);
        if (alignCount == 0)
        {
            return (new List<TtsResult>(), "ElevenLabs returned no character timings.");
        }

        var t0 = new double[cueTexts.Count];
        var t1 = new double[cueTexts.Count];
        for (var i = 0; i < cueTexts.Count; i++)
        {
            var s = Math.Clamp(accentTag.Length + starts[i], 0, alignCount - 1);
            var e = Math.Clamp(accentTag.Length + ends[i], 0, alignCount - 1);
            t0[i] = timed.StartTimes[s];
            t1[i] = timed.EndTimes[e];
        }

        var chunkFile = Path.Combine(Path.GetTempPath(), "se-tts-chunk-" + Guid.NewGuid().ToString("N") + ".mp3");
        await File.WriteAllBytesAsync(chunkFile, timed.Audio, cancellationToken);

        var ffmpeg = FfmpegHelper.GetFfmpegLocation();
        var outputDir = TtsOutputFolder.Resolve(outputFolder, GetSetElevenLabsFolder);
        var results = new List<TtsResult>();
        // Cut placement. The endpoint's character timestamps are only approximate - they routinely
        // attribute the pause (and some of the next sentence's onset) to the last character of the
        // previous cue, so a cut derived from them either clips the next attack or leaks the next
        // sentence's opening letters into the previous clip. The timestamps are still good enough
        // to say WHICH pause belongs to each cue boundary, so detect the actual pauses on the
        // generated audio and cut in the middle of the pause nearest each boundary. Only a boundary
        // with no pause near it (a cue broken mid-sentence) keeps the timestamp guess.
        const double leadInSeconds = 0.09;
        const double snapRadiusSeconds = 0.8;

        IReadOnlyList<OpenAiSttChunker.SilenceInterval> silences;
        try
        {
            silences = await OpenAiSttChunker.DetectSilenceIntervalsAsync(ffmpeg, chunkFile, -35.0, 0.08, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SeLogger.Error(ex, "ElevenLabs chunk: silence detection failed - falling back to the timestamps");
            silences = Array.Empty<OpenAiSttChunker.SilenceInterval>();
        }

        var boundaries = new double[cueTexts.Count + 1];
        boundaries[0] = 0.0;
        for (var i = 1; i < cueTexts.Count; i++)
        {
            var approx = t0[i] - leadInSeconds;
            // Prefer the longest pause near this boundary: a sentence gap is longer than the short
            // intra-word silences the detector also reports.
            var snapped = silences
                .Where(s => Math.Abs(s.Midpoint - approx) <= snapRadiusSeconds)
                .OrderByDescending(s => s.DurationSeconds)
                .ThenBy(s => Math.Abs(s.Midpoint - approx))
                .FirstOrDefault();

            if (snapped != null)
            {
                boundaries[i] = snapped.Midpoint;
            }
            else
            {
                var boundary = Math.Min(approx, t0[i]);
                if (boundary < t1[i - 1])
                {
                    boundary = t1[i - 1];
                }

                boundaries[i] = boundary;
            }
        }

        boundaries[cueTexts.Count] = t1[cueTexts.Count - 1] + 0.30;

        // Keep the boundaries strictly increasing, so a snapped pause can never reorder the cues.
        for (var i = 1; i < boundaries.Length; i++)
        {
            if (boundaries[i] <= boundaries[i - 1])
            {
                boundaries[i] = boundaries[i - 1] + 0.05;
            }
        }

        Se.WriteToolsLog($"ElevenLabs chunk: {cueTexts.Count} cues, {silences.Count} pauses detected; cut boundaries = " +
                         string.Join(", ", boundaries.Select(b => b.ToString("0.00", CultureInfo.InvariantCulture))));

        try
        {
            for (var i = 0; i < cueTexts.Count; i++)
            {
                var start = boundaries[i];
                var end = boundaries[i + 1];
                if (end <= start)
                {
                    end = start + 0.05;
                }

                var fileName = Path.Combine(outputDir, Guid.NewGuid() + ".mp3");
                if (!await CutChunkAudioAsync(ffmpeg, chunkFile, start, end, fileName, cancellationToken))
                {
                    return (new List<TtsResult>(), "Could not cut the ElevenLabs chunk audio (ffmpeg failed).");
                }

                results.Add(new TtsResult { Text = cueTexts[i], FileName = fileName, RequestId = i == 0 ? timed.RequestId : string.Empty });
            }
        }
        finally
        {
            try { File.Delete(chunkFile); } catch { /* best-effort temp cleanup */ }
        }

        return (results, string.Empty);
    }

    private static async Task<bool> CutChunkAudioAsync(
        string ffmpegPath,
        string inputFileName,
        double startSeconds,
        double endSeconds,
        string outputFileName,
        CancellationToken cancellationToken)
    {
        var start = startSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        var duration = Math.Max(0.01, endSeconds - startSeconds).ToString("0.###", CultureInfo.InvariantCulture);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                // -ss before -i is the fast (keyframe) seek; re-encoding makes the result exact.
                Arguments = $"-nostdin -y -ss {start} -i \"{inputFileName}\" -t {duration} -vn -c:a libmp3lame -b:a 192k \"{outputFileName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        var exitCode = -1;
        try
        {
            await process.StartAndWaitAsync(cancellationToken, TimeSpan.FromMinutes(2));
            exitCode = process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SeLogger.Error(ex, "ElevenLabs chunk: ffmpeg cut failed");
            return false;
        }
        finally
        {
            process.Dispose();
        }

        return exitCode == 0 && File.Exists(outputFileName) && new FileInfo(outputFileName).Length > 0;
    }

    // The general accent as a leading tag for one request. Unlike ApplyGeneralAccent, which merges
    // it into the FIRST cue's own leading tag, the chunk prepends it once and leaves every cue's
    // text untouched - a merged tag only makes sense per line, and the point here is one continuous
    // reading of the whole run.
    private static string BuildAccentTag(string model)
    {
        var accent = Se.Settings.Video.TextToSpeech.ElevenLabsGeneralAccent?.Trim();
        if (string.IsNullOrEmpty(accent) || model is not ("eleven_v3" or "eleven_v4" or "eleven_v4_turbo"))
        {
            return string.Empty;
        }

        accent = accent.TrimStart('[').TrimEnd(']').Trim();
        return "[" + accent + "] ";
    }

    // Prepends the configured general accent tag to the leading tag group of the line, for the
    // models that read audio tags (v3 / v4 family). The accent is never stored in the text - it is
    // only merged into what is sent to the API.
    private static string ApplyGeneralAccent(string text, string model)
    {
        var accent = Se.Settings.Video.TextToSpeech.ElevenLabsGeneralAccent?.Trim();
        if (string.IsNullOrEmpty(accent) || model is not ("eleven_v3" or "eleven_v4" or "eleven_v4_turbo"))
        {
            return text;
        }

        accent = accent.TrimStart('[').TrimEnd(']').Trim();

        if (text.Length > 0 && text[0] == '[')
        {
            var close = text.IndexOf(']');
            if (close > 0)
            {
                var inner = text.Substring(1, close - 1).Trim();
                var merged = inner.Length == 0 ? accent : accent + " " + inner;
                return "[" + merged + "]" + text.Substring(close + 1);
            }
        }

        return "[" + accent + "] " + text;
    }

    public Task<string[]> GetRegions()
    {
        return Task.FromResult(Array.Empty<string>());
    }

    public Task<string[]> GetModels()
    {
        return Task.FromResult(new[]
        {
            "eleven_v4_turbo",
            "eleven_v4",
            "eleven_turbo_v2_5",
            "eleven_v3",
            "eleven_multilingual_v2",
            "eleven_turbo_v2_5"
        });
    }

    public bool ImportVoice(string fileName)
    {
        return false;
    }
}