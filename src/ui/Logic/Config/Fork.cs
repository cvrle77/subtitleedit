namespace Nikse.SubtitleEdit.Logic.Config;

/// <summary>
/// The cvrle77 fork's feature switches, in one place so every call site reads the same way:
/// <c>Fork.ReviewVideoPlayer</c> is the master switch AND that feature's own setting. Turning the
/// master off (<see cref="SeGeneral.ForkFeatures"/>) reverts every feature - and the bug fixes that
/// belong to it - to upstream Subtitle Edit behaviour.
/// </summary>
public static class Fork
{
    public static bool Enabled => Se.Settings.General.ForkFeatures;

    private static SeVideoTextToSpeech Tts => Se.Settings.Video.TextToSpeech;

    public static bool ReviewVideoPlayer => Enabled && Tts.ReviewVideoPlayer;

    public static bool ReviewAnchorMarker => Enabled && Tts.ReviewAnchorMarker;

    public static bool ReviewCredits => Enabled && Tts.ReviewCredits;

    public static bool ReviewTagPalette => Enabled && Tts.ReviewTagPalette;

    public static bool ReviewGridLook => Enabled && Tts.ReviewGridLook;

    public static bool ReviewSplitMerge => Enabled && Tts.ReviewSplitMerge;

    public static bool ReviewPlayheadTint => Enabled && Tts.ReviewPlayheadTint;

    public static bool ReviewPlayFromBlockStart => Enabled && Tts.ReviewPlayFromBlockStart;

    public static bool ReviewRegeneratePreviewOnly => Enabled && Tts.ReviewRegeneratePreviewOnly;

    public static bool ReviewTwoWaveforms => Enabled && Tts.ReviewTwoWaveforms;

    public static bool TtsCleanText => Enabled && Tts.TtsCleanText;

    public static bool ElevenLabsV4 => Enabled && Tts.ElevenLabsV4;

    public static bool ElevenLabsGeneralAccent => Enabled && Tts.ElevenLabsGeneralAccentEnabled;
}
