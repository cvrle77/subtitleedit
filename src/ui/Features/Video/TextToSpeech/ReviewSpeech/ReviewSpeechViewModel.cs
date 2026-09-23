using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nikse.SubtitleEdit.Controls.AudioVisualizerControl;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Main;
using Nikse.SubtitleEdit.Features.Shared;
using Nikse.SubtitleEdit.Features.Video.GoToVideoPosition;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.ActorVoices;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.DownloadTts;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.ElevenLabsSettings;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.Engines;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.Voices;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.VoiceCloneConsent;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.Logic.VideoPlayers.LibMpvDynamic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using ElevenLabsSettingsViewModel = Nikse.SubtitleEdit.Features.Video.TextToSpeech.ElevenLabsSettings.ElevenLabsSettingsViewModel;
using Timer = System.Timers.Timer;
using Nikse.SubtitleEdit.UiLogic.Media;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;

public partial class ReviewSpeechViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<ITtsEngine> _engines;
    [ObservableProperty] private ITtsEngine? _selectedEngine;
    [ObservableProperty] private ObservableCollection<Voice> _voices;
    [ObservableProperty] private Voice? _selectedVoice;
    [ObservableProperty] private ObservableCollection<TtsLanguage> _languages;
    [ObservableProperty] private TtsLanguage? _selectedLanguage;
    [ObservableProperty] private ObservableCollection<string> _regions;
    [ObservableProperty] private string? _selectedRegion;
    [ObservableProperty] private ObservableCollection<string> _models;
    [ObservableProperty] private string? _selectedModel;
    [ObservableProperty] private ObservableCollection<string> _styles;
    [ObservableProperty] private string? _selectedStyle;
    [ObservableProperty] private ObservableCollection<ReviewRow> _lines;
    [ObservableProperty] private ReviewRow? _selectedLine;
    // True whenever no regenerate is in flight. Must start true: OK/Export/Cancel and Escape
    // are gated on it, and the default false left them dead until the first regenerate ran.
    [ObservableProperty] private bool _isRegenerateEnabled = true;
    [ObservableProperty] private bool _isElevenLabsControlsVisible;
    [ObservableProperty] private bool _autoContinue;
    [ObservableProperty] private bool _isPlayVisible;
    [ObservableProperty] private bool _isStopVisible;
    // Waveform playhead as a time code, so a spot can be compared with the original video (#15211).
    [ObservableProperty] private string _positionText = FormatPosition(0);
    [ObservableProperty] private bool _isElevenLabsEngineV3Selected;
    // Whether the picked engine has a settings dialog. The knobs in there (emotion, speed,
    // instruction) change how a regenerated line sounds, so they belong next to Regenerate.
    [ObservableProperty] private bool _isEngineSettingsVisible;
    [ObservableProperty] private double _stability;
    [ObservableProperty] private double _similarity;
    [ObservableProperty] private double _speakerBoost;
    [ObservableProperty] private double _speed;
    [ObservableProperty] private double _styleExaggeration;

    // Voice-design controls mirrored from the main TTS window. Visibility is engine+model+voice
    // driven so the picker doesn't appear for engines that don't use it.
    [ObservableProperty] private bool _hasInstruction;
    [ObservableProperty] private bool _isInstructionTextVisible;
    [ObservableProperty] private bool _isInstructionPickerVisible;
    [ObservableProperty] private bool _isInstructionPickerEnabled;
    [ObservableProperty] private bool _isInstructionVoiceHintVisible;
    [ObservableProperty] private string _instruction = string.Empty;
    [ObservableProperty] private string _selectedOmniVoiceGender = OmniVoiceAny;
    [ObservableProperty] private string _selectedOmniVoiceAge = OmniVoiceAny;
    [ObservableProperty] private string _selectedOmniVoicePitch = OmniVoiceAny;
    [ObservableProperty] private string _selectedOmniVoiceAccent = OmniVoiceAny;
    [ObservableProperty] private bool _omniVoiceWhisper;

    public ObservableCollection<string> OmniVoiceGenders { get; } = BuildKeywordOptions(OmniVoiceTtsCpp.InstructionGenders);
    public ObservableCollection<string> OmniVoiceAges { get; } = BuildKeywordOptions(OmniVoiceTtsCpp.InstructionAges);
    public ObservableCollection<string> OmniVoicePitches { get; } = BuildKeywordOptions(OmniVoiceTtsCpp.InstructionPitches);
    public ObservableCollection<string> OmniVoiceAccents { get; } = BuildKeywordOptions(OmniVoiceTtsCpp.InstructionAccents);

    private const string OmniVoiceAny = "(any)";
    private bool _suppressKeywordSync;

    public Window? Window { get; set; }
    public TableView LineGrid { get; internal set; }
    public TextBox? EditTextBox { get; set; }
    public AudioVisualizer? AudioVisualizer { get; set; }

    // Second waveform stacked under the original one: the generated speech of every row placed at
    // its cue's start, on the same time axis. The window keeps it in sync with AudioVisualizer
    // (scroll, zoom, selection, playhead).
    public AudioVisualizer? AudioVisualizerTts { get; set; }
    public TtsStepResult[] StepResults { get; set; }

    // Source-of-truth peaks (of the original video audio) for the waveform shown next to the
    // review grid. May be null when no video is loaded — the visualizer then sits idle.
    [ObservableProperty] private WavePeakData2? _wavePeakData;

    // Composite peaks of every row's generated clip laid out at the row's cue start - the "TTS
    // wav" track under the original audio. Built in the background (see ScheduleTtsWaveformRebuild)
    // and rebuilt whenever clips or cue times change.
    [ObservableProperty] private WavePeakData2? _wavePeakDataTts;

    // End of the loaded video in seconds (0 = unknown). Drawn as a line on both waveform tracks so
    // a clip that runs past the last frame is visible; the merge trims that tail, which can cut
    // speech, so the user can drag the line's end back first.
    [ObservableProperty] private double _videoEndSeconds;

    // True when some cue ends after the video does - the end-of-video line turns red (a clip will be
    // trimmed), green when everything fits.
    [ObservableProperty] private bool _videoEndOverrun;

    // Each ReviewRow's paragraph projected as a SubtitleLineViewModel so AudioVisualizer drag
    // logic (which writes to SubtitleLineViewModel.StartTime/EndTime) works unchanged. The
    // canonical link is ReviewRow.WaveformParagraph (set in Initialize); this list is the
    // sorted-by-start-time view the visualizer needs, and the dictionary is the reverse lookup
    // used by OnWaveformParagraphChanged to find the row that owns a mirror VM.
    public List<SubtitleLineViewModel> WaveformParagraphs { get; } = new();
    private readonly Dictionary<SubtitleLineViewModel, ReviewRow> _waveformParagraphToRow = new();

    // Cast travelling with the audio. Populated by the main TTS VM via SetActorVoiceMappings;
    // round-tripped through SubtitleEditTts.json so a future Import re-applies the same voices.
    public List<ActorVoiceMapping> ActorVoiceMappings { get; private set; } = new();

    // Full path of the loaded subtitle file (empty for an unsaved subtitle). Export starts its
    // folder picker here so the session lands next to the subtitle instead of wherever the
    // picker was last used (#13881).
    public string SubtitleFileName { get; set; } = string.Empty;

    // What the video says during a paragraph - the transcript for a reference clip cut here (see
    // ResolvePerLineCloneVoiceAsync). Supplied by the TTS window, which knows the original-language
    // subtitle a translation was dubbed from; null when it does not, and the line's own text is
    // then the best guess left.
    public Func<Paragraph, string?>? ReferenceTextOf { get; set; }

    public bool OkPressed { get; private set; }

    // Text edits made in this window, published on OK so the caller can offer to apply them to
    // the main subtitle (#12093). Keyed by the rows' original time codes - see ReviewTextChange.
    public List<ReviewTextChange> TextChanges { get; private set; } = new();

    private readonly IFolderHelper _folderHelper;
    private readonly IWindowService _windowService;

    private LibMpvDynamicPlayer? _mpvContext;
    private Lock _playLock;
    private readonly Timer _timer;
    private UiTickPump? _cursorTimer;
    private volatile bool _isClosing;
    private string _videoFileName;
    private string _waveFolder;
    private CancellationTokenSource _cancellationTokenSource;
    private CancellationToken _cancellationToken;
    private bool _skipAutoContinue;
    private long _startPlayTicks;
    private readonly List<string> _tempAudioFiles = new();

    // The row whose audio is currently playing. Auto-continue must advance from this row, not
    // from SelectedLine - grid selection is two-way and can move during playback.
    private ReviewRow? _playingRow;

    public ReviewSpeechViewModel(IFolderHelper folderHelper, IWindowService windowService)
    {
        _folderHelper = folderHelper;
        _windowService = windowService;

        LineGrid = new TableView();
        Lines = new ObservableCollection<ReviewRow>();
        Engines = new ObservableCollection<ITtsEngine>();
        Voices = new ObservableCollection<Voice>();
        Languages = new ObservableCollection<TtsLanguage>();
        Regions = new ObservableCollection<string>();
        Models = new ObservableCollection<string>();
        Styles = new ObservableCollection<string>();
        StepResults = [];

        Stability = Se.Settings.Video.TextToSpeech.ElevenLabsStability;
        Similarity = Se.Settings.Video.TextToSpeech.ElevenLabsSimilarity;
        SpeakerBoost = Se.Settings.Video.TextToSpeech.ElevenLabsSpeakerBoost;
        Speed = Se.Settings.Video.TextToSpeech.ElevenLabsSpeed;
        StyleExaggeration = Se.Settings.Video.TextToSpeech.ElevenLabsStyleeExaggeration;

        IsPlayVisible = true;
        _videoFileName = string.Empty;
        _waveFolder = string.Empty;
        _cancellationTokenSource = new CancellationTokenSource();
        _cancellationToken = _cancellationTokenSource.Token;

        _playLock = new Lock();
        // 100 ms: drives the playback state machine (auto-continue, play/stop UI). The waveform
        // playhead/scroll is driven separately at ~60 fps by _cursorTimer - 100 ms looked steppy.
        _timer = new Timer(100);
        _timer.Elapsed += OnTimerOnElapsed;
        _timer.Start();

        // ~60 fps, same as the main window's waveform cursor. Reading mpv's position here (instead
        // of on the 100 ms timer) is what makes the playhead and the center-scroll smooth.
        _cursorTimer = new UiTickPump(TimeSpan.FromMilliseconds(16), CursorTick, DispatcherPriority.Normal);
    }

    private async void OnTimerOnElapsed(object? sender, ElapsedEventArgs args)
    {
        if (_isClosing)
        {
            return;
        }

        try
        {
            _timer.Stop();

            // Read mpv state under the play lock: Stop()/OnClosing dispose _mpvContext under the
            // same lock on the UI thread, and this tick runs on a threadpool thread - the old
            // unguarded IsPaused read raced the dispose (NRE / native use-after-free window).
            var stopped = false;
            var paused = false;
            var positionSeconds = 0.0;
            lock (_playLock)
            {
                if (_cancellationTokenSource.IsCancellationRequested || _mpvContext == null)
                {
                    stopped = true;
                }
                else
                {
                    paused = _mpvContext.IsPaused;
                    positionSeconds = _mpvContext.Position;
                }
            }

            if (stopped)
            {
                await Dispatcher.UIThread.InvokeAsync(ResetPlaybackUiState);
                return;
            }

            // The playhead is driven by _cursorTimer at ~60 fps; this 100 ms tick only runs the
            // state machine (auto-continue, play/stop UI), so it deliberately does not move it -
            // doing both here is what forced the cursor into 100 ms steps.

            // The row that is actually playing - not SelectedLine: two-way grid selection meant
            // clicking another row during playback made auto-continue advance from the *clicked*
            // row, stranding the playing row's stop icon and skipping the clicked one.
            var line = _playingRow;
            var timeSinceStart = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _startPlayTicks);
            if (paused && AutoContinue && !_skipAutoContinue && line != null && timeSinceStart.TotalMilliseconds > 500)
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    // Re-check on the UI thread: Stop/Space pressed between this tick's threadpool
                    // checks and this lambda running used to be ignored - the advance started a
                    // fresh mpv for the next row while the UI had already reset to idle, leaving
                    // audio playing with nothing able to stop it. The timer is already stopped and
                    // this lambda is the last thing to run, so it must also reset the playback UI:
                    // a bare return left the finished row's stop icon showing and every row
                    // disabled, with no way to recover in the window.
                    if (_skipAutoContinue || _cancellationTokenSource.IsCancellationRequested || !ReferenceEquals(_playingRow, line))
                    {
                        ResetPlaybackUiState();
                        return;
                    }

                    line.IsPlaying = false;
                    var index = Lines.IndexOf(line);
                    if (index >= 0 && index < Lines.Count - 1)
                    {
                        var nextLine = Lines[index + 1];
                        nextLine.IsPlaying = true;
                        _playingRow = nextLine;
                        SelectedLine = nextLine;
                        LineGrid.ScrollIntoView(nextLine);
                        await PlayAudio(nextLine.StepResult.CurrentFileName);
                    }
                    else
                    {
                        _skipAutoContinue = true; // no more lines to play
                        ResetPlaybackUiState();
                    }
                });

                return;
            }

            if (paused)
            {
                await Dispatcher.UIThread.InvokeAsync(ResetPlaybackUiState);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsPlayVisible = false;
                IsStopVisible = true;
            });

            // OnClosing may have disposed the timer while this async handler awaited; Start() on a
            // disposed timer throws ObjectDisposedException (no longer swallowed on modern .NET). (#12739)
            if (!_isClosing)
            {
                _timer.Start();
            }
        }
        catch (Exception ex)
        {
            SeLogger.Error(ex, "Error in ReviewSpeech playback timer.");
            // A throw used to kill the timer for good, leaving every row disabled (PlayRow
            // disables them and only this timer re-enables) - reset instead of wedging.
            Dispatcher.UIThread.Post(ResetPlaybackUiState);
        }
    }

    /// <summary>
    /// Returns the playback UI to idle: play button showing, no row marked as playing, all
    /// rows clickable again. Must run on the UI thread.
    /// </summary>
    private void ResetPlaybackUiState()
    {
        IsPlayVisible = true;
        IsStopVisible = false;
        _playingRow = null;
        _cursorTimer?.Stop();
        foreach (var l in Lines)
        {
            l.IsPlaying = false;
            l.IsPlayingEnabled = true;
        }
    }

    private async Task PlayAudio(string fileName)
    {
        // A row can point at a deleted/moved file (e.g. an imported session whose folder was
        // cleaned). mpv's failed loadfile is only logged, so playback used to sit in "playing"
        // forever with every row disabled and no error. Reset and tell the user instead.
        if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
        {
            SeLogger.Error($"ReviewSpeech: cannot play missing audio file \"{fileName}\"");
            ResetPlaybackUiState();
            if (Window != null)
            {
                await MessageBox.Show(
                    Window,
                    Se.Language.General.Error,
                    "The audio file for this line does not exist:" + Environment.NewLine + fileName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return;
        }

        try
        {
            // Retire the previous row's core on a worker thread - disposing it inline here runs
            // mpv_terminate_destroy on the UI thread, and every row-to-row transition (including
            // the auto-continue timer path) would corrupt state that later blows up as an access
            // violation in IFrameworkInputPane.Unadvise when a window closes (#13567, #13376).
            DisposePlayerOffThread();

            Se.WriteToolsLog($"TTS review: creating mpv core to play \"{fileName}\"");
            LibMpvDynamicPlayer player;
            lock (_playLock)
            {
                player = new LibMpvDynamicPlayer();
                player.LoadLib();
                var err = player.Initialize();
                if (err < 0)
                {
                    throw new InvalidOperationException($"Failed to initialize mpv: {player.GetErrorString(err)}");
                }

                _mpvContext = player;
            }

            // Through the local: a close running now can null the field out from under us.
            await player.LoadAudio(fileName);
        }
        catch (Exception exception)
        {
            // An mpv load/init failure must not escape: the callers disable all rows before
            // awaiting and only the playback timer re-enables them, so an unhandled throw
            // leaves every play/regenerate button dead until the window is reopened.
            SeLogger.Error(exception, $"ReviewSpeech: unable to play audio file \"{fileName}\"");
            ResetPlaybackUiState();
            if (Window != null)
            {
                await MessageBox.Show(
                    Window,
                    Se.Language.General.Error,
                    "Unable to play audio: " + exception.Message,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return;
        }

        _timer.Start();
        _cursorTimer?.Start();
    }

    internal void Initialize(
        TtsStepResult[] stepResults,
        ITtsEngine[] engines,
        ITtsEngine engine,
        Voice[] voices,
        Voice? voice,
        TtsLanguage[] languages,
        TtsLanguage? language,
        string videoFileName,
        string waveFolder,
        WavePeakData2? wavePeakData)
    {
        foreach (var p in stepResults)
        {
            var row = new ReviewRow
            {
                Include = p.Include,
                Number = p.Paragraph.Number,
                // The subtitle's own text, not the tag-stripped/unbroken copy that was fed to
                // the engine: edits made here are published back to the main subtitle, so
                // starting from the stripped copy silently dropped italics and line breaks
                // from every line the user touched. Synthesis strips at the point of use.
                Text = p.Paragraph.Text,
                Voice = p.Voice == null ? string.Empty : p.Voice.ToString(),
                Speed = Math.Round(p.SpeedFactor, 2).ToString(CultureInfo.CurrentCulture),
                Cps = Math.Round(p.Paragraph.GetCharactersPerSecond(), 2).ToString(CultureInfo.CurrentCulture),
                StepResult = p,
                OriginalText = p.Paragraph.Text,
                OriginalStartMs = p.Paragraph.StartTime.TotalMilliseconds,
                OriginalEndMs = p.Paragraph.EndTime.TotalMilliseconds,
            };
            row.StartHistory();
            Lines.Add(row);

            // Mirror this row's paragraph as a SubtitleLineViewModel so the AudioVisualizer can
            // draw + drag it. The visualizer's drag handlers mutate StartTime/EndTime on the VM,
            // so OnWaveformParagraphChanged below forwards those edits back to row.StepResult.Paragraph.
            var waveformParagraph = new SubtitleLineViewModel
            {
                Number = p.Paragraph.Number,
                Text = p.Text,
                StartTime = TimeSpan.FromMilliseconds(p.Paragraph.StartTime.TotalMilliseconds),
                EndTime = TimeSpan.FromMilliseconds(p.Paragraph.EndTime.TotalMilliseconds),
            };
            waveformParagraph.UpdateDuration();
            waveformParagraph.PropertyChanged += OnWaveformParagraphChanged;
            row.WaveformParagraph = waveformParagraph;
            WaveformParagraphs.Add(waveformParagraph);
            _waveformParagraphToRow[waveformParagraph] = row;
        }

        // The caller passes either real peaks of the source video, an empty placeholder (when
        // there's no video yet), or null. The visualizer renders nothing when peaks are missing;
        // background generation in TextToSpeechViewModel.Import pushes real peaks into this
        // property once ffmpeg finishes, at which point the binding refreshes the visualizer.
        WavePeakData = wavePeakData;

        // Build the generated-speech track for the second waveform (background - reading every
        // clip can take a moment on a long session).
        ScheduleTtsWaveformRebuild();

        // Shared with the Cast dialog (see ActorVoiceDetector.FilterUsableEngines) so the two
        // windows always show the same set of usable engines. Add new engine availability rules
        // there, not here.
        foreach (var engineItem in ActorVoiceDetector.FilterUsableEngines(engines))
        {
            Engines.Add(engineItem);
        }

        SelectedEngine = engine;

        Voices.AddRange(voices);
        SelectedVoice = voice;

        Languages.AddRange(languages);
        SelectedLanguage = language;

        _videoFileName = videoFileName;
        _waveFolder = waveFolder;

        // End-of-video marker for the waveform. Read once - it does not change for the session.
        // 0 when there is no video or ffmpeg cannot read it (no line is drawn).
        VideoEndSeconds = GetVideoDurationSeconds();
        UpdateVideoEndOverrun();

        if (Lines.Count > 0)
        {
            SelectedLine = Lines[0];
            LineGrid.SelectedIndex = 0;
            LineGrid.ScrollIntoView(Lines[0]);
        }
    }

    // Duration of the loaded video in seconds, 0 when there is no video or it cannot be read.
    private double GetVideoDurationSeconds()
    {
        try
        {
            if (!string.IsNullOrEmpty(_videoFileName) && File.Exists(_videoFileName))
            {
                return FfmpegMediaInfo2.Parse(_videoFileName).Duration?.TotalSeconds ?? 0;
            }
        }
        catch (Exception exception)
        {
            SeLogger.Error(exception, $"ReviewSpeech: cannot read the video duration of \"{_videoFileName}\"");
        }

        return 0;
    }

    // Drag/edit done on the waveform mutates the SubtitleLineViewModel mirror; this writes the
    // new times back to the underlying TtsStepResult.Paragraph so OK/Export see the change.
    private void OnWaveformParagraphChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SubtitleLineViewModel waveformParagraph)
        {
            return;
        }

        if (e.PropertyName != nameof(SubtitleLineViewModel.StartTime) &&
            e.PropertyName != nameof(SubtitleLineViewModel.EndTime))
        {
            return;
        }

        if (!_waveformParagraphToRow.TryGetValue(waveformParagraph, out var row))
        {
            return;
        }

        var paragraph = row.StepResult.Paragraph;
        paragraph.StartTime = new TimeCode(waveformParagraph.StartTime);
        paragraph.EndTime = new TimeCode(waveformParagraph.EndTime);

        // Cps depends on duration; refresh so the grid stays consistent with the dragged times.
        row.Cps = Math.Round(paragraph.GetCharactersPerSecond(), 2).ToString(CultureInfo.CurrentCulture);

        // The other waveform shows the same cue block - repaint it too so a drag on one control
        // moves the block on both.
        InvalidateWaveforms();
    }

    // The original-audio visualizer plus, when the window built it, the generated-speech one.
    private IEnumerable<AudioVisualizer> WaveformControls()
    {
        if (AudioVisualizer != null)
        {
            yield return AudioVisualizer;
        }

        if (AudioVisualizerTts != null)
        {
            yield return AudioVisualizerTts;
        }
    }

    // Copies the view state (scroll, zoom, playhead) from one waveform to the other so the two
    // stay on the same time axis. Guarded against re-entry: assigning a property raises the other
    // control's PropertyChanged, which would otherwise bounce straight back.
    private bool _syncingWaveformView;

    public void SyncWaveformView(AudioVisualizer source, AudioVisualizer target)
    {
        if (_syncingWaveformView || ReferenceEquals(source, target))
        {
            return;
        }

        _syncingWaveformView = true;
        try
        {
            target.ZoomFactor = source.ZoomFactor;
            target.VerticalZoomFactor = source.VerticalZoomFactor;
            target.StartPositionSeconds = source.StartPositionSeconds;

            // The playhead is NOT copied here: SetWaveformPlayhead writes CurrentVideoPositionSeconds
            // on both controls directly each tick, and copying it back through this path only fed a
            // feedback loop (the other control would re-raise for the value it already had and both
            // ended up drifting off the same position).
        }
        finally
        {
            _syncingWaveformView = false;
        }
    }

    // State of the smooth "keep the play-head centered" scroll - the same magnet the main window
    // uses. While it runs the view eases to the center over the configured time instead of
    // snapping, so a clip ending and the next one starting does not jerk the timeline. The target
    // jumps on every clip change (start time of the new cue); when that happens the ease restarts
    // from wherever the view is now, so the block still glides into the center.
    private bool _centerAnimActive;
    private long _centerAnimStartTicks;
    private double _centerAnimFromSeconds;
    private double _centerAnimTargetSeconds;

    // Called from the playback timer (and on any paused position change) to move the playhead and
    // keep the view centered on it. The original waveform is the one that owns StartPositionSeconds
    // here; the generated-speech one follows it through SyncWaveformView.
    private void SetWaveformPlayhead(double seconds)
    {
        var av = AudioVisualizer;
        if (av == null)
        {
            return;
        }

        // Both tracks show the same cursor. It is written here directly (not copied through
        // SyncWaveformView) so the generated-speech track gets it too - the sync only carries the
        // scroll/zoom, and leaving the cursor out of it there meant the second track's playhead
        // never moved.
        av.CurrentVideoPositionSeconds = seconds;
        var avTts = AudioVisualizerTts;
        if (avTts != null)
        {
            avTts.CurrentVideoPositionSeconds = seconds;
        }

        // Center mode is opt-in (Se.Settings.Waveform.CenterVideoPosition). Without it the view
        // stays put and only the cursor moves, exactly as before this change.
        if (Se.Settings.Waveform.CenterVideoPosition && av.WavePeaks != null)
        {
            var halfSeconds = (av.EndPositionSeconds - av.StartPositionSeconds) / 2.0;
            var centerTarget = Math.Max(0, seconds - halfSeconds);
            var centerSuspended = av.IsEditingWithPointer || av.IsMouseWheelInteracting;

            if (centerSuspended)
            {
                _centerAnimActive = false;
            }
            else
            {
                // The target jumped (a seek, the next clip) while the ease was still running:
                // restart it from where the view is now. Carrying the old progress onto the new
                // target snapped the view the rest of the way at once - the skip the user sees
                // when one clip ends and the next begins.
                if (_centerAnimActive && Math.Abs(centerTarget - _centerAnimTargetSeconds) > 0.15)
                {
                    _centerAnimActive = false;
                }

                if (!_centerAnimActive && Math.Abs(av.StartPositionSeconds - centerTarget) > 0.15)
                {
                    _centerAnimActive = true;
                    _centerAnimFromSeconds = av.StartPositionSeconds;
                    _centerAnimStartTicks = Stopwatch.GetTimestamp();
                }

                _centerAnimTargetSeconds = centerTarget;

                if (_centerAnimActive)
                {
                    var duration = Math.Max(0.1, Se.Settings.Waveform.CenterSmoothSeconds);
                    var progress = (Stopwatch.GetTimestamp() - _centerAnimStartTicks) / (double)Stopwatch.Frequency / duration;
                    if (progress >= 1)
                    {
                        _centerAnimActive = false;
                        av.StartPositionSeconds = centerTarget;
                    }
                    else
                    {
                        // Ease-out: quick at first, settling as it reaches the center.
                        var eased = 1 - Math.Pow(1 - progress, 3);
                        av.StartPositionSeconds = _centerAnimFromSeconds + (centerTarget - _centerAnimFromSeconds) * eased;
                    }
                }
                else
                {
                    av.StartPositionSeconds = centerTarget;
                }

                SyncWaveformView(av, AudioVisualizerTts);
            }
        }
        else
        {
            _centerAnimActive = false;
        }

        av.InvalidateVisual();
        AudioVisualizerTts?.InvalidateVisual();
    }

    // ~60 fps tick whose only job is moving the playhead (and, in center mode, easing the scroll)
    // while a clip plays. Runs on the UI thread (UiTickPump posts to the dispatcher), so it can
    // read mpv directly and write the visualizer without a per-frame InvokeAsync hop - the 100 ms
    // timer was the reason the playhead moved in ~3-10 fps jumps.
    private void CursorTick()
    {
        if (_isClosing)
        {
            return;
        }

        var playingRow = _playingRow;
        if (playingRow == null)
        {
            return;
        }

        double positionSeconds;
        lock (_playLock)
        {
            if (_cancellationTokenSource.IsCancellationRequested || _mpvContext == null || _mpvContext.IsPaused)
            {
                return;
            }

            positionSeconds = _mpvContext.Position;
        }

        var waveformParagraph = playingRow.WaveformParagraph;
        if (waveformParagraph == null)
        {
            return;
        }

        SetWaveformPlayhead(waveformParagraph.StartTime.TotalSeconds + positionSeconds);
    }

    private void InvalidateWaveforms()
    {
        UpdateVideoEndOverrun();
        foreach (var av in WaveformControls())
        {
            av.InvalidateVisual();
        }
    }

    // Recomputes whether any cue ends after the video, so the end-of-video line's colour can turn
    // red (overrun) or green. Tracks the row times, not the generated clip length (the clip is
    // what gets placed and trimmed).
    private void UpdateVideoEndOverrun()
    {
        var videoEnd = VideoEndSeconds;
        if (videoEnd <= 0)
        {
            VideoEndOverrun = false;
            return;
        }

        var overrun = false;
        foreach (var row in Lines)
        {
            var paragraph = row.StepResult.Paragraph;
            if (paragraph.EndTime.TotalSeconds > videoEnd + 0.001)
            {
                overrun = true;
                break;
            }
        }

        VideoEndOverrun = overrun;
    }

    // Length of each row's generated clip, keyed by file name: a regenerate always writes a new
    // file, so a stale entry can never be served for a changed clip. Non-WAV or unreadable files
    // yield 0, which the visualizer treats as "no bar".
    private readonly Dictionary<string, double> _audioLengthCache = new();

    public double GetGeneratedAudioLengthSeconds(ReviewRow row)
    {
        var fileName = row.StepResult.CurrentFileName;
        if (string.IsNullOrEmpty(fileName))
        {
            return 0;
        }

        if (_audioLengthCache.TryGetValue(fileName, out var cached))
        {
            return cached;
        }

        var seconds = 0.0;
        try
        {
            if (File.Exists(fileName))
            {
                using var stream = File.OpenRead(fileName);
                var header = new WaveHeader2(stream);
                if (header.ChunkId == "RIFF" && header.Format == "WAVE" && header.BytesPerSecond > 0)
                {
                    seconds = header.LengthInSeconds;
                }
            }
        }
        catch (Exception exception)
        {
            SeLogger.Error(exception, $"ReviewSpeech: cannot read audio length of \"{fileName}\"");
        }

        _audioLengthCache[fileName] = seconds;
        return seconds;
    }

    // Provider for AudioVisualizer.ParagraphAudioLengthProvider - the visualizer hands back the
    // mirror instance it draws, so an instance lookup is right here (unlike the event args).
    public double GetWaveformParagraphAudioLength(SubtitleLineViewModel waveformParagraph)
    {
        return _waveformParagraphToRow.TryGetValue(waveformParagraph, out var row) ? GetGeneratedAudioLengthSeconds(row) : 0;
    }

    // Peaks of each generated clip, keyed by file name. A regenerate always writes a new file, so
    // an entry can never go stale for a changed clip; the cache is what makes a rebuild cheap
    // (only the placement is redone, not the wav reads).
    private readonly Dictionary<string, WavePeakData2?> _ttsClipPeaks = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _ttsWaveformCts;
    // Peaks-per-second the generated track is (re)built at - the original track's rate. Published
    // to AudioVisualizerTts.FallbackSampleRate so the two controls measure time identically even
    // before the track is built.
    private int _ttsTargetSampleRate = Se.Settings.Waveform.WaveformMinimumSampleRate;

    private WavePeakData2? GetClipPeaks(string fileName)
    {
        if (_ttsClipPeaks.TryGetValue(fileName, out var cached))
        {
            return cached;
        }

        WavePeakData2? peaks = null;
        try
        {
            // Only WAVs can be read for peaks; cloud engines can hand back mp3 - those rows simply
            // get no waveform instead of failing the whole build.
            if (File.Exists(fileName) && fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                using var generator = new WavePeakGenerator2(fileName);
                if (generator.IsSupported)
                {
                    peaks = generator.GeneratePeaks(0, string.Empty);
                }
            }
        }
        catch (Exception exception)
        {
            SeLogger.Error(exception, $"ReviewSpeech: cannot read wave peaks of \"{fileName}\"");
        }

        _ttsClipPeaks[fileName] = peaks;
        return peaks;
    }

    // Rebuilds the generated-speech track from the rows' clips, on a worker thread (reading every
    // clip can take a moment on a long session). Called on load, after a regenerate, and when cue
    // times change; the in-memory clip-peak cache keeps the repeated calls cheap.
    public void ScheduleTtsWaveformRebuild()
    {
        if (_isClosing)
        {
            return;
        }

        // Snapshot on the UI thread - the background build must not walk the observable rows.
        var placements = new List<(double StartSeconds, string FileName)>();
        foreach (var row in Lines)
        {
            var waveformParagraph = row.WaveformParagraph;
            var fileName = row.StepResult.CurrentFileName;
            if (waveformParagraph != null && !string.IsNullOrEmpty(fileName))
            {
                placements.Add((waveformParagraph.StartTime.TotalSeconds, fileName));
            }
        }

        // Same peaks-per-second as the original waveform so the two share a time base; a video
        // length to pad to so scrolling clamps at the same place on both controls.
        var targetRate = WavePeakData is { SampleRate: > 0 }
            ? WavePeakData.SampleRate
            : Se.Settings.Waveform.WaveformMinimumSampleRate;
        var totalSeconds = WavePeakData?.LengthInSeconds ?? 0;

        // The generated track always has a time base (never a shorter one than the original), so
        // the shared scroll/zoom and the playhead behave the same on both controls even before any
        // peaks exist.
        _ttsTargetSampleRate = targetRate;
        if (AudioVisualizerTts != null)
        {
            AudioVisualizerTts.FallbackSampleRate = targetRate;
        }

        _ttsWaveformCts?.Cancel();
        _ttsWaveformCts?.Dispose();
        var cts = new CancellationTokenSource();
        _ttsWaveformCts = cts;

        _ = Task.Run(() => BuildTtsWaveform(placements, targetRate, totalSeconds, cts.Token));
    }

    private void BuildTtsWaveform(List<(double StartSeconds, string FileName)> placements, int targetRate, double totalSeconds, CancellationToken token)
    {
        try
        {
            var clips = new List<(int Offset, WavePeakData2 Peaks)>();
            foreach (var (startSeconds, fileName) in placements)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var peaks = GetClipPeaks(fileName);
                if (peaks == null || peaks.Peaks.Count == 0)
                {
                    continue;
                }

                var offset = (int)Math.Round(startSeconds * targetRate);
                clips.Add((offset, peaks));
                totalSeconds = Math.Max(totalSeconds, startSeconds + peaks.LengthInSeconds);
            }

            WavePeakData2? result;
            if (clips.Count == 0)
            {
                // Nothing generated yet: a silent track of the video's length keeps the two
                // waveforms on the same time axis. A short one is published when even that length
                // is unknown. The single sentinel peak at the very start keeps HighestPeak > 0 -
                // the per-pixel draw scales by 1/HighestPeak and an all-zero track would divide by
                // zero (this mirrors WavePeakGenerator2.GenerateEmptyPeaks).
                var emptySeconds = Math.Max(totalSeconds, 1.0);
                var emptyPeaks = new WavePeak2[(int)Math.Ceiling(emptySeconds * targetRate)];
                if (emptyPeaks.Length > 0)
                {
                    emptyPeaks[0] = new WavePeak2(1000, -1000);
                }

                result = new WavePeakData2(targetRate, emptyPeaks);
            }
            else
            {
                // Never shorter than the original: the counts below use totalSeconds, which the
                // loop above has already raised to cover every clip.
                var count = (int)Math.Ceiling(totalSeconds * targetRate);
                var peaks = new WavePeak2[count];
                foreach (var (offset, clip) in clips)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    var clipRate = clip.SampleRate;
                    var span = clip.AsSpan();
                    for (var i = 0; i < span.Length; i++)
                    {
                        var targetIndex = offset + (int)Math.Round((double)i * targetRate / clipRate);
                        if (targetIndex < 0 || targetIndex >= count)
                        {
                            continue;
                        }

                        var peak = span[i];
                        var existing = peaks[targetIndex];

                        // Overlapping cues: keep the louder peak so both are visible.
                        if (Math.Abs((int)peak.Max) + Math.Abs((int)peak.Min) >
                            Math.Abs((int)existing.Max) + Math.Abs((int)existing.Min))
                        {
                            peaks[targetIndex] = peak;
                        }
                    }
                }

                result = new WavePeakData2(targetRate, peaks);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (!token.IsCancellationRequested && !_isClosing)
                {
                    WavePeakDataTts = result;
                }
            });
        }
        catch (Exception exception)
        {
            SeLogger.Error(exception, "ReviewSpeech: building the generated-speech waveform failed");
        }
    }

    // "Fit duration to generated audio": the cue ends exactly where the speech ends - what the
    // user otherwise does by dragging the right edge to the end of the red overrun bar.
    [RelayCommand]
    private void FitDurationToAudio(ReviewRow? row)
    {
        row ??= SelectedLine;
        var wp = row?.WaveformParagraph;
        if (row == null || wp == null)
        {
            return;
        }

        var seconds = GetGeneratedAudioLengthSeconds(row);
        if (seconds <= 0)
        {
            return;
        }

        wp.EndTime = wp.StartTime + TimeSpan.FromSeconds(seconds);
        wp.UpdateDuration();
        InvalidateWaveforms();
        ScheduleTtsWaveformRebuild();
    }

    // Restores the times the line had when the window opened (waveform drags have no undo).
    [RelayCommand]
    private void ResetTiming(ReviewRow? row)
    {
        row ??= SelectedLine;
        var wp = row?.WaveformParagraph;
        if (row == null || wp == null)
        {
            return;
        }

        wp.StartTime = TimeSpan.FromMilliseconds(row.OriginalStartMs);
        wp.EndTime = TimeSpan.FromMilliseconds(row.OriginalEndMs);
        wp.UpdateDuration();
        InvalidateWaveforms();
        ScheduleTtsWaveformRebuild();
    }

    // Shifts the selected cue as a whole (duration kept), used by the waveform's keyboard nudge.
    private void NudgeSelectedLine(double milliseconds)
    {
        var wp = SelectedLine?.WaveformParagraph;
        if (wp == null)
        {
            return;
        }

        var start = Math.Max(0, wp.StartTime.TotalMilliseconds + milliseconds);
        var duration = wp.Duration.TotalMilliseconds;
        wp.StartTime = TimeSpan.FromMilliseconds(start);
        wp.EndTime = TimeSpan.FromMilliseconds(start + duration);
        wp.UpdateDuration();
        InvalidateWaveforms();
        ScheduleTtsWaveformRebuild();
    }

    // Right-click on the waveform: the row under the pointer becomes the context-menu target
    // (and the selection) whether or not "right click selects" is on; an empty-area right-click
    // keeps the current selection. Returns the target row or null.
    public ReviewRow? SelectRowAtWaveformPosition(double seconds)
    {
        var wp = WaveformParagraphs.Find(p => p.StartTime.TotalSeconds <= seconds && seconds <= p.EndTime.TotalSeconds);
        if (wp != null)
        {
            SelectFromWaveform(wp);
        }

        return SelectedLine;
    }

    public void UpdatePositionText(double seconds)
    {
        PositionText = FormatPosition(seconds);
    }

    // Same text as the main window's video position: follows frame mode and the video offset.
    private static string FormatPosition(double seconds)
    {
        return TimeCode.FromSeconds(seconds + Se.Settings.General.CurrentVideoOffsetInMs / 1000.0).ToDisplayString();
    }

    // Opened by clicking the position text or the main window's "Go to video position" shortcut
    // (#15211). The dialog works in displayed time, so the video offset goes on and comes off here.
    [RelayCommand]
    private async Task ShowGoToPosition()
    {
        var av = AudioVisualizer;
        if (Window == null || av == null || WavePeakData == null)
        {
            return;
        }

        var offsetSeconds = Se.Settings.General.CurrentVideoOffsetInMs / 1000.0;
        var result = await _windowService.ShowDialogAsync<GoToVideoPositionWindow, GoToVideoPositionViewModel>(Window,
            vm => vm.Time = TimeSpan.FromSeconds(av.CurrentVideoPositionSeconds + offsetSeconds));
        if (!result.OkPressed || result.Time.TotalMicroseconds < 0)
        {
            return;
        }

        GoToPosition(result.Time.TotalSeconds - offsetSeconds);
    }

    internal void GoToPosition(double seconds)
    {
        var av = AudioVisualizer;
        if (av == null)
        {
            return;
        }

        if (_playingRow != null)
        {
            Stop();
        }

        seconds = Math.Max(0, seconds);
        if (seconds < av.StartPositionSeconds || seconds > av.EndPositionSeconds)
        {
            // Same 2 s lead-in as selecting a row, so the spot isn't glued to the left edge.
            av.StartPositionSeconds = Math.Max(0, seconds - 2.0);
        }

        SetWaveformPlayhead(seconds);
    }

    // A left-click on empty waveform just parks the playhead there; the review window has no
    // video to seek, so nothing plays until the user presses Play.
    public void OnWaveformPositionClicked(double seconds)
    {
        if (_playingRow == null)
        {
            SetWaveformPlayhead(seconds);
        }
    }

    // Keys that act on the waveform when it has focus (the grid handles its own Up/Down):
    // Home/End jump to the first/last row; Ctrl+Left/Right nudge the selected cue 100 ms
    // (10 ms with Shift) without changing its duration.
    public bool OnWaveformKeyDown(KeyEventArgs e)
    {
        if (Lines.Count == 0)
        {
            return false;
        }

        var ctrl = e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (e.Key == Key.Home && e.KeyModifiers == KeyModifiers.None)
        {
            SelectedLine = Lines[0];
            LineGrid.ScrollIntoView(Lines[0]);
            return true;
        }

        if (e.Key == Key.End && e.KeyModifiers == KeyModifiers.None)
        {
            SelectedLine = Lines[^1];
            LineGrid.ScrollIntoView(Lines[^1]);
            return true;
        }

        if (ctrl && e.Key is Key.Left or Key.Right)
        {
            var step = shift ? 10 : 100;
            NudgeSelectedLine(e.Key == Key.Left ? -step : step);
            return true;
        }

        return false;
    }

    // True while a selection change originates from a click/drag on the waveform itself. The
    // block the user grabbed is already in view, so RefreshWaveformPosition must only update the
    // highlight and not recenter - recentering would jump the view (and the block under the
    // pointer) mid-drag (#14000).
    private bool _selectionFromWaveform;

    // Click/drag on a waveform block selects the row that owns it, which in turn loads its text
    // and per-line settings into the edit panel through OnSelectedLineChanged (#14000).
    //
    // The event args carry a *copy* of the paragraph (ParagraphEventArgs clones it), so the
    // lookup goes by Id, which the copy preserves - never by instance.
    public void SelectFromWaveform(SubtitleLineViewModel? waveformParagraph)
    {
        if (waveformParagraph == null)
        {
            return;
        }

        var mirror = WaveformParagraphs.Find(wp => wp.Id == waveformParagraph.Id);
        if (mirror == null ||
            !_waveformParagraphToRow.TryGetValue(mirror, out var row) ||
            ReferenceEquals(row, SelectedLine))
        {
            return;
        }

        _selectionFromWaveform = true;
        try
        {
            SelectedLine = row;
            LineGrid.ScrollIntoView(row);
        }
        finally
        {
            _selectionFromWaveform = false;
        }
    }

    // Centers both visualizers on the currently selected paragraph and marks it as selected so the
    // user can grab its start/end handles. Safe to call before the visualizers are attached.
    public void RefreshWaveformPosition()
    {
        if (WavePeakData == null && WavePeakDataTts == null)
        {
            return;
        }

        var row = SelectedLine;
        var waveformParagraph = row?.WaveformParagraph;
        if (waveformParagraph == null || WaveformParagraphs.Count == 0)
        {
            return;
        }

        if (_selectionFromWaveform)
        {
            foreach (var av in WaveformControls())
            {
                av.SelectedParagraph = waveformParagraph;
                av.AllSelectedParagraphs = new List<SubtitleLineViewModel> { waveformParagraph };
                av.InvalidateVisual();
            }

            return;
        }

        // SetPosition still wants an index into the list it's given — we know the mirror is in
        // WaveformParagraphs because Initialize put it there.
        var index = WaveformParagraphs.IndexOf(waveformParagraph);
        if (index < 0)
        {
            return;
        }

        foreach (var av in WaveformControls())
        {
            // Only the paragraph list and the selection are refreshed - NOT the view position.
            // Snapping StartPositionSeconds here is what made the timeline jerk when a clip ended
            // and the next row was selected mid-playback; the center-ease in SetWaveformPlayhead
            // owns the scroll now and glides to wherever the play-head goes.
            av.SetPosition(
                av.StartPositionSeconds,
                WaveformParagraphs,
                av.CurrentVideoPositionSeconds,
                index,
                new List<SubtitleLineViewModel> { waveformParagraph });
            av.InvalidateVisual();
        }
    }

    // Hands both visualizers the blocks for wherever they are looking now, without moving the view
    // or the playhead. They only keep the blocks around the view they were last given, so the
    // window calls this whenever the user scrolls, zooms or resizes (#15102).
    public void ReloadWaveformParagraphs()
    {
        if (WaveformParagraphs.Count == 0)
        {
            return;
        }

        var selected = SelectedLine?.WaveformParagraph;
        var index = selected == null ? -1 : WaveformParagraphs.IndexOf(selected);
        foreach (var av in WaveformControls())
        {
            av.SetPosition(
                av.StartPositionSeconds,
                WaveformParagraphs,
                av.CurrentVideoPositionSeconds,
                index,
                index < 0 ? new List<SubtitleLineViewModel>() : new List<SubtitleLineViewModel> { selected! });
        }
    }

    [RelayCommand]
    private async Task Export()
    {
        if (Window == null)
        {
            return;
        }

        // Start the picker in the subtitle's own folder (or the video's, for an unsaved
        // subtitle) - the OS-remembered last picker folder is rarely where this export
        // belongs (#13881).
        var suggestedStartFolder = GetFolderName(SubtitleFileName) ?? GetFolderName(_videoFileName);
        var folder = await _folderHelper.PickFolderAsync(Window!, Se.Language.General.SelectSaveFolder, suggestedStartFolder);
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        var jsonFileName = Path.Combine(folder, "SubtitleEditTts.json");

        // ask if overwrite if jsonFileName exists
        if (File.Exists(jsonFileName))
        {
            var answer = await MessageBox.Show(
                Window,
                Se.Language.General.OverwriteQuestion,
                string.Format(Se.Language.General.OverwriteFilesInFolderX, folder),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                File.Delete(jsonFileName);
            }
            catch (Exception e)
            {
                await MessageBox.Show(
                    Window,
                    Se.Language.General.Error,
                    $"Could not overwrite the file \"{jsonFileName}" + Environment.NewLine + e.Message,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }

        // The audio files go into a "wav" subfolder so they don't flood the folder the user
        // picked (usually the subtitle's own folder) with hundreds of clips - only the JSON
        // lives at the top level (#12093). Import resolves the relative "wav/0001.wav" names
        // against the JSON's folder, and legacy exports with the clips next to the JSON still
        // import fine.
        var audioFolder = Path.Combine(folder, "wav");
        Directory.CreateDirectory(audioFolder);

        // The recordings cloned voices speak from travel with the session too - see
        // ExportVoiceReference. The folder is only created when there is something to put in it.
        var referenceFolder = Path.Combine(folder, "refs");
        var exportedReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Copy files. Files still referenced by rows or their history entries must not be
        // overwritten: re-exporting to the folder a session was imported from used to replace an
        // original take (e.g. 0002.wav) that a history entry still pointed at - "pick from
        // history" then played and published the wrong audio with no warning.
        var referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in Lines)
        {
            if (!string.IsNullOrEmpty(l.StepResult.CurrentFileName))
            {
                referencedFiles.Add(Path.GetFullPath(l.StepResult.CurrentFileName));
            }

            foreach (var h in l.HistoryItems)
            {
                if (!string.IsNullOrEmpty(h.FileName))
                {
                    referencedFiles.Add(Path.GetFullPath(h.FileName));
                }
            }
        }

        var index = 0;
        var missingAudioCount = 0;
        var exportFormat = new TtsImportExport
        {
            VideoFileName = _videoFileName,
            ActorVoiceMappings = ActorVoiceMappings.ToList(),
        };
        foreach (var line in Lines)
        {
            index++;
            var sourceFileName = line.StepResult.CurrentFileName;
            var targetFileName = Path.Combine(audioFolder, index.ToString().PadLeft(4, '0') + Path.GetExtension((string?)sourceFileName));

            // A row can point at a deleted/moved file (e.g. an imported session whose folder was
            // cleaned). File.Copy used to throw unhandled mid-export, leaving some files copied
            // and no JSON written. Export the row without audio instead and report the count.
            if (string.IsNullOrEmpty(sourceFileName) || !File.Exists(sourceFileName))
            {
                missingAudioCount++;
                SeLogger.Error($"ReviewSpeech export: audio file missing for line {index}: \"{sourceFileName}\"");
                targetFileName = string.Empty;
            }

            // Re-exporting to the folder the session was imported from makes source and target
            // the SAME file (import resolves names against the JSON folder). The overwrite path
            // then deleted the target - destroying the source - and the copy threw with the
            // audio gone and no JSON written. An identical path needs no copy at all.
            var sourceIsTarget = !string.IsNullOrEmpty(targetFileName) &&
                                 string.Equals(Path.GetFullPath(sourceFileName), Path.GetFullPath(targetFileName), StringComparison.OrdinalIgnoreCase);

            // Divert to a suffixed name when the default target is a *different* file that a row
            // or history entry still references.
            if (!sourceIsTarget && !string.IsNullOrEmpty(targetFileName) && referencedFiles.Contains(Path.GetFullPath(targetFileName)))
            {
                var suffix = 1;
                string candidate;
                do
                {
                    candidate = Path.Combine(audioFolder, $"{index.ToString().PadLeft(4, '0')}_{suffix}{Path.GetExtension((string?)sourceFileName)}");
                    suffix++;
                }
                while (referencedFiles.Contains(Path.GetFullPath(candidate)) || File.Exists(candidate));
                targetFileName = candidate;
            }

            if (!sourceIsTarget && !string.IsNullOrEmpty(targetFileName) && File.Exists(targetFileName))
            {
                try
                {
                    File.Delete(targetFileName);
                }
                catch (Exception e)
                {
                    await MessageBox.Show(
                        Window,
                        Se.Language.General.Error,
                        $"Could not overwrite the file \"{targetFileName}" + Environment.NewLine + e.Message,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
            }

            if (!sourceIsTarget && !string.IsNullOrEmpty(targetFileName))
            {
                File.Copy(sourceFileName, targetFileName, true);
            }

            exportFormat.Items.Add(new TtsImportExportItem
            {
                // Relative path ("wav/0001.wav"), not absolute: the export folder is meant to be
                // moved or shared, and Import resolves the path against the JSON's own directory.
                // Forward slash so the same JSON opens on Windows, macOS and Linux.
                AudioFileName = string.IsNullOrEmpty(targetFileName) ? string.Empty : "wav/" + Path.GetFileName(targetFileName),
                StartMs = (long)Math.Round(line.StepResult.Paragraph.StartTime.TotalMilliseconds, MidpointRounding.AwayFromZero),
                EndMs = (long)Math.Round(line.StepResult.Paragraph.EndTime.TotalMilliseconds, MidpointRounding.AwayFromZero),
                VoiceName = line.StepResult.Voice?.Name ?? string.Empty,
                // Per-line engine snapshot, not the global SelectedEngine — different rows can
                // come from different engines via the cast workflow, and on re-import we want
                // each line to remember which engine produced it.
                EngineName = string.IsNullOrEmpty(line.StepResult.EngineName)
                    ? (SelectedEngine?.Name ?? string.Empty)
                    : line.StepResult.EngineName,
                Model = line.StepResult.Model,
                Instruction = line.StepResult.Instruction,
                VoiceFileName = ExportVoiceReference(line.StepResult.Voice, referenceFolder, exportedReferences),
                SpeedFactor = line.StepResult.SpeedFactor,
                Text = line.Text,
                Include = line.Include,
            });
        }

        // Export json
        var json = JsonSerializer.Serialize(exportFormat, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(jsonFileName, json);

        if (missingAudioCount > 0)
        {
            await MessageBox.Show(
                Window,
                Se.Language.General.Warning,
                $"{missingAudioCount} line(s) had no audio file and were exported without audio - see error-log.txt in the Subtitle Edit data folder.",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        await _folderHelper.OpenFolder(Window!, folder);
    }

    /// <summary>
    /// Copies the recording <paramref name="voice"/> clones from into the export's "refs" folder,
    /// returning the relative name to store with the line - or an empty string when the voice
    /// clones from nothing, or the copy failed.
    /// </summary>
    /// <remarks>
    /// Without this an imported session could not regenerate a cloned line: an imported clone is
    /// only a name in the engine's voice list on the machine that imported it, and the per-line
    /// "clone from video" is not even that - its references are cut into the run folder, which is
    /// swept when Subtitle Edit closes (#14095).
    ///
    /// Keyed by source path, so a cast of a few clones is copied once and shared by every line
    /// using it rather than once per line. The transcript sidecar goes along when there is one -
    /// the cloning engines need it, so a copy without it could only fail at synthesis.
    /// </remarks>
    internal static string ExportVoiceReference(Voice? voice, string referenceFolder, Dictionary<string, string> exported)
    {
        var source = PerLineVoiceClone.TryGetReferenceClip(voice);
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            return string.Empty;
        }

        var sourceFullPath = Path.GetFullPath(source);
        if (exported.TryGetValue(sourceFullPath, out var alreadyExported))
        {
            return alreadyExported;
        }

        try
        {
            Directory.CreateDirectory(referenceFolder);

            // Keep the clip's own name - the voice name for an imported clone, "line-0007" for a
            // per-line one. Suffix only when a *different* recording already claimed the name,
            // which also covers re-exporting into a folder an older export still owns.
            var baseName = Path.GetFileNameWithoutExtension(source);
            var extension = Path.GetExtension(source);
            var target = Path.Combine(referenceFolder, baseName + extension);
            var suffix = 1;
            while (File.Exists(target) &&
                   !string.Equals(Path.GetFullPath(target), sourceFullPath, StringComparison.OrdinalIgnoreCase))
            {
                target = Path.Combine(referenceFolder, $"{baseName}_{suffix}{extension}");
                suffix++;
            }

            // Re-exporting to the folder the session was imported from makes source and target the
            // same file; copying it onto itself would only throw.
            if (!string.Equals(Path.GetFullPath(target), sourceFullPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(source, target, true);

                var sourceTranscript = Path.ChangeExtension(source, ".txt");
                if (File.Exists(sourceTranscript))
                {
                    File.Copy(sourceTranscript, Path.ChangeExtension(target, ".txt"), true);
                }
            }

            var relativeName = "refs/" + Path.GetFileName(target);
            exported[sourceFullPath] = relativeName;
            return relativeName;
        }
        catch (Exception exception)
        {
            // One un-copyable reference must not take the whole export down - the line is exported
            // without it, exactly as it was before references travelled at all.
            SeLogger.Error(exception, $"ReviewSpeech export: copying the voice reference \"{source}\" failed");
            return string.Empty;
        }
    }

    private static string? GetFolderName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var folder = Path.GetDirectoryName(fileName);
        return string.IsNullOrEmpty(folder) ? null : folder;
    }

    [RelayCommand]
    private void ElevenLabsReset()
    {
        var settings = new SeVideoTextToSpeech();
        Stability = settings.ElevenLabsStability;
        Similarity = settings.ElevenLabsSimilarity;
        SpeakerBoost = settings.ElevenLabsSpeakerBoost;
        Speed = settings.ElevenLabsSpeed;
        StyleExaggeration = settings.ElevenLabsStyleeExaggeration;
    }

    [RelayCommand]
    private async Task ShowHistory(ReviewRow? line)
    {
        if (Window == null || line == null)
        {
            return;
        }

        var result =
            await _windowService.ShowDialogAsync<ReviewSpeechHistoryWindow, ReviewSpeechHistoryViewModel>(Window!,
                vm => vm.Initialize(line));
        if (!result.OkPressed || result.SelectedHistoryItem == null)
        {
            return;
        }

        var picked = result.SelectedHistoryItem;
        line.Voice = picked.Voice?.Name ?? string.Empty;
        line.StepResult.CurrentFileName = picked.FileName ?? string.Empty;
        line.StepResult.Voice = picked.Voice;
        // Restore the full engine snapshot stored with the history row so the line knows which
        // engine/model/instruction produced this audio. Without this, clicking the row would
        // sync the left panel to the live (last-set) values rather than what's actually playing.
        line.StepResult.EngineName = picked.EngineName ?? string.Empty;
        line.StepResult.Model = picked.Model ?? string.Empty;
        line.StepResult.Instruction = picked.Instruction ?? string.Empty;
        // Restore the real speed factor too, not just the display string - otherwise Export
        // writes the previous SpeedFactor next to this entry's audio file.
        line.StepResult.SpeedFactor = picked.Speed;
        line.Speed = Math.Round(picked.Speed, 2).ToString(CultureInfo.CurrentCulture);

        // Mirror the click-to-sync behaviour so the left panel immediately reflects the picked
        // history entry's engine/model/voice/instruction.
        await ApplyLineToLeftPanelAsync(line);
    }

    [RelayCommand]
    private async Task ShowElevenLabsEngineV3Help()
    {
        if (Window == null)
        {
            return;
        }

        await Window.Launcher.LaunchUriAsync(new Uri("https://elevenlabs.io/blog/eleven-v3-audio-tags-expressing-emotional-context-in-speech"));
    }

    [RelayCommand]
    private async Task ShowStabilityHelp()
    {
        await ElevenLabsSettingsViewModel.ShowStabilityHelp(Window!);
    }

    [RelayCommand]
    private async Task ShowSimilarityHelp()
    {
        await ElevenLabsSettingsViewModel.ShowSimilarityHelp(Window!);
    }

    [RelayCommand]
    private async Task ShowSpeakerBoostHelp()
    {
        await ElevenLabsSettingsViewModel.ShowSpeakerBoostHelp(Window!);
    }

    [RelayCommand]
    private async Task ShowSpeedHelp()
    {
        await ElevenLabsSettingsViewModel.ShowSpeedHelp(Window!);
    }

    [RelayCommand]
    private async Task ShowStyleExaggerationHelp()
    {
        await ElevenLabsSettingsViewModel.ShowStyleExaggerationHelp(Window!);
    }

    /// <summary>
    /// What the video says during a line - the transcript a freshly cut reference clip needs. The
    /// original-language text when the TTS window supplied a lookup for it and it knows (a dub
    /// is generated from a translation, and the clip holds what was said, not its translation),
    /// otherwise null. The line's own text is deliberately not a fallback: handing an engine the
    /// translation as the clip's transcript makes it replay the clip instead of speaking the
    /// line (#14480).
    /// </summary>
    private string? SpokenTextInVideo(ReviewRow line)
    {
        var fromOriginal = ReferenceTextOf?.Invoke(line.StepResult.Paragraph);
        return string.IsNullOrWhiteSpace(fromOriginal) ? null : fromOriginal;
    }

    /// <summary>
    /// A real voice for the per-line clone marker: the reference this line was generated from when
    /// it is still on disk, otherwise a fresh cut of the line's own audio in the video. Returns
    /// null when there is nothing to clone from - the user has already been told why.
    /// </summary>
    private async Task<Voice?> ResolvePerLineCloneVoiceAsync(ITtsEngine engine, ReviewRow line)
    {
        // Prefer the clip the line was generated from, so a regenerate clones the same speaker the
        // line's other takes did - and so an imported session uses the reference that travelled
        // with it instead of cutting the video again.
        var existingClip = PerLineVoiceClone.TryGetReferenceClip(line.StepResult.Voice);
        if (!string.IsNullOrEmpty(existingClip) && File.Exists(existingClip))
        {
            var reused = PerLineVoiceClone.MakeVoiceForClip(engine, existingClip, line.StepResult.Voice?.Name);
            if (reused != null)
            {
                return reused;
            }
        }

        if (string.IsNullOrEmpty(_videoFileName) || !File.Exists(_videoFileName))
        {
            await ShowCloneVoiceError(Se.Language.Video.TextToSpeech.CloneVoicePerLineNeedsVideo);
            return null;
        }

        var index = Lines.IndexOf(line);
        if (index < 0)
        {
            return null;
        }

        // A folder of its own, not the generate run's "clone-references": those clips belong to
        // the rows as they were generated, and a re-cut of an edited line must not replace one.
        // The video duration is unknown here, which only means the last line's clip is not
        // clamped to the end of the video - ffmpeg stops at the end of the audio either way.
        var clipFileName = await PerLineVoiceClone.CutReferenceClipAsync(
            _videoFileName,
            Lines.Select(l => l.StepResult.Paragraph).ToList(),
            index,
            SpokenTextInVideo(line),
            Path.Combine(_waveFolder, "clone-references-regenerate"),
            videoDurationSeconds: 0,
            audioTrackFfIndex: -1,
            // Not _cancellationToken: it still belongs to the previous regenerate at this point
            // (this run replaces it further down), so cancelling one regenerate would abort the
            // next one's cut before it started. The cut is one short ffmpeg call anyway - the
            // main window's preview cut passes None for the same reason.
            CancellationToken.None);
        if (clipFileName == null)
        {
            await ShowCloneVoiceError(Se.Language.Video.TextToSpeech.CloneVoicePerLineNoClips);
            return null;
        }

        var clonedVoice = PerLineVoiceClone.MakeVoiceForClip(engine, clipFileName);
        if (clonedVoice == null)
        {
            // The engine could not use the clip as a reference (or - a wiring bug - it claims
            // per-line cloning without implementing IPerLineCloneEngine). Say so rather than
            // quietly regenerating in some other voice.
            await ShowCloneVoiceError($"{engine.Name} cannot clone the voice of each line.");
        }

        return clonedVoice;
    }

    private async Task ShowCloneVoiceError(string message)
    {
        if (Window == null)
        {
            return;
        }

        await MessageBox.Show(
            Window,
            Se.Language.General.Error,
            message,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    // Replace _cancellationTokenSource, disposing the previous instance. The
    // RegenerateAudio path swaps in a CTS owned by GeneratingAudioViewModel,
    // so disposing the old one here just frees the per-window CTS created in
    // the constructor (or a previous Play*/RegenerateAudio call we own).
    private void ReplaceCts(CancellationTokenSource next)
    {
        var old = _cancellationTokenSource;
        _cancellationTokenSource = next;
        _cancellationToken = next.Token;
        // The old CTS is deliberately not disposed: it may still be held by a non-modal
        // GeneratingAudioWindow whose Cancel button calls Cancel() on it - disposing here made
        // that throw ObjectDisposedException. A CTS without timers has no unmanaged state that
        // needs deterministic disposal; window close disposes the final instance.
    }

    [RelayCommand]
    private async Task RegenerateAudio(ReviewRow? row)
    {
        var engine = SelectedEngine;
        if (engine == null)
        {
            return;
        }

        if (engine is ElevenLabs)
        {
            var settings = Se.Settings.Video.TextToSpeech;

            settings.ElevenLabsStability = Stability;
            settings.ElevenLabsSimilarity = Similarity;
            settings.ElevenLabsSpeakerBoost = SpeakerBoost;
            settings.ElevenLabsSpeed = Speed;
            settings.ElevenLabsStyleeExaggeration = StyleExaggeration;
        }

        var line = row ?? SelectedLine;
        if (line == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(line.Text))
        {
            if (Window != null)
            {
                await MessageBox.Show(
                    Window,
                    Se.Language.General.Warning,
                    "Cannot regenerate audio with empty text",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        // Gate the whole grid BEFORE the engine probes below: IsInstalled / EnsureVoiceInstalled
        // can take seconds (process spawn, HTTP), and with the gate applied only after them a
        // second regenerate or a play started in that window swapped the shared cancellation
        // source under this run. The per-row play/regenerate buttons, Ctrl+R and the Space
        // shortcut all honor IsPlayingEnabled; OK/Export/Escape honor IsRegenerateEnabled. The
        // outer try/finally guarantees the gate is lifted on every exit, including the early
        // returns.
        IsRegenerateEnabled = false;
        foreach (var l in Lines)
        {
            l.IsPlayingEnabled = false;
        }

        try
        {
            if (await PrepareEngineAndVoiceAsync(engine, line) is not { } prepared)
            {
                return;
            }

            if (!await RegenerateRowCoreAsync(engine, line, prepared.Voice, prepared.Model,
                    prepared.Instruction, prepared.Language, prepared.Region, prepared.OldStyle))
            {
                return;
            }

            _skipAutoContinue = true;
            await PlayAudio(line.StepResult.CurrentFileName);
        }
        finally
        {
            IsRegenerateEnabled = true;
            foreach (var l in Lines)
            {
                l.IsPlayingEnabled = true;
            }
        }
    }

    // Engine/voice preparation shared by the single-row regenerate and the split's two
    // regenerates: install checks, the clone-from-video consent + reference cut, and the
    // panel snapshot the run must use. Returns null when the user backed out (or a probe failed).
    private async Task<PreparedRegenerate?> PrepareEngineAndVoiceAsync(ITtsEngine engine, ReviewRow line)
    {
        var voice = SelectedVoice;
        if (voice == null)
        {
            return null;
        }

        // Same install/download flow as the main window: this used to be a bare IsInstalled check
        // that silently returned, so picking a not-yet-downloaded engine (e.g. CosyVoice3) here
        // made Regenerate do nothing with no prompt.
        if (!await TtsEngineInstaller.EnsureEngineInstalled(engine, Window, _windowService, SelectedRegion, SelectedModel, null, null, async () => await SelectedEngineChangedAsync()))
        {
            return null;
        }

        // "Clone from video" is a marker, not a voice any engine can speak with: the generate
        // pipeline swaps it for a real one per paragraph, and this window used to hand it straight
        // to the engine - which is what an imported session failed on with "Voice is not an
        // OmniVoice" (#14095).
        if (PerLineVoiceClone.IsSelected(voice))
        {
            // Same one-time gate the TTS window puts in front of cloning; picking this voice here
            // is a fresh decision to clone whoever speaks in the video.
            if (Window != null && !await VoiceCloneConsentPrompt.EnsureAsync(
                    engine,
                    Window,
                    () => _windowService.ShowDialogAsync<VoiceCloneConsentWindow, VoiceCloneConsentViewModel>(Window, _ => { })))
            {
                return null;
            }

            var clonedVoice = await ResolvePerLineCloneVoiceAsync(engine, line);
            if (clonedVoice == null)
            {
                return null;
            }

            voice = clonedVoice;
        }

        if (!await TtsVoiceInstaller.EnsureVoiceInstalled(engine, voice, Window, _windowService))
        {
            return null;
        }

        // Snapshot the panel state this run uses: the progress popup is non-modal, so clicking
        // another row mid-run rewrites SelectedModel/Instruction (via the row-click panel sync) -
        // reading them after the awaits recorded settings that never produced the audio into the
        // row snapshot and its history entry.
        return new PreparedRegenerate(
            voice,
            SelectedModel,
            Instruction,
            SelectedLanguage,
            SelectedRegion,
            Se.Settings.Video.TextToSpeech.MurfStyle);
    }

    private sealed record PreparedRegenerate(Voice Voice, string? Model, string? Instruction, TtsLanguage? Language, string? Region, string? OldStyle);

    /// <summary>
    /// Synthesizes one row and swaps its clip in - the body the single-row regenerate and each
    /// half of a split share. Returns false when the audio could not be produced (the row is left
    /// as it was, and the caller must not play it).
    /// </summary>
    private async Task<bool> RegenerateRowCoreAsync(ITtsEngine engine, ReviewRow line, Voice voice,
        string? model, string? instruction, TtsLanguage? language, string? region, string? oldStyle)
    {
        // Capture the *saved* style, not SelectedStyle: capturing the new value made the
        // restore a no-op, so a one-line style override silently became the permanent global Murf style.
        if (engine is Murf && !string.IsNullOrEmpty(SelectedStyle))
        {
            Se.Settings.Video.TextToSpeech.MurfStyle = SelectedStyle;
        }

        var generatingAudioVm = _windowService.ShowWindow<GeneratingAudioWindow, GeneratingAudioViewModel>(Window!);
        ReplaceCts(generatingAudioVm.CancellationTokenSource);

        // The row's live StepResult must only change once the whole pipeline has succeeded - it
        // used to be mutated right after Speak, so a cancel or a failed trim/post-process left the
        // row half-updated (raw un-stretched clip with the old speed/voice display) and OK/Export
        // published that state.
        var originalFileName = line.StepResult.CurrentFileName;
        var originalVoice = line.StepResult.Voice;

        try
        {
            var speakResult = await TtsInstructionSwap.RunAsync(engine, instruction, () =>
                // Strip markup here the way the main generate path does - the row text is the
                // subtitle's own text, and engines vocalize "<i>" or garble on tags.
                engine.Speak(Utilities.UnbreakLine(HtmlUtil.RemoveHtmlTags(line.Text, alsoSsaTags: true)),
                    _waveFolder, voice, language, region, model, _cancellationToken));

            if (speakResult.Error || string.IsNullOrEmpty(speakResult.FileName) || !File.Exists(speakResult.FileName))
            {
                if (Window != null)
                {
                    var detail = string.IsNullOrEmpty(speakResult.ErrorMessage)
                        ? "The engine produced no audio - see error-log.txt in the Subtitle Edit data folder."
                        : speakResult.ErrorMessage;
                    await MessageBox.Show(
                        Window,
                        Se.Language.General.Error,
                        "Regenerating audio failed: " + detail,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return false;
            }

            line.StepResult.CurrentFileName = speakResult.FileName;
            line.StepResult.Voice = voice;

            var adjustSpeedStepResult = await TrimAndAdjustSpeed(line);
            var postProcessedFileName = await TtsPostProcessor.ApplyPostProcessing(adjustSpeedStepResult.CurrentFileName, _waveFolder, _cancellationToken);

            if (_cancellationToken.IsCancellationRequested)
            {
                line.StepResult.CurrentFileName = originalFileName;
                line.StepResult.Voice = originalVoice;
                return false;
            }

            adjustSpeedStepResult.CurrentFileName = postProcessedFileName;
            // Record which engine/model/instruction this regenerate used so the row's "click to
            // sync left panel" feature can restore them later.
            adjustSpeedStepResult.EngineName = engine.Name;
            adjustSpeedStepResult.Model = model ?? string.Empty;
            adjustSpeedStepResult.Instruction = instruction ?? string.Empty;
            line.Speed = Math.Round(adjustSpeedStepResult.SpeedFactor, 2).ToString(CultureInfo.CurrentCulture);
            line.Cps = Math.Round(adjustSpeedStepResult.Paragraph.GetCharactersPerSecond(), 2).ToString(CultureInfo.CurrentCulture);
            line.StepResult = adjustSpeedStepResult;
            line.Voice = voice.ToString();

            line.AddHistory(voice, line.StepResult.CurrentFileName, engine.Name, model ?? string.Empty, instruction ?? string.Empty);

            // The row's clip changed - refresh the generated-speech waveform.
            ScheduleTtsWaveformRebuild();
            return true;
        }
        catch (OperationCanceledException)
        {
            // Cancel in the GeneratingAudio popup: Speak and the ffmpeg steps surface it as a
            // throw (it used to escape as an unhandled exception). Undo the partial row update.
            line.StepResult.CurrentFileName = originalFileName;
            line.StepResult.Voice = originalVoice;
            return false;
        }
        catch (HttpRequestException ex)
        {
            line.StepResult.CurrentFileName = originalFileName;
            line.StepResult.Voice = originalVoice;
            SeLogger.Error(ex, "TTS server error during regeneration.");
            if (Window != null)
            {
                await MessageBox.Show(
                    Window,
                    Se.Language.General.Error,
                    "TTS server error: " + ex.Message,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return false;
        }
        catch (Exception ex)
        {
            line.StepResult.CurrentFileName = originalFileName;
            line.StepResult.Voice = originalVoice;
            SeLogger.Error(ex, "Regenerating audio failed.");
            if (Window != null)
            {
                await MessageBox.Show(
                    Window,
                    Se.Language.General.Error,
                    "Regenerating audio failed: " + ex.Message,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return false;
        }
        finally
        {
            generatingAudioVm.Close();
            if (engine is Murf && oldStyle != null)
            {
                Se.Settings.Video.TextToSpeech.MurfStyle = oldStyle;
            }
        }
    }

    // One entry per reversible structural edit (currently: split). Holds the row list as it was,
    // so Undo can put it back exactly - text, times, clips, history and Include flags included.
    private sealed record ReviewUndoEntry(string Description, List<ReviewRow> Rows, ReviewRow? Selected);

    private readonly Stack<ReviewUndoEntry> _undoStack = new();

    [ObservableProperty] private bool _canUndo;

    private void PushUndoSnapshot(string description)
    {
        // The rows are the same instances that survive the edit (a split only adds/removes rows),
        // so a shallow copy of the list plus the current selection is a complete before-image.
        _undoStack.Push(new ReviewUndoEntry(description, new List<ReviewRow>(Lines), SelectedLine));
        CanUndo = _undoStack.Count > 0;
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var entry = _undoStack.Pop();
        CanUndo = _undoStack.Count > 0;

        // The split's halves may be the rows currently playing/selected; stop playback first so
        // the cursor timer does not keep chasing a row that is about to leave the list.
        _skipAutoContinue = true;
        ResetPlaybackUiState();

        Lines.Clear();
        foreach (var row in entry.Rows)
        {
            Lines.Add(row);
        }

        RenumberRows();
        RebuildWaveformParagraphs();

        SelectedLine = entry.Selected != null && Lines.Contains(entry.Selected) ? entry.Selected : Lines.FirstOrDefault();
        if (SelectedLine != null)
        {
            LineGrid.SelectedItem = SelectedLine;
            LineGrid.ScrollIntoView(SelectedLine);
        }

        foreach (var av in WaveformControls())
        {
            var selected = SelectedLine?.WaveformParagraph;
            av.SetPosition(
                av.StartPositionSeconds,
                WaveformParagraphs,
                av.CurrentVideoPositionSeconds,
                selected == null ? -1 : WaveformParagraphs.IndexOf(selected),
                selected == null ? new List<SubtitleLineViewModel>() : new List<SubtitleLineViewModel> { selected });
            av.InvalidateVisual();
        }

        ScheduleTtsWaveformRebuild();
    }

    // Split the selected line into two at the waveform play-head (audio) and the text box caret
    // (text). Both halves become rows of their own and both are re-synthesized from their text, so
    // the two clips always match what the two lines say.
    [RelayCommand]
    private async Task SplitLine(ReviewRow? row)
    {
        var engine = SelectedEngine;
        var line = row ?? SelectedLine;
        if (engine == null || line == null || string.IsNullOrWhiteSpace(line.Text))
        {
            return;
        }

        // Audio split point: the play-head, when it sits inside this line; otherwise the middle.
        var paragraph = line.StepResult.Paragraph;
        var startSeconds = paragraph.StartTime.TotalSeconds;
        var endSeconds = paragraph.EndTime.TotalSeconds;
        var playheadSeconds = AudioVisualizer?.CurrentVideoPositionSeconds ?? 0;
        if (playheadSeconds <= startSeconds || playheadSeconds >= endSeconds)
        {
            playheadSeconds = (startSeconds + endSeconds) / 2.0;
        }

        // Text split point: the caret, when it sits inside the line's text; otherwise -1, which
        // makes the text split fall back to the line break / auto-break (SplitManager's rules).
        // Not gated on IsFocused: opening the context menu moves focus to the flyout, so by the
        // time the command runs the box is no longer focused - the caret it holds is still the one
        // the user placed, so use it whenever it is a position inside the text.
        var caret = -1;
        if (EditTextBox != null)
        {
            var selectionStart = EditTextBox.SelectionStart;
            if (selectionStart > 0 && selectionStart < line.Text.Length &&
                !string.IsNullOrWhiteSpace(line.Text.Substring(0, selectionStart)) &&
                !string.IsNullOrWhiteSpace(line.Text.Substring(selectionStart)))
            {
                caret = selectionStart;
            }
        }

        var language = LanguageAutoDetect.AutoDetectGoogleLanguage(
            new Subtitle(new List<Paragraph> { paragraph }));

        // Split the text with the same rules as the main window's "split line at position" so a
        // formatted or two-line cue is handled identically. Reuse SplitManager on a throwaway
        // SubtitleLineViewModel, then read the two halves back out.
        var carrier = new SubtitleLineViewModel
        {
            Text = line.Text,
            StartTime = TimeSpan.FromMilliseconds(paragraph.StartTime.TotalMilliseconds),
            EndTime = TimeSpan.FromMilliseconds(paragraph.EndTime.TotalMilliseconds),
        };
        var list = new ObservableCollection<SubtitleLineViewModel> { carrier };
        new SplitManager().Split(list, carrier, playheadSeconds, caret, language);

        // Re-wrap both halves the way the main window's split does - the cursor cut can leave a
        // line wider than the configured max, or two short lines that fit on one.
        var firstText = RebalanceSplitText(list[0].Text, language);
        var secondText = RebalanceSplitText(list[1].Text, language);

        var gapMs = Se.Settings.General.MinimumBetweenLines.GetMilliseconds();
        var splitMs = playheadSeconds * 1000.0;
        var firstEndMs = Math.Max(startSeconds * 1000.0 + 1, splitMs - gapMs / 2.0);
        var secondStartMs = Math.Min(endSeconds * 1000.0 - 1, splitMs + gapMs / 2.0);
        if (firstEndMs > secondStartMs)
        {
            var middle = (firstEndMs + secondStartMs) / 2.0;
            firstEndMs = middle;
            secondStartMs = middle;
        }

        var first = CreateSplitRow(line, firstText, startSeconds * 1000.0, firstEndMs);
        var second = CreateSplitRow(line, secondText, secondStartMs, endSeconds * 1000.0);

        // Replace the original row with the two halves, then re-regenerate each from its own text.
        var index = Lines.IndexOf(line);
        if (index < 0)
        {
            return;
        }

        IsRegenerateEnabled = false;
        foreach (var l in Lines)
        {
            l.IsPlayingEnabled = false;
        }

        try
        {
            // Both halves keep the row's engine/voice/model/instruction; gate the split on the
            // engine install once (PrepareEngineAndVoiceAsync) and reuse it for both rows so the
            // user is not asked twice.
            var prepared = await PrepareEngineAndVoiceAsync(engine, line);
            if (prepared == null)
            {
                return;
            }

            // Undo point: the whole row list as it stands now. The split replaces one row with two
            // regenerated ones and cannot be reversed by editing, so keep the before-image.
            PushUndoSnapshot($"split \"{line.Text}\"");

            // Detach the replaced row's mirror, splice the two halves in, then rebuild every
            // mirror in Lines order - appending them instead left WaveformParagraphs unsorted,
            // and the visualizer's window scan assumes it is sorted by start time.
            if (line.WaveformParagraph != null)
            {
                line.WaveformParagraph.PropertyChanged -= OnWaveformParagraphChanged;
                _waveformParagraphToRow.Remove(line.WaveformParagraph);
            }

            Lines.RemoveAt(index);
            Lines.Insert(index, first);
            Lines.Insert(index + 1, second);

            RenumberRows();
            RebuildWaveformParagraphs();

            // Synthesize both halves. A failure on one half leaves the other in place - the row is
            // still editable and can be regenerated by hand.
            await RegenerateRowCoreAsync(engine, first, prepared.Voice, prepared.Model,
                prepared.Instruction, prepared.Language, prepared.Region, prepared.OldStyle);
            await RegenerateRowCoreAsync(engine, second, prepared.Voice, prepared.Model,
                prepared.Instruction, prepared.Language, prepared.Region, prepared.OldStyle);

            SelectedLine = first;
            LineGrid.SelectedItem = first;
            LineGrid.ScrollIntoView(first);

            // Force the blocks onto the visualizers: ReloadWaveformParagraphs is a no-op if the
            // view is at the same place (NeedsParagraphReload), so hand the list over directly.
            foreach (var av in WaveformControls())
            {
                av.SetPosition(
                    av.StartPositionSeconds,
                    WaveformParagraphs,
                    av.CurrentVideoPositionSeconds,
                    WaveformParagraphs.IndexOf(first.WaveformParagraph!),
                    new List<SubtitleLineViewModel> { first.WaveformParagraph! });
                av.InvalidateVisual();
            }

            ScheduleTtsWaveformRebuild();
        }
        finally
        {
            IsRegenerateEnabled = true;
            foreach (var l in Lines)
            {
                l.IsPlayingEnabled = true;
            }
        }
    }

    // Re-wraps one half of a split with the same algorithm/settings as the main window's
    // "split/rebalance long lines" (RebalanceAfterSplit), so a re-wrapped half matches what the
    // same split produces in the editor.
    private static string RebalanceSplitText(string text, string language)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var singleLineMaxLength = Se.Settings.Tools.SplitRebalanceLongLinesSingleLineMaxLength > 0
            ? Se.Settings.Tools.SplitRebalanceLongLinesSingleLineMaxLength
            : Se.Settings.General.SubtitleLineMaximumLength;
        var unbreakLinesShorterThan = Se.Settings.Tools.SplitRebalanceLongLinesUnbreakShorterThan > 0
            ? Se.Settings.Tools.SplitRebalanceLongLinesUnbreakShorterThan
            : Se.Settings.General.UnbreakLinesShorterThan;

        var mergeLinesShorterThan = unbreakLinesShorterThan >= singleLineMaxLength
            ? singleLineMaxLength + 1
            : unbreakLinesShorterThan;

        return Utilities.AutoBreakLine(text, singleLineMaxLength, mergeLinesShorterThan, language);
    }

    // Builds one half of a split: a fresh row carrying the original row's engine/voice/model,
    // history and original-text snapshot, with the given text and time codes. The audio is
    // regenerated later, so the clip starts empty.
    private ReviewRow CreateSplitRow(ReviewRow source, string text, double startMs, double endMs)
    {
        var paragraph = new Paragraph
        {
            Number = source.StepResult.Paragraph.Number,
            Text = text,
            StartTime = new TimeCode(TimeSpan.FromMilliseconds(startMs)),
            EndTime = new TimeCode(TimeSpan.FromMilliseconds(endMs)),
        };

        var result = new TtsStepResult
        {
            Paragraph = paragraph,
            Text = text,
            CurrentFileName = string.Empty,
            SpeedFactor = source.StepResult.SpeedFactor,
            Voice = source.StepResult.Voice,
            EngineName = source.StepResult.EngineName,
            Model = source.StepResult.Model,
            Instruction = source.StepResult.Instruction,
            Include = source.Include,
        };

        var row = new ReviewRow
        {
            Include = source.Include,
            Number = paragraph.Number,
            Text = text,
            Voice = source.Voice,
            Speed = source.Speed,
            Cps = Math.Round(paragraph.GetCharactersPerSecond(), 2).ToString(CultureInfo.CurrentCulture),
            StepResult = result,
            // Snapshots are compared against the main subtitle on OK to publish text edits, so the
            // halves carry the *original* time codes they were born from (unchanged by the split).
            OriginalText = text,
            OriginalStartMs = source.OriginalStartMs,
            OriginalEndMs = source.OriginalEndMs,
        };
        row.StartHistory();
        return row;
    }

    // Rebuilds the visualizer's mirror list from the rows, in Lines order. Called after a split:
    // the two halves replace the row in place, so the sorted-by-start-time invariant the
    // visualizer relies on must be re-established from scratch.
    private void RebuildWaveformParagraphs()
    {
        foreach (var wp in WaveformParagraphs)
        {
            wp.PropertyChanged -= OnWaveformParagraphChanged;
        }

        WaveformParagraphs.Clear();
        _waveformParagraphToRow.Clear();

        foreach (var row in Lines)
        {
            var result = row.StepResult;
            var waveformParagraph = new SubtitleLineViewModel
            {
                Number = result.Paragraph.Number,
                Text = result.Text,
                StartTime = TimeSpan.FromMilliseconds(result.Paragraph.StartTime.TotalMilliseconds),
                EndTime = TimeSpan.FromMilliseconds(result.Paragraph.EndTime.TotalMilliseconds),
            };
            waveformParagraph.UpdateDuration();
            waveformParagraph.PropertyChanged += OnWaveformParagraphChanged;
            row.WaveformParagraph = waveformParagraph;
            WaveformParagraphs.Add(waveformParagraph);
            _waveformParagraphToRow[waveformParagraph] = row;
        }
    }

    private void RenumberRows()
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            Lines[i].Number = i + 1;
            Lines[i].StepResult.Paragraph.Number = i + 1;
            if (Lines[i].WaveformParagraph != null)
            {
                Lines[i].WaveformParagraph!.Number = i + 1;
            }
        }
    }

    [RelayCommand]
    private async Task PlayRow(ReviewRow? line)
    {
        if (line == null)
        {
            return;
        }

        ReplaceCts(new CancellationTokenSource());
        _skipAutoContinue = false;
        _startPlayTicks = DateTime.UtcNow.Ticks;

        // Playing a row selects it, so the keyboard (Space to replay, R to regenerate) always
        // targets the line just heard - previously selection stayed on the old row (#12093).
        SelectedLine = line;

        line.IsPlaying = true;
        _playingRow = line;
        foreach (var l in Lines)
        {
            l.IsPlayingEnabled = false;
        }

        await PlayAudio(line.StepResult.CurrentFileName);
    }


    // The row to play from the play-head: the clip under it, or the next clip to its right when
    // it sits in a gap (play must never walk back into the clip the play-head already passed).
    // Null when the play-head is at/after the end of the last clip.
    private ReviewRow? FindRowToPlayFromPlayhead()
    {
        var seconds = AudioVisualizer?.CurrentVideoPositionSeconds ?? double.NaN;
        if (double.IsNaN(seconds))
        {
            return SelectedLine;
        }

        // Lines are in start-time order; the first row that has not ended by the play-head is the
        // one the play-head is inside or about to enter.
        ReviewRow? next = null;
        foreach (var row in Lines)
        {
            var paragraph = row.StepResult.Paragraph;
            if (paragraph.EndTime.TotalSeconds > seconds)
            {
                next = row;
                break;
            }
        }

        return next;
    }

    [RelayCommand]
    private async Task Play()
    {
        // Play from the play-head: the clip under it, or the next one when it is in a gap, so the
        // clip to the left of the play-head is never replayed by mistake.
        var line = FindRowToPlayFromPlayhead() ?? SelectedLine;
        if (line == null)
        {
            return;
        }

        ReplaceCts(new CancellationTokenSource());
        _skipAutoContinue = false;
        _startPlayTicks = DateTime.UtcNow.Ticks;
        _playingRow = line;
        SelectedLine = line;
        await PlayAudio(line.StepResult.CurrentFileName);
    }

    [RelayCommand]
    private void Stop()
    {
        Se.WriteToolsLog("TTS review: Stop clicked");
        _skipAutoContinue = true;
        _cancellationTokenSource.Cancel();

        // Off-thread: an inline Dispose here is mpv_terminate_destroy on the UI thread, the
        // pattern behind the delayed IFrameworkInputPane.Unadvise crash (#13567, #13376).
        DisposePlayerOffThread();

        _playingRow = null;
        IsPlayVisible = true;
        IsStopVisible = false;
    }

    [RelayCommand]
    private void Ok()
    {
        Se.WriteToolsLog("TTS review: OK clicked - closing");

        // Push any edits the user made to row.Text back into the step results so
        // the caller sees them, then publish the included rows as StepResults.
        foreach (var row in Lines)
        {
            row.StepResult.Text = row.Text;
        }

        StepResults = Lines.Where(p => p.Include).Select(p => p.StepResult).ToArray();

        // All rows, not just included ones - excluding a row only skips its audio in the merge,
        // while a text edit was still made deliberately and should reach the main subtitle.
        TextChanges = Lines
            .Where(row => row.Text != row.OriginalText)
            .Select(row => new ReviewTextChange(row.OriginalStartMs, row.OriginalEndMs, row.Text))
            .ToList();

        Se.SaveSettings();
        OkPressed = true;
        Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        Se.WriteToolsLog("TTS review: Cancel clicked - closing");
        Close();
    }

    private void Close()
    {
        Dispatcher.UIThread.Invoke(() => { Window?.Close(); });
    }

    private async Task<TtsStepResult> TrimAndAdjustSpeed(ReviewRow row)
    {
        var item = row.StepResult;
        var p = item.Paragraph;
        var index = Lines.IndexOf(row);
        var next = index + 1 < Lines.Count ? Lines[index + 1] : null;

        var doVad = Se.Settings.Video.TextToSpeech.VadSilenceCompressionEnabled;
        var vadMaxSilence = Se.Settings.Video.TextToSpeech.VadMaxSilenceSeconds;
        var doHighQualityStretch = Se.Settings.Video.TextToSpeech.HighQualityTimeStretchEnabled;

        // Step 1: Trim silence from start and end. A failed trim (misconfigured/failing ffmpeg)
        // must fall back to the untrimmed audio - adopting the missing output blindly made
        // FfmpegMediaInfo.Parse below return a null Duration and the factor math NRE'd.
        // Silence threshold relative to the clip's peak - same as the main pipeline (#14480).
        var peakDbfs = await TtsSilenceThreshold.MeasurePeakDbfsAsync(item.CurrentFileName, _cancellationToken);
        var outputFileNameTrim = Path.Combine(_waveFolder, Guid.NewGuid() + ".wav");
        _tempAudioFiles.Add(outputFileNameTrim);
        var trimProcess = FfmpegGenerator.TrimSilenceStartAndEnd(item.CurrentFileName, outputFileNameTrim, TtsSilenceThreshold.Amplitude(peakDbfs));
        await trimProcess.StartAndWaitAsync(_cancellationToken);

        var currentFile = File.Exists(outputFileNameTrim) && new FileInfo(outputFileNameTrim).Length > 0
            ? outputFileNameTrim
            : item.CurrentFileName;

        // Step 2: VAD-based internal silence compression
        if (doVad)
        {
            var vadOutput = Path.Combine(_waveFolder, $"vad_{Guid.NewGuid()}.wav");
            _tempAudioFiles.Add(vadOutput);
            var vadProcess = FfmpegGenerator.CompressInternalSilence(currentFile, vadOutput, vadMaxSilence, TtsSilenceThreshold.DbLiteral(peakDbfs));
            await vadProcess.StartAndWaitAsync(_cancellationToken);

            if (File.Exists(vadOutput) && new FileInfo(vadOutput).Length > 0)
            {
                currentFile = vadOutput;
            }
        }

        var addDuration = 0d;
        if (next != null && p.EndTime.TotalMilliseconds < next.StepResult.Paragraph.StartTime.TotalMilliseconds)
        {
            var diff = next.StepResult.Paragraph.StartTime.TotalMilliseconds - p.EndTime.TotalMilliseconds;
            addDuration = Math.Min(1000, diff);
            if (addDuration < 0)
            {
                addDuration = 0;
            }
        }

        var mediaInfo = FfmpegMediaInfo.Parse(currentFile);
        // Duration is null when ffmpeg could not read the file - keep the audio unstretched
        // (same policy as the main pipeline) instead of NRE'ing into the generic error dialog.
        if (mediaInfo.Duration == null || mediaInfo.Duration.TotalMilliseconds <= p.DurationTotalMilliseconds + addDuration)
        {
            return new TtsStepResult
            {
                Paragraph = p,
                Text = item.Text,
                CurrentFileName = currentFile,
                SpeedFactor = 1.0f,
                Voice = item.Voice,
                EngineName = item.EngineName,
                Model = item.Model,
                Instruction = item.Instruction,
            };
        }

        var divisor = (decimal)(p.DurationTotalMilliseconds + addDuration);
        if (divisor <= 0)
        {
            return new TtsStepResult
            {
                Paragraph = p,
                Text = item.Text,
                CurrentFileName = item.CurrentFileName,
                SpeedFactor = 1.0f,
                Voice = item.Voice,
                EngineName = item.EngineName,
                Model = item.Model,
                Instruction = item.Instruction,
            };
        }

        // Step 3: Time-stretching
        var ext = ".wav";
        var factor = (decimal)mediaInfo.Duration.TotalMilliseconds / divisor;
        var outputFileName2 = Path.Combine(_waveFolder, $"{index}_{Guid.NewGuid()}{ext}");
        var overrideFileName = string.Empty;
        if (!string.IsNullOrEmpty(overrideFileName) && File.Exists(Path.Combine(_waveFolder, overrideFileName)))
        {
            outputFileName2 = Path.Combine(_waveFolder, $"{Path.GetFileNameWithoutExtension(overrideFileName)}_{Guid.NewGuid()}{ext}");
        }
        _tempAudioFiles.Add(outputFileName2);

        // Use rubberband (WSOLA) for high-quality stretch, or atempo as fallback
        Process speedProcess;
        if (doHighQualityStretch)
        {
            speedProcess = FfmpegGenerator.ChangeSpeedHighQuality(currentFile, outputFileName2, (float)factor);
        }
        else
        {
            speedProcess = FfmpegGenerator.ChangeSpeed(currentFile, outputFileName2, (float)factor);
        }
        await speedProcess.StartAndWaitAsync(_cancellationToken);

        // Fallback: if rubberband failed, retry with atempo
        if (doHighQualityStretch && (!File.Exists(outputFileName2) || new FileInfo(outputFileName2).Length == 0))
        {
            var fallbackProcess = FfmpegGenerator.ChangeSpeed(currentFile, outputFileName2, (float)factor);
            await fallbackProcess.StartAndWaitAsync(_cancellationToken);
        }

        return new TtsStepResult
        {
            Paragraph = p,
            Text = item.Text,
            CurrentFileName = outputFileName2,
            SpeedFactor = (float)factor,
            Voice = item.Voice,
            EngineName = item.EngineName,
            Model = item.Model,
            Instruction = item.Instruction,
        };
    }

    internal void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            // Closing mid-regenerate would publish/roll back a half-updated row and delete temp
            // files under the running pipeline - the window buttons are disabled for the same
            // reason; cancel the regenerate from its own popup instead.
            if (!IsRegenerateEnabled)
            {
                return;
            }

            Window?.Close();
        }
        else if (UiUtil.IsHelp(e))
        {
            e.Handled = true;
            UiUtil.ShowHelp("features/text-to-speech");
        }
        else if (e.Key == Key.R && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            RegenerateSelectedLine();
        }
    }

    /// <summary>
    /// Tunnel-stage keys (see the window's AddHandler): play/pause and regenerate must fire
    /// before the focused control gets the key, otherwise a focused button treats bare Space
    /// as a click - the initially focused OK button then published the session (#12093).
    /// </summary>
    internal void OnPreviewKeyDown(KeyEventArgs e)
    {
        // A modifier-less key must still type into a focused text box; with modifiers held the
        // shortcut works everywhere.
        var isTextBoxFocused = Window?.FocusManager?.GetFocusedElement() is TextBox;

        if (MatchesPlayPauseShortcut(e))
        {
            if (e.KeyModifiers == KeyModifiers.None && isTextBoxFocused)
            {
                return;
            }

            e.Handled = true;
            TogglePlayPauseSelectedRow();
        }
        else if (MainShortcutKeys.Matches(e, nameof(MainViewModel.ShowGoToVideoPositionCommand), []))
        {
            e.Handled = true;
            _ = ShowGoToPosition();
        }
        else if (e.Key == Key.R && e.KeyModifiers == KeyModifiers.None && !isTextBoxFocused)
        {
            // Bare R = regenerate the selected line: pairs with Space for fast keyboard-only
            // review (space to listen, R to redo), as requested in #12093.
            e.Handled = true;
            RegenerateSelectedLine();
        }
        else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                 (!isTextBoxFocused || !CanUndo))
        {
            // Undo a structural edit (a split). Left to the text box while it is focused and there
            // is no split to undo, so Ctrl+Z still undoes typing there.
            e.Handled = true;
            if (UndoCommand.CanExecute(null))
            {
                UndoCommand.Execute(null);
            }
        }
    }

    /// <summary>
    /// Tunnel-stage KeyUp twin of <see cref="OnPreviewKeyDown"/>. Avalonia's Button raises
    /// OnClick from OnKeyUp on Space whenever the button is focused - it does not check that
    /// the button also saw the KeyDown - so a handled KeyDown alone still let a focused button
    /// (OK!) click on Space release, both playing the line and publishing the session (#12093).
    /// </summary>
    internal void OnPreviewKeyUp(KeyEventArgs e)
    {
        var isTextBoxFocused = Window?.FocusManager?.GetFocusedElement() is TextBox;

        if (MatchesPlayPauseShortcut(e))
        {
            if (e.KeyModifiers == KeyModifiers.None && isTextBoxFocused)
            {
                return;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.R && e.KeyModifiers == KeyModifiers.None && !isTextBoxFocused)
        {
            e.Handled = true;
        }
    }

    private void RegenerateSelectedLine()
    {
        var line = SelectedLine;
        if (line == null || line.IsPlaying || !line.IsPlayingEnabled)
        {
            return;
        }

        if (RegenerateAudioCommand.CanExecute(line))
        {
            RegenerateAudioCommand.Execute(line);
        }
    }

    /// <summary>
    /// True when the pressed keys are this window's play/pause keys. Fixed to Space / Ctrl+Space
    /// here, not the user's main-window bindings: if the main window's play/pause was remapped
    /// (e.g. Space moved to "play selected lines" and F5 to play/pause), following it left this
    /// window with no Space to play the selected segment.
    /// </summary>
    private static bool MatchesPlayPauseShortcut(KeyEventArgs e)
    {
        return e.Key == Key.Space &&
               (e.KeyModifiers == KeyModifiers.None ||
                e.KeyModifiers == KeyModifiers.Control ||
                e.KeyModifiers == KeyModifiers.Meta);
    }

    private void TogglePlayPauseSelectedRow()
    {
        if (IsStopVisible || Lines.Any(l => l.IsPlaying))
        {
            Stop();
            return;
        }

        // Same play-head rule as Play: the clip under the cursor, or the next one when it is in a
        // gap between clips.
        var line = FindRowToPlayFromPlayhead() ?? SelectedLine;
        if (line is { IsPlayingEnabled: true } && PlayRowCommand.CanExecute(line))
        {
            PlayRowCommand.Execute(line);
        }
    }

    internal void SelectedEngineChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectedEngineChanged();
    }

    // True while ApplyLineToLeftPanelAsync is in the middle of switching the engine itself. We
    // skip the fire-and-forget refresh that the ComboBox's SelectionChanged event triggers in
    // that case, otherwise it races our own awaited refresh and wins — overwriting the row's
    // voice/model/instruction with the engine's defaults.
    private bool _suppressEngineRefreshDispatch;

    [RelayCommand]
    private async Task ShowEngineSettings()
    {
        if (Window == null)
        {
            return;
        }

        await TtsEngineSettingsDialog.ShowAsync(SelectedEngine, Window, _windowService);
    }

    public void SelectedEngineChanged()
    {
        if (_suppressEngineRefreshDispatch)
        {
            return;
        }

        // Fire-and-forget wrapper for the SelectionChanged event. Callers that need to wait for
        // the refresh (e.g. ApplyLineToLeftPanelAsync) should await SelectedEngineChangedAsync()
        // directly so they see the new Voices/Models populated before applying further changes.
        Dispatcher.UIThread.PostSafe(async () => await SelectedEngineChangedAsync());
    }

    public async Task SelectedEngineChangedAsync()
    {
        var engine = SelectedEngine;
        // No gear for ElevenLabs: its knobs live inline below the engine combo for fast per-line
        // tweaking, and the settings dialog behind the gear duplicated exactly those sliders -
        // two "settings windows" showing different values (#13881).
        IsEngineSettingsVisible = TtsEngineSettingsDialog.HasSettings(engine) && engine is not ElevenLabs;
        if (engine == null)
        {
            return;
        }

        // Guarded like the main window's engine switch: a throwing GetVoices was swallowed by
        // PostSafe and Voices.First() on an empty list (e.g. Qwen3 voice-clone model with no
        // imported reference WAVs) threw - either way the panel was left half-switched with the
        // previous engine's voices, and Regenerate handed the wrong engine's Voice to Speak.
        Voice[] voices;
        try
        {
            voices = await engine.GetVoices(SelectedLanguage?.Code ?? string.Empty);
        }
        catch (Exception ex)
        {
            SeLogger.Error(ex, $"ReviewSpeech: loading voices for {engine.Name} failed");
            voices = [];
        }

        Voices.Clear();
        foreach (var vo in voices)
        {
            Voices.Add(vo);
        }

        var lastVoice = Voices.FirstOrDefault(v => v.Name == Se.Settings.Video.TextToSpeech.Voice);
        if (lastVoice == null)
        {
            lastVoice = Voices.FirstOrDefault(p => p.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase) ||
                                                  p.Name.Contains("English", StringComparison.OrdinalIgnoreCase));
        }

        SelectedVoice = lastVoice ?? Voices.FirstOrDefault();

        if (engine.HasRegion)
        {
            var regions = await engine.GetRegions();
            Regions.Clear();
            foreach (var region in regions)
            {
                Regions.Add(region);
            }

            SelectedRegion = Regions.FirstOrDefault();
        }

        if (engine.HasModel)
        {
            var models = await engine.GetModels();
            Models.Clear();
            foreach (var model in models)
            {
                Models.Add(model);
            }

            SelectedModel = Models.FirstOrDefault();
        }

        IsElevenLabsControlsVisible = false;
        UpdateInstructionVisibility();
        LoadInstructionForEngine();
        if (engine is AzureSpeech)
        {
            SelectedRegion = Se.Settings.Video.TextToSpeech.AzureRegion;
            if (string.IsNullOrEmpty(SelectedRegion))
            {
                SelectedRegion = "westeurope";
            }
        }
        else if (engine is ElevenLabs)
        {
            IsElevenLabsControlsVisible = true;
            SelectedModel = Se.Settings.Video.TextToSpeech.ElevenLabsModel;
            if (string.IsNullOrEmpty(SelectedModel))
            {
                SelectedModel = Models.First();
            }
        }
        else if (engine is Qwen3TtsCpp)
        {
            SelectedModel = Models.FirstOrDefault(p => p == Se.Settings.Video.TextToSpeech.Qwen3TtsCppModel);
            if (string.IsNullOrEmpty(SelectedModel))
            {
                SelectedModel = Models.FirstOrDefault();
            }
        }
        else if (engine is ChatterboxTtsCpp)
        {
            SelectedModel = Models.FirstOrDefault(p => p == Se.Settings.Video.TextToSpeech.ChatterboxModel);
            if (string.IsNullOrEmpty(SelectedModel))
            {
                SelectedModel = Models.FirstOrDefault();
            }
        }

        // Languages last: the list depends on the model for some engines (ElevenLabs returns an
        // empty list for a null/unknown model), so the model - including the per-engine saved-model
        // overrides above - must be resolved first. This used to run before the model was loaded,
        // with null passed instead, leaving the language combo empty (#12093).
        if (engine.HasLanguageParameter && SelectedVoice != null)
        {
            var languages = await engine.GetLanguages(SelectedVoice, SelectedModel);
            Languages.Clear();
            foreach (var language in languages)
            {
                Languages.Add(language);
            }

            // Same preference order as SelectedModelChanged: the saved language, then English,
            // then whatever comes first.
            SelectedLanguage = ResolveSavedLanguage(engine);
        }
    }

    /// <summary>
    /// The language to preselect for <paramref name="engine"/> from the current
    /// <see cref="Languages"/> list. The CrispASR cloning engines lead with "Auto" and persist
    /// per-engine picks — restore those, and fall back to Auto (first entry) rather than English
    /// so an untouched language combo keeps the engine's pre-language-selection behaviour.
    /// Everything else keeps the saved-ElevenLabs → English → first order.
    /// </summary>
    private TtsLanguage? ResolveSavedLanguage(ITtsEngine engine) => engine switch
    {
        MossTtsCrispAsr => Languages.FirstOrDefault(p => p.Name == Se.Settings.Video.TextToSpeech.MossTtsCrispAsrLanguage)
                           ?? Languages.FirstOrDefault(),
        OmniVoiceCrispAsr => Languages.FirstOrDefault(p => p.Name == Se.Settings.Video.TextToSpeech.OmniVoiceCrispAsrLanguage)
                             ?? Languages.FirstOrDefault(),
        CosyVoice3CrispAsr => Languages.FirstOrDefault(p => p.Name == Se.Settings.Video.TextToSpeech.CosyVoice3CrispAsrLanguage)
                              ?? Languages.FirstOrDefault(),
        Qwen3TtsCrispAsr => Languages.FirstOrDefault(p => p.Name == Se.Settings.Video.TextToSpeech.Qwen3TtsCrispAsrLanguage)
                            ?? Languages.FirstOrDefault(),
        Confucius4TtsCrispAsr => Languages.FirstOrDefault(p => p.Name == Se.Settings.Video.TextToSpeech.Confucius4TtsCrispAsrLanguage)
                                 ?? Languages.FirstOrDefault(),
        _ => Languages.FirstOrDefault(p => p.Name == Se.Settings.Video.TextToSpeech.ElevenLabsLanguage)
             ?? Languages.FirstOrDefault(p => p.Code == "en")
             ?? Languages.FirstOrDefault(),
    };

    internal void SelectedLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        var engine = SelectedEngine;
        if (engine == null)
        {
            return;
        }

        if (engine is Murf murf)
        {
            Dispatcher.UIThread.PostSafe(async () =>
            {
                var voices = await murf.GetVoices(SelectedLanguage?.Code ?? string.Empty);
                Voices.Clear();
                Voices.AddRange(voices);

                var lastVoice = Voices.FirstOrDefault(v => v.Name == Se.Settings.Video.TextToSpeech.Voice);
                if (lastVoice == null)
                {
                    lastVoice = Voices.FirstOrDefault(p => p.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase) ||
                                                          p.Name.Contains("English", StringComparison.OrdinalIgnoreCase));
                }

                SelectedVoice = lastVoice ?? Voices.First();
            });
        }
    }

    internal void SelectedModelChanged(object? sender, SelectionChangedEventArgs e)
    {
        var engine = SelectedEngine;
        var voice = SelectedVoice;
        var model = SelectedModel;
        IsElevenLabsEngineV3Selected = false;
        UpdateInstructionVisibility();
        if (engine == null || voice == null || model == null)
        {
            return;
        }

        Dispatcher.UIThread.PostSafe(async () =>
        {
            if (engine is ElevenLabs && model == "eleven_v3")
            {
                IsElevenLabsEngineV3Selected = true;
            }

            if (engine.HasLanguageParameter)
            {
                var languages = await engine.GetLanguages(voice, model);
                Languages.Clear();
                foreach (var language in languages)
                {
                    Languages.Add(language);
                }

                SelectedLanguage = ResolveSavedLanguage(engine);
            }
        });
    }

    // ---- voice-design / instruction helpers -----------------------------------------------

    private static ObservableCollection<string> BuildKeywordOptions(string[] keywords)
    {
        var options = new ObservableCollection<string> { OmniVoiceAny };
        foreach (var k in keywords)
        {
            options.Add(k);
        }
        return options;
    }

    private static bool IsReal(string? value) => !string.IsNullOrEmpty(value) && value != OmniVoiceAny;

    partial void OnSelectedVoiceChanged(Voice? value) => UpdateInstructionVisibility();

    // When the user clicks a row, push that row's recorded engine/voice/model/instruction into
    // the left-side combos so they reflect what produced the selected line. Without this the
    // panel always shows the *current* (last-set) values, which is confusing when each line was
    // generated from a different per-actor mapping.
    //
    // Guard with _suppressSelectedLineSync so the chain of engine/model writes doesn't loop
    // back through SelectedEngineChanged (which would reset Voices and clobber our intended
    // voice).
    partial void OnSelectedLineChanged(ReviewRow? value)
    {
        // Re-center the waveform on the newly selected row regardless of the left-panel sync
        // state — the visual cue should follow the user's click immediately.
        RefreshWaveformPosition();

        if (value == null)
        {
            return;
        }

        // If another apply is in flight, queue this selection — we'll re-apply at the end of the
        // current cycle so the panel ends up on the row the user actually clicked last (not
        // wherever the in-flight apply ends).
        if (_suppressSelectedLineSync)
        {
            _pendingApplyRow = value;
            return;
        }

        _ = ApplyLineWithFollowUpAsync(value);
    }

    private async Task ApplyLineWithFollowUpAsync(ReviewRow row)
    {
        await ApplyLineToLeftPanelAsync(row);

        // Drain any selection changes that landed during the await. Loop until we catch up so
        // multiple rapid clicks all resolve to the latest.
        while (_pendingApplyRow != null && !ReferenceEquals(_pendingApplyRow, row))
        {
            var next = _pendingApplyRow;
            _pendingApplyRow = null;
            row = next;
            await ApplyLineToLeftPanelAsync(row);
        }
        _pendingApplyRow = null;
    }

    private bool _suppressSelectedLineSync;
    private ReviewRow? _pendingApplyRow;

    private async Task ApplyLineToLeftPanelAsync(ReviewRow row)
    {
        var step = row.StepResult;
        if (step == null)
        {
            return;
        }

        _suppressSelectedLineSync = true;
        try
        {
            ITtsEngine? targetEngine = null;
            if (!string.IsNullOrEmpty(step.EngineName))
            {
                targetEngine = Engines.FirstOrDefault(e => string.Equals(e.Name, step.EngineName, StringComparison.OrdinalIgnoreCase));
            }
            targetEngine ??= step.Voice != null
                ? Engines.FirstOrDefault(e => string.Equals(e.Name, SelectedEngine?.Name, StringComparison.OrdinalIgnoreCase))
                : SelectedEngine;

            // Refresh Voices / Models / Languages / Regions when:
            //   - The row's engine differs from the current engine, OR
            //   - We haven't loaded for this engine yet. After Initialize (Lines.Count > 0) the
            //     Loaded-time refresh is skipped, and if the first row's engine matches the
            //     caller-supplied SelectedEngine the engine-change branch above also skips —
            //     leaving Models empty. We detect that with `engine.HasModel && Models.Count==0`.
            //
            // Setting SelectedEngine also fires the ComboBox's SelectionChanged → which would
            // dispatch its own fire-and-forget refresh. We suppress that with the flag so the
            // two don't race; the awaited call below does the same work and lets us apply the
            // row's voice/model on top of it without it being immediately overwritten.
            var needsRefresh = targetEngine != null
                && (!ReferenceEquals(targetEngine, SelectedEngine)
                    || (targetEngine.HasModel && Models.Count == 0));
            if (needsRefresh)
            {
                _suppressEngineRefreshDispatch = true;
                try
                {
                    SelectedEngine = targetEngine;
                    await SelectedEngineChangedAsync();
                }
                finally
                {
                    _suppressEngineRefreshDispatch = false;
                }
            }

            // Voice: prefer the recorded Voice instance, otherwise match by name.
            if (step.Voice != null)
            {
                var match = Voices.FirstOrDefault(v => string.Equals(v.Name, step.Voice.Name, StringComparison.OrdinalIgnoreCase));
                SelectedVoice = match ?? step.Voice;
            }

            if (!string.IsNullOrEmpty(step.Model))
            {
                var matchModel = Models.FirstOrDefault(m => string.Equals(m, step.Model, StringComparison.OrdinalIgnoreCase));
                if (matchModel != null)
                {
                    SelectedModel = matchModel;
                }
            }

            Instruction = step.Instruction ?? string.Empty;
            UpdateInstructionVisibility();
            if (IsInstructionPickerVisible)
            {
                SyncOmniVoicePickerFromInstruction();
            }
        }
        finally
        {
            _suppressSelectedLineSync = false;
        }
    }

    partial void OnSelectedOmniVoiceGenderChanged(string value) => RebuildOmniVoiceInstruction();
    partial void OnSelectedOmniVoiceAgeChanged(string value) => RebuildOmniVoiceInstruction();
    partial void OnSelectedOmniVoicePitchChanged(string value) => RebuildOmniVoiceInstruction();
    partial void OnSelectedOmniVoiceAccentChanged(string value) => RebuildOmniVoiceInstruction();
    partial void OnOmniVoiceWhisperChanged(bool value) => RebuildOmniVoiceInstruction();

    private void RebuildOmniVoiceInstruction()
    {
        if (_suppressKeywordSync || !IsInstructionPickerVisible)
        {
            return;
        }

        var parts = new List<string>();
        if (IsReal(SelectedOmniVoiceGender))
        {
            parts.Add(SelectedOmniVoiceGender);
        }
        if (IsReal(SelectedOmniVoiceAge))
        {
            parts.Add(SelectedOmniVoiceAge);
        }
        if (IsReal(SelectedOmniVoicePitch))
        {
            parts.Add(SelectedOmniVoicePitch);
        }
        if (IsReal(SelectedOmniVoiceAccent))
        {
            parts.Add(SelectedOmniVoiceAccent);
        }
        if (OmniVoiceWhisper)
        {
            parts.Add(OmniVoiceTtsCpp.InstructionWhisper);
        }
        Instruction = string.Join(", ", parts);
    }

    private void SyncOmniVoicePickerFromInstruction()
    {
        var present = (Instruction ?? string.Empty)
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _suppressKeywordSync = true;
        SelectedOmniVoiceGender = MatchGroup(OmniVoiceTtsCpp.InstructionGenders, present);
        SelectedOmniVoiceAge = MatchGroup(OmniVoiceTtsCpp.InstructionAges, present);
        SelectedOmniVoicePitch = MatchGroup(OmniVoiceTtsCpp.InstructionPitches, present);
        SelectedOmniVoiceAccent = MatchGroup(OmniVoiceTtsCpp.InstructionAccents, present);
        OmniVoiceWhisper = present.Contains(OmniVoiceTtsCpp.InstructionWhisper);
        _suppressKeywordSync = false;

        RebuildOmniVoiceInstruction();
    }

    private static string MatchGroup(string[] group, HashSet<string> present)
        => Array.Find(group, present.Contains) ?? OmniVoiceAny;

    // Recomputes the three visibility/enabled flags from the current engine, model, and voice.
    // Call after any of those change. Mirrors RefreshInstructionVisibility +
    // UpdateOmniVoicePickerState in the main TTS VM.
    private void UpdateInstructionVisibility()
    {
        var engine = SelectedEngine;
        var model = SelectedModel;

        IsInstructionTextVisible =
            (engine is Qwen3TtsCpp && Qwen3TtsCpp.IsVoiceDesignModel(model))
            || (engine is Qwen3TtsCrispAsr && Qwen3TtsCrispAsr.IsVoiceDesignModel(model));
        IsInstructionPickerVisible = engine is OmniVoiceTtsCpp;
        HasInstruction = IsInstructionTextVisible || IsInstructionPickerVisible;

        var isDefaultOmniVoice = SelectedVoice?.EngineVoice is OmniVoice ov && string.IsNullOrEmpty(ov.FilePath);
        IsInstructionPickerEnabled = IsInstructionPickerVisible && isDefaultOmniVoice;
        IsInstructionVoiceHintVisible = IsInstructionPickerVisible && !isDefaultOmniVoice;
    }

    private void LoadInstructionForEngine()
    {
        Instruction = SelectedEngine switch
        {
            Qwen3TtsCpp => Se.Settings.Video.TextToSpeech.Qwen3TtsCppInstruction ?? string.Empty,
            Qwen3TtsCrispAsr => Se.Settings.Video.TextToSpeech.Qwen3TtsCppInstruction ?? string.Empty,
            OmniVoiceTtsCpp => Se.Settings.Video.TextToSpeech.OmniVoiceTtsCppInstruction ?? string.Empty,
            _ => string.Empty,
        };

        if (IsInstructionPickerVisible)
        {
            SyncOmniVoicePickerFromInstruction();
        }
    }

    /// <summary>
    /// Drops the playback player and destroys its mpv core on a worker thread.
    /// <para>
    /// <c>mpv_terminate_destroy</c> runs libmpv's native win32/audio deinit, which must not happen
    /// on the UI thread while a window is closing: it leaves <c>Window.CloseInternal()</c> to make
    /// its next COM call (<c>IFrameworkInputPane.Unadvise</c>) into an apartment libmpv's teardown
    /// has already disturbed - an access violation no catch block can intercept. Same reasoning,
    /// and same off-thread cure, as <c>TextToSpeechViewModel.DisposePreviewPlayer</c> (#13376) and
    /// <c>VideoPlayerControl.CloseAndDisposePlayer</c> (#11176).
    /// </para>
    /// Safe to call more than once - the second call finds no player and does nothing.
    /// </summary>
    private void DisposePlayerOffThread()
    {
        LibMpvDynamicPlayer? player;
        lock (_playLock)
        {
            player = _mpvContext;
            _mpvContext = null;
        }

        if (player == null)
        {
            return;
        }

        Se.WriteToolsLog("TTS review: disposing playback player (mpv) on worker thread");
        Task.Run(() =>
        {
            try
            {
                // Stop first so the core tears down from an idle state instead of mid-playback.
                player.Stop();
                player.Dispose();
                Se.WriteToolsLog("TTS review: playback player (mpv) destroyed");
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "ReviewSpeech: disposing the audio player failed");
            }
        });
    }

    internal void OnClosing(WindowClosingEventArgs e)
    {
        // Title-bar X / Alt+F4 bypass the Escape guard, so a regenerate can still be in flight
        // here. Cancel it, but leave its popup-owned CTS undisposed (the popup's Cancel button
        // still holds it) and skip the temp-file sweep below - the unwinding pipeline may still
        // be writing those files.
        var regenerateInFlight = !IsRegenerateEnabled;
        Se.WriteToolsLog($"TTS review: window closing (regenerate in flight={regenerateInFlight})");

        _skipAutoContinue = true;
        _isClosing = true;
        _timer.StopAndDispose(OnTimerOnElapsed);
        _cursorTimer?.Dispose();
        _cursorTimer = null;
        try { _cancellationTokenSource.Cancel(); } catch (ObjectDisposedException) { }
        try { _ttsWaveformCts?.Cancel(); } catch (ObjectDisposedException) { }
        if (!regenerateInFlight)
        {
            try { _cancellationTokenSource.Dispose(); } catch (ObjectDisposedException) { }
        }

        DisposePlayerOffThread();

        // The waveform mirrors subscribe to PropertyChanged in Initialize so drags
        // on the visualizer write back into the per-row TtsStepResult. Detach now
        // so the mirror VMs (and the rows they reference) become collectible.
        foreach (var wp in WaveformParagraphs)
        {
            wp.PropertyChanged -= OnWaveformParagraphChanged;
        }
        WaveformParagraphs.Clear();
        _waveformParagraphToRow.Clear();

        // Intermediate WAVs from TrimAndAdjustSpeed land in _waveFolder and would
        // otherwise pile up across regenerate cycles. Best-effort — _waveFolder is
        // usually a session-scoped temp dir but a stray locked file shouldn't tank
        // window close. A regenerated row's *final* audio file is tracked here too,
        // and on OK it was just published via StepResults for the merge step —
        // deleting it would silently break that line in the final audio — so keep
        // anything a published result still references.
        var publishedFiles = OkPressed
            ? new HashSet<string>(StepResults.Select(r => r.CurrentFileName), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!regenerateInFlight)
        {
            foreach (var f in _tempAudioFiles)
            {
                if (publishedFiles.Contains(f))
                {
                    continue;
                }

                try { if (File.Exists(f))
                {
                    File.Delete(f);
                } } catch { /* ignore */ }
            }
            _tempAudioFiles.Clear();
        }

        UiUtil.SaveWindowPosition(Window);
    }

    internal void LineGridDoubleClicked()
    {
        var line = SelectedLine;
        if (line == null || line.IsPlaying || !line.IsPlayingEnabled)
        {
            return;
        }

        _ = PlayRow(line);
    }

    internal void Loaded()
    {
        UiUtil.RestoreWindowPosition(Window);
        RefreshWaveformPosition();
    }
}