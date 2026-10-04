using CommunityToolkit.Mvvm.ComponentModel;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Main;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;

public partial class ReviewRow : ObservableObject
{
    [ObservableProperty] private bool _include;
    [ObservableProperty] private int _number;
    [ObservableProperty] private string _voice;
    [ObservableProperty] private string _cps;
    [ObservableProperty] private string _speed;
    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _hasHistory;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isPlayingEnabled;

    public TtsStepResult StepResult { get; set; }
    public List<ReviewHistoryRow> HistoryItems { get; set; }

    // Mirror of StepResult.Paragraph used by the AudioVisualizer in ReviewSpeechWindow. Stored
    // on the row itself so lookup is direct (no Lines.IndexOf, which would break under sorting).
    public SubtitleLineViewModel? WaveformParagraph { get; set; }

    // Snapshots taken when the window opens, used on OK to detect text edits and map them back
    // to the main subtitle (see ReviewTextChange). The times must be captured up front because
    // waveform drags mutate StepResult.Paragraph while the window is open.
    public string OriginalText { get; set; } = string.Empty;
    public double OriginalStartMs { get; set; }
    public double OriginalEndMs { get; set; }

    public ReviewRow()
    {
        Include = true;
        Number = 0;
        Voice = string.Empty;
        Cps = string.Empty;
        Speed = string.Empty;
        Text = string.Empty;
        StepResult = new TtsStepResult();
        HistoryItems = new List<ReviewHistoryRow>();
        HasHistory = false;
    }

    internal void StartHistory()
    {
        HistoryItems.Add(new ReviewHistoryRow
        {
            Number = 1,
            FileName = StepResult.CurrentFileName,
            VoiceName = StepResult.Voice?.Name ?? string.Empty,
            Voice = StepResult.Voice,
            Speed = StepResult.SpeedFactor,
            EngineName = StepResult.EngineName,
            Model = StepResult.Model,
            Instruction = StepResult.Instruction,
        });
    }

    internal void AddHistory(Voices.Voice voice, string processedFileName, string engineName, string model, string instruction)
    {
        HistoryItems.Add(new ReviewHistoryRow
        {
            Number = HistoryItems.Count + 1,
            FileName = processedFileName,
            VoiceName = voice.Name,
            Voice = voice,
            Speed = StepResult.SpeedFactor,
            EngineName = engineName,
            Model = model,
            Instruction = instruction,
        });

        HasHistory = true;
    }

    public double HistoryButtonOpacity => HasHistory ? 1.0 : 0.3;

    partial void OnHasHistoryChanged(bool value)
    {
        OnPropertyChanged(nameof(HistoryButtonOpacity));
    }

    // Deep-enough copy for the Undo stack: the row plus its StepResult (with a fresh Paragraph)
    // and a copy of the history list, so restoring a snapshot brings back text, times, clip,
    // voice, model, engine, include flag and take history exactly.
    public ReviewRow Clone()
    {
        var paragraph = StepResult.Paragraph;
        var step = new TtsStepResult
        {
            Paragraph = new Paragraph
            {
                Number = paragraph.Number,
                Text = paragraph.Text,
                StartTime = new TimeCode(paragraph.StartTime.TotalMilliseconds),
                EndTime = new TimeCode(paragraph.EndTime.TotalMilliseconds),
            },
            Text = StepResult.Text,
            CurrentFileName = StepResult.CurrentFileName,
            SpeedFactor = StepResult.SpeedFactor,
            Voice = StepResult.Voice,
            EngineName = StepResult.EngineName,
            Model = StepResult.Model,
            Instruction = StepResult.Instruction,
            Include = StepResult.Include,
        };

        return new ReviewRow
        {
            Include = Include,
            Number = Number,
            Voice = Voice,
            Cps = Cps,
            Speed = Speed,
            Text = Text,
            HasHistory = HasHistory,
            StepResult = step,
            HistoryItems = new List<ReviewHistoryRow>(HistoryItems),
            OriginalText = OriginalText,
            OriginalStartMs = OriginalStartMs,
            OriginalEndMs = OriginalEndMs,
        };
    }
}