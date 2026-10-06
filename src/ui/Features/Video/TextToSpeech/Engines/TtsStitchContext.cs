using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.Engines;

/// <summary>
/// Prosody context for one ElevenLabs request, so the neighbouring lines condition how the
/// current one is spoken (ElevenLabs "request stitching"). <see cref="PreviousRequestIds"/> is the
/// strongest form: the ids of the immediately preceding generations (max 3, read from each
/// response's request-id header) let the model continue the actual previous audio. When they are
/// not available - the very first line, or the parallel path where no prior request has finished
/// yet - the plain <see cref="PreviousText"/>/<see cref="NextText"/> words still give the model the
/// surrounding context (the two are mutually exclusive on the API, so the ids take precedence).
/// </summary>
public sealed record TtsStitchContext(
    string PreviousText,
    string NextText,
    IReadOnlyList<string> PreviousRequestIds)
{
    public static readonly TtsStitchContext Empty = new(string.Empty, string.Empty, Array.Empty<string>());
}
