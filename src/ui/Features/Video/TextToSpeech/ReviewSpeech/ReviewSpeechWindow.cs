using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Nikse.SubtitleEdit.Controls.AudioVisualizerControl;
using Nikse.SubtitleEdit.Features.Main.Layout;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.ElevenLabsSettings;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.Engines;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.ValueConverters;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;

public class ReviewSpeechWindow : Window
{
    private readonly ReviewSpeechViewModel _vm;

    public ReviewSpeechWindow(ReviewSpeechViewModel vm)
    {
        UiUtil.InitializeWindow(this, GetType().Name);
        Title = Se.Language.Video.TextToSpeech.ReviewAudioSegments;
        Width = 1100;
        Height = 700;
        MinWidth = 700;
        MinHeight = 690;
        CanResize = true;

        _vm = vm;
        vm.Window = this;
        DataContext = vm;

        var controls = MakeControls(vm);
        var lineGridView = MakeLineGrid(vm);
        var waveform = MakeWaveform(vm);

        // Disabled while a regenerate runs: its progress popup is non-modal, and publishing
        // (OK/Export) or closing mid-run would commit the row's half-updated step result.
        var buttonUndo = UiUtil.MakeButton(vm.UndoCommand, IconNames.Restore, Se.Language.General.Undo)
            .WithBindEnabled(nameof(vm.CanUndo));
        var buttonExport = UiUtil.MakeButton(Se.Language.General.ExportDotDotDot, vm.ExportCommand).WithBindEnabled(nameof(vm.IsRegenerateEnabled));
        var buttonOk = UiUtil.MakeButtonOk(vm.OkCommand).WithBindEnabled(nameof(vm.IsRegenerateEnabled));
        var buttonCancel = UiUtil.MakeButtonCancel(vm.CancelCommand).WithBindEnabled(nameof(vm.IsRegenerateEnabled));
        var panelButtons = UiUtil.MakeButtonBar(buttonUndo, buttonExport, buttonOk, buttonCancel);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Margin = UiUtil.MakeWindowMargin(),
            ColumnSpacing = 10,
            RowSpacing = 10,
            Width = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var checkBoxAutoContinue = new CheckBox
        {
            Content = Se.Language.Video.TextToSpeech.AutoContinuePlaying,
            [!CheckBox.IsCheckedProperty] = new Binding(nameof(vm.AutoContinue)) { Mode = BindingMode.TwoWay },
        };

        grid.Add(controls, 0, 0);
        grid.Add(lineGridView, 0, 1);
        grid.Add(waveform, 1, 0, 1, 2);
        grid.Add(panelButtons, 2, 0, 1, 2);
        grid.Add(checkBoxAutoContinue, 2, 0);
        grid.Add(MakePositionLabel(vm), 2, 0, 1, 2);

        Content = grid;

        // Focus the grid, not a button, so the window receives key events (OnKeyDown needs a
        // focused element) without arming any button: a focused button fires OnClick on bare
        // Space/Enter, and OK used to be focused here - so the first Space a user pressed
        // published the whole session instead of playing the selected line (#12093).
        UiUtil.FocusOnFirstActivation(this, () => { TableViewExtras.FocusRow(vm.LineGrid); });

        // Tunnel-stage handlers: see Space/R before the focused control does. KeyDown alone is
        // not enough - Avalonia's Button fires OnClick from OnKeyUp on Space (unconditionally
        // when focused, no IsPressed check), so a focused button still clicked on Space release
        // even with the KeyDown handled (#12093).
        AddHandler(KeyDownEvent, (_, e) => vm.OnPreviewKeyDown(e), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => vm.OnPreviewKeyUp(e), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Loaded += delegate
        {
            vm.Loaded();
            // When Initialize already selected the first row (Lines.Count > 0), that selection
            // has already kicked off ApplyLineToLeftPanelAsync which loads the right engine's
            // voices/models for the row. Firing SelectedEngineChanged here would post another
            // fire-and-forget refresh that races (and wins against) the row sync, replacing the
            // row's voice/model/instruction with the engine's defaults.
            if (vm.Lines.Count == 0)
            {
                vm.SelectedEngineChanged();
            }
        };
    }

    private static Border MakeLineGrid(ReviewSpeechViewModel vm)
    {
        var lineGrid = TableViewExtras.MakeTableView(multiSelect: false);
        lineGrid.Margin = new Thickness(0, 10, 0, 0);
        lineGrid.Width = double.NaN;
        lineGrid.Height = double.NaN;
        lineGrid[!TableView.ItemsSourceProperty] = new Binding(nameof(vm.Lines));
        lineGrid[!TableView.SelectedItemProperty] = new Binding(nameof(vm.SelectedLine)) { Mode = BindingMode.TwoWay };

        // Re-enabled: OK publishes only rows with Include ticked and Export/Import
        // round-trip the flag, so without this column an imported session's excluded
        // rows were invisible and could never be re-included.
        lineGrid.Columns.Add(new SeTableViewColumn
        {
            Header = Se.Language.General.Enabled,
            CellTheme = UiUtil.TableViewNoPaddingCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
            CellTemplate = new FuncDataTemplate<ReviewRow>((item, _) =>
                new Border
                {
                    Background = Brushes.Transparent, // Prevents highlighting
                    Padding = new Thickness(4),
                    Child = new CheckBox
                    {
                        [!ToggleButton.IsCheckedProperty] = new Binding(nameof(ReviewRow.Include)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }),
            Width = new GridLength(80),
        });
        lineGrid.Columns.Add(new SeTableViewColumn
        {
            CellTheme = UiUtil.TableViewNoPaddingCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
            CellTemplate = new FuncDataTemplate<ReviewRow>((item, _) =>
            {
                var buttonRegenerate = UiUtil.MakeButton(vm.RegenerateAudioCommand, IconNames.Recycle, Se.Language.Video.TextToSpeech.RegenerateAudio)
                .WithBindEnabled(nameof(item.IsPlayingEnabled));
                buttonRegenerate.CommandParameter = item;

                var buttonHistory = UiUtil.MakeButton(vm.ShowHistoryCommand, IconNames.DotsVertical, Se.Language.General.ShowHistory).WithBindEnabled(nameof(ReviewRow.HasHistory));
                buttonHistory.CommandParameter = item;
                buttonHistory.Bind(Button.OpacityProperty, new Binding(nameof(ReviewRow.HistoryButtonOpacity)));

                // Split the line at the play-head / text caret into two regenerated rows.
                var buttonSplit = UiUtil.MakeButton(vm.SplitLineCommand, IconNames.ContentCut, Se.Language.General.SplitLineAtVideoAndTextBoxPosition)
                    .WithBindEnabled(nameof(item.IsPlayingEnabled));
                buttonSplit.CommandParameter = item;

                var buttonPlay = UiUtil.MakeButton(vm.PlayRowCommand,"fa-solid fa-play")
                .WithBindIsVisible(nameof(item.IsPlaying), InverseBooleanConverter.Instance)
                .WithBindEnabled(nameof(item.IsPlayingEnabled));
                buttonPlay.CommandParameter = item;

                var buttonStop = UiUtil.MakeButton(vm.StopCommand, "fa-solid fa-stop")
                .WithBindIsVisible(nameof(item.IsPlaying));
                buttonStop.CommandParameter = item;

                return new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 5,
                    Children =
                    {
                        buttonRegenerate,
                        buttonHistory,
                        buttonSplit,
                        buttonPlay,
                        buttonStop,
                    }
                };
            }),
            Width = new GridLength(190),
        });
        lineGrid.Columns.Add(new SeTableViewColumn
        {
            Header = Se.Language.General.NumberSymbol,
            Binding = new Binding(nameof(ReviewRow.Number)),
            Width = new GridLength(50),
            CellTheme = UiUtil.TableViewCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
        });
        lineGrid.Columns.Add(new SeTableViewColumn
        {
            Header = Se.Language.General.Voice,
            Binding = new Binding(nameof(ReviewRow.Voice)),
            Width = new GridLength(150),
            CellTheme = UiUtil.TableViewCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
        });
        lineGrid.Columns.Add(new SeTableViewColumn
        {
            Header = Se.Language.General.CharsPerSec,
            Binding = new Binding(nameof(ReviewRow.Cps)),
            Width = new GridLength(80),
            CellTheme = UiUtil.TableViewCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
        });
        lineGrid.Columns.Add(new SeTableViewColumn
        {
            Header = Se.Language.General.Speed,
            Binding = new Binding(nameof(ReviewRow.Speed)),
            Width = new GridLength(70),
            CellTheme = UiUtil.TableViewCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
        });
        lineGrid.Columns.Add(new SeTableViewColumn
        {
            Header = Se.Language.General.Text,
            Binding = new Binding(nameof(ReviewRow.Text)),
            Width = new GridLength(1, GridUnitType.Star),
            CellTheme = UiUtil.TableViewCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
        });
        lineGrid.DoubleTapped += (s, e) => vm.LineGridDoubleClicked();
        vm.LineGrid = lineGrid;

        var textBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
            [!TextBox.TextProperty] = new Binding(nameof(vm.SelectedLine) + "." + nameof(ReviewRow.Text))
            {
                Mode = BindingMode.TwoWay
            },
            FontSize = Se.Settings.Appearance.SubtitleTextBoxFontSize,
            FontWeight = Se.Settings.Appearance.SubtitleTextBoxFontBold ? FontWeight.Bold : FontWeight.Normal,
            Margin = new Thickness(0, 0, 0, 3),
        };
        if (!string.IsNullOrEmpty(Se.Settings.Appearance.SubtitleTextBoxAndGridFontName))
        {
            textBox.FontFamily = FontFamilyHelper.Make(Se.Settings.Appearance.SubtitleTextBoxAndGridFontName);
        }
        textBox.WithAccessibleName(Se.Language.General.Text); // edits the selected row's text; no visible label (#12087)
        vm.EditTextBox = textBox; // split reads the caret from here

        // Right-click in the text box splits at the caret + play-head, mirroring the main window.
        // The caret (not a clicked row) is the text split point, so the menu belongs on the box.
        var textBoxFlyout = new MenuFlyout { Placement = PlacementMode.Pointer };
        var menuSplitAtCaret = new MenuItem
        {
            Header = Se.Language.General.SplitLineAtVideoAndTextBoxPosition,
            Command = vm.SplitLineCommand,
        };
        textBoxFlyout.Items.Add(menuSplitAtCaret);
        textBox.ContextFlyout = textBoxFlyout;
        textBoxFlyout.Opening += (_, _) => menuSplitAtCaret.CommandParameter = vm.SelectedLine;

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Width = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        grid.Add(lineGrid, 0, 0);
        grid.Add(textBox, 1, 0);

        return UiUtil.MakeBorderForControl(grid);
    }

    private static Border MakeControls(ReviewSpeechViewModel vm)
    {
        var labelMinWidth = 100;
        var controlMinWidth = 200;

        var comboBoxEngines = UiUtil.MakeComboBox(vm.Engines, vm, nameof(vm.SelectedEngine)).WithMinWidth(controlMinWidth);
        comboBoxEngines.SelectionChanged += vm.SelectedEngineChanged;
        var buttonEngineSettings = UiUtil.MakeButton(string.Empty, vm.ShowEngineSettingsCommand)
            .WithIconLeft(IconNames.Settings)
            .WithBindIsVisible(nameof(vm.IsEngineSettingsVisible));
        if (Se.Settings.Appearance.ShowHints)
        {
            ToolTip.SetTip(buttonEngineSettings, Se.Language.General.Settings);
        }

        var buttonElevenLabsRest = UiUtil.MakeButton(Se.Language.General.Reset, vm.ElevenLabsResetCommand)
            .WithIconLeft(IconNames.Repeat)
            .WithBindIsVisible(nameof(vm.IsElevenLabsControlsVisible));
        if (Se.Settings.Appearance.ShowHints)
        {
            ToolTip.SetTip(buttonElevenLabsRest, Se.Language.Video.TextToSpeech.ElevenLabsSettingsResetHint);
        }

        var panelEngine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                new Label
                {
                    Content = Se.Language.General.Engine,
                    MinWidth = labelMinWidth,
                },
                comboBoxEngines,
                buttonEngineSettings,
                buttonElevenLabsRest,
            }
        };

        var panelVoice = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                new Label
                {
                    Content = Se.Language.General.Voice,
                    MinWidth = labelMinWidth,
                },
                UiUtil.MakeComboBox(vm.Voices, vm, nameof(vm.SelectedVoice)).WithWidth(controlMinWidth),
            }
        };

        var comboBoxModels = UiUtil.MakeComboBox(vm.Models, vm, nameof(vm.SelectedModel)).WithWidth(controlMinWidth);
        var panelModel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                new Label
                {
                    Content = Se.Language.General.Model,
                    MinWidth = labelMinWidth,
                },
                comboBoxModels,
                UiUtil.MakeButton(vm.ShowElevenLabsEngineV3HelpCommand, IconNames.Help, $"{Se.Language.General.Model} - {Se.Language.General.Help}")
                    .WithBindIsVisible(nameof(vm.IsElevenLabsEngineV3Selected))
                    .WithMarginLeft(5),
            },
            [!StackPanel.IsVisibleProperty] = new Binding(nameof(vm.SelectedEngine) + "." + nameof(ITtsEngine.HasModel)) { Mode = BindingMode.OneWay },
        };
        comboBoxModels.SelectionChanged += vm.SelectedModelChanged;

        var panelRegion = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                new Label
                {
                    Content = Se.Language.General.Region,
                    MinWidth = labelMinWidth,
                },
                UiUtil.MakeComboBox(vm.Regions, vm, nameof(vm.SelectedRegion)).WithWidth(controlMinWidth),
            },
            [!StackPanel.IsVisibleProperty] = new Binding(nameof(vm.SelectedEngine) + "." + nameof(ITtsEngine.HasRegion)) { Mode = BindingMode.OneWay },
        };

        var comboBoxLanguages = UiUtil.MakeComboBox(vm.Languages, vm, nameof(vm.SelectedLanguage)).WithWidth(controlMinWidth);
        var panelLanguage = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                new Label
                {
                    Content = Se.Language.General.Language,
                    MinWidth = labelMinWidth,
                },
                comboBoxLanguages,
            },
            [!StackPanel.IsVisibleProperty] = new Binding(nameof(vm.SelectedEngine) + "." + nameof(ITtsEngine.HasLanguageParameter)) { Mode = BindingMode.OneWay },
        };
        comboBoxLanguages.SelectionChanged += vm.SelectedLanguageChanged;


        var elevenLabsControls = MakeElevenLabsControls(vm);
        var panelInstruction = MakeInstructionPanel(vm, labelMinWidth);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // filler
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
            },
            ColumnSpacing = 10,
            Width = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 15),
        };

        grid.Add(panelEngine, 0, 0);
        // Model comes before Voice — same ordering as the main TTS window and Cast dialog so the
        // user picks a model first (which sometimes filters the voice list) and the dropdowns
        // line up across windows.
        grid.Add(panelModel, 1, 0);
        grid.Add(panelVoice, 2, 0);
        grid.Add(panelRegion, 3, 0);
        grid.Add(panelLanguage, 4, 0);
        grid.Add(elevenLabsControls, 5, 0);
        grid.Add(panelInstruction, 6, 0);
        // 7 is filler

        return UiUtil.MakeBorderForControl(grid);
    }

    // Voice-design controls shared with the main TTS window: free-text instruction (Qwen3
    // VoiceDesign model) and OmniVoice keyword picker. Visibility flags on the VM mirror those
    // in TextToSpeechViewModel — see ReviewSpeechViewModel.UpdateInstructionVisibility.
    private static StackPanel MakeInstructionPanel(ReviewSpeechViewModel vm, int labelMinWidth)
    {
        var textBoxInstruction = new TextBox
        {
            Width = 260,
            Height = 90,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            PlaceholderText = Se.Language.Video.TextToSpeech.VoiceInstructionHint,
            DataContext = vm,
            [!TextBox.IsVisibleProperty] = new Binding(nameof(vm.IsInstructionTextVisible)) { Mode = BindingMode.OneWay },
        };
        textBoxInstruction.Bind(TextBox.TextProperty, new Binding(nameof(vm.Instruction))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                new Label
                {
                    Content = Se.Language.Video.TextToSpeech.VoiceInstruction,
                    MinWidth = labelMinWidth,
                    VerticalAlignment = VerticalAlignment.Top,
                },
                textBoxInstruction,
                MakeInstructionKeywordPicker(vm),
            },
            [!StackPanel.IsVisibleProperty] = new Binding(nameof(vm.HasInstruction)) { Mode = BindingMode.OneWay },
        };
    }

    // OmniVoice keyword picker — gender/age/pitch/accent combos plus a whisper checkbox and a
    // "doesn't apply to cloned voices" hint. Mirrors the picker in the main TTS window so the
    // user sees the same control set regardless of which window they regenerate from.
    private static Control MakeInstructionKeywordPicker(ReviewSpeechViewModel vm)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
            },
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
            },
            ColumnSpacing = 8,
            RowSpacing = 6,
            [!Grid.IsEnabledProperty] = new Binding(nameof(vm.IsInstructionPickerEnabled)) { Mode = BindingMode.OneWay },
        };

        AddPickerRow(grid, 0, Se.Language.Video.TextToSpeech.VoiceGender, vm, vm.OmniVoiceGenders, nameof(vm.SelectedOmniVoiceGender));
        AddPickerRow(grid, 1, Se.Language.Video.TextToSpeech.VoiceAge, vm, vm.OmniVoiceAges, nameof(vm.SelectedOmniVoiceAge));
        AddPickerRow(grid, 2, Se.Language.Video.TextToSpeech.VoicePitch, vm, vm.OmniVoicePitches, nameof(vm.SelectedOmniVoicePitch));
        AddPickerRow(grid, 3, Se.Language.Video.TextToSpeech.VoiceAccent, vm, vm.OmniVoiceAccents, nameof(vm.SelectedOmniVoiceAccent));

        var whisper = new CheckBox
        {
            Content = OmniVoiceTtsCpp.InstructionWhisper,
            DataContext = vm,
            Margin = new Thickness(0, 4, 0, 0),
        };
        whisper.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(vm.OmniVoiceWhisper)) { Mode = BindingMode.TwoWay });
        grid.Add(whisper, 4, 0, 1, 2);

        var clonedVoiceNote = new TextBlock
        {
            Text = Se.Language.Video.TextToSpeech.VoiceInstructionClonedVoiceNote,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.7,
            MaxWidth = 280,
            Margin = new Thickness(0, 6, 0, 0),
            [!TextBlock.IsVisibleProperty] = new Binding(nameof(vm.IsInstructionVoiceHintVisible)) { Mode = BindingMode.OneWay },
        };

        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children = { grid, clonedVoiceNote },
            [!StackPanel.IsVisibleProperty] = new Binding(nameof(vm.IsInstructionPickerVisible)) { Mode = BindingMode.OneWay },
        };
    }

    private static void AddPickerRow(Grid grid, int row, string label, ReviewSpeechViewModel vm,
        System.Collections.ObjectModel.ObservableCollection<string> items, string selectedPropertyPath)
    {
        grid.Add(new Label { Content = label, MinWidth = 60, VerticalAlignment = VerticalAlignment.Center }, row, 0);
        grid.Add(UiUtil.MakeComboBox(items, vm, selectedPropertyPath).WithWidth(200), row, 1);
    }

    private static Grid MakeElevenLabsControls(ReviewSpeechViewModel vm)
    {
        var sliderWidth = 150;

        var labelStability = UiUtil.MakeLabel(Se.Language.Video.TextToSpeech.Stability);
        var sliderStability = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = vm.Stability,
            Width = sliderWidth,
            [!Slider.ValueProperty] = new Binding(nameof(vm.Stability)),
        };

        var labelStabilityValue = UiUtil.MakeLabel().WithBindText(vm, nameof(vm.Stability), new DoubleToTwoDecimalConverter());
        var buttonStability = UiUtil.MakeButton(vm.ShowStabilityHelpCommand, IconNames.Help, $"{Se.Language.Video.TextToSpeech.Stability} - {Se.Language.General.Help}");

        var labelSimilarity = UiUtil.MakeLabel(Se.Language.Video.TextToSpeech.Similarity);
        var sliderSimilarity = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = vm.Similarity,
            Width = sliderWidth,
            [!Slider.ValueProperty] = new Binding(nameof(vm.Similarity)),
        };
        var labelSimilarityValue = UiUtil.MakeLabel().WithBindText(vm, nameof(vm.Similarity), new DoubleToTwoDecimalConverter());
        var buttonSimilarity = UiUtil.MakeButton(vm.ShowSimilarityHelpCommand, IconNames.Help, $"{Se.Language.Video.TextToSpeech.Similarity} - {Se.Language.General.Help}");

        var labelSpeakerBoost = UiUtil.MakeLabel(Se.Language.Video.TextToSpeech.SpeakerBoost);
        var sliderSpeakerBoost = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = vm.SpeakerBoost,
            Width = sliderWidth,
            [!Slider.ValueProperty] = new Binding(nameof(vm.SpeakerBoost)),
        };
        var labelSpeakerBoostValue = UiUtil.MakeLabel().WithBindText(vm, nameof(vm.SpeakerBoost), new DoubleToTwoDecimalConverter());
        var buttonSpeakerBoost = UiUtil.MakeButton(vm.ShowSpeakerBoostHelpCommand, IconNames.Help, $"{Se.Language.Video.TextToSpeech.SpeakerBoost} - {Se.Language.General.Help}");

        var labelSpeed = UiUtil.MakeLabel(Se.Language.General.Speed);
        var sliderSpeed = new Slider
        {
            Minimum = 0.7,
            Maximum = 1.2,
            Value = vm.Speed,
            Width = sliderWidth,
            [!Slider.ValueProperty] = new Binding(nameof(vm.Speed)),
        };
        var labelSpeedValue = UiUtil.MakeLabel().WithBindText(vm, nameof(vm.Speed), new DoubleToTwoDecimalConverter());
        var buttonSpeed = UiUtil.MakeButton(vm.ShowSpeedHelpCommand, IconNames.Help, $"{Se.Language.General.Speed} - {Se.Language.General.Help}");

        var labelStyleExaggeration = UiUtil.MakeLabel(Se.Language.General.StyleExaggeration);
        var sliderStyleExaggeration = new Slider
        {
            Minimum = 0.0,
            Maximum = 1.0,
            Value = vm.StyleExaggeration,
            Width = sliderWidth,
            Margin = new Thickness(5, 0, 0, 0),
            [!Slider.ValueProperty] = new Binding(nameof(ElevenLabsSettingsViewModel.StyleExaggeration)),
        };
        var labelStyleExaggerationValue = UiUtil.MakeLabel().WithBindText(vm, nameof(vm.StyleExaggeration), new DoubleToTwoDecimalConverter());
        var buttonStyleExaggeration = UiUtil.MakeButton(vm.ShowStyleExaggerationHelpCommand, IconNames.Help, $"{Se.Language.General.StyleExaggeration} - {Se.Language.General.Help}");

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
            },
            Margin = UiUtil.MakeWindowMargin(),
            ColumnSpacing = 5,
            Width = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            [!Grid.IsVisibleProperty] = new Binding(nameof(vm.IsElevenLabsControlsVisible)) { Mode = BindingMode.OneWay },
        };

        grid.Add(labelStability, 0, 0);
        grid.Add(sliderStability, 0, 1);
        grid.Add(labelStabilityValue, 0, 2);
        grid.Add(buttonStability, 0, 3);

        grid.Add(labelSimilarity, 1, 0);
        grid.Add(sliderSimilarity, 1, 1);
        grid.Add(labelSimilarityValue, 1, 2);
        grid.Add(buttonSimilarity, 1, 3);

        grid.Add(labelSpeakerBoost, 2, 0);
        grid.Add(sliderSpeakerBoost, 2, 1);
        grid.Add(labelSpeakerBoostValue, 2, 2);
        grid.Add(buttonSpeakerBoost, 2, 3);

        grid.Add(labelSpeed, 3, 0);
        grid.Add(sliderSpeed, 3, 1);
        grid.Add(labelSpeedValue, 3, 2);
        grid.Add(buttonSpeed, 3, 3);

        grid.Add(labelStyleExaggeration, 4, 0);
        grid.Add(sliderStyleExaggeration, 4, 1);
        grid.Add(labelStyleExaggerationValue, 4, 2);
        grid.Add(buttonStyleExaggeration, 4, 3);

        return grid;
    }

    private static TextBlock MakePositionLabel(ReviewSpeechViewModel vm)
    {
        var label = new TextBlock
        {
            [!TextBlock.TextProperty] = new Binding(nameof(vm.PositionText)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 16,
            FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") },
            Background = Brushes.Transparent, // hit-testable between the digits
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        if (Se.Settings.Appearance.ShowHints)
        {
            ToolTip.SetTip(label, Se.Language.Video.GoToVideoPositionDotDotDot);
        }

        label.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(label).Properties.IsLeftButtonPressed)
            {
                e.Handled = true;
                vm.ShowGoToPositionCommand.Execute(null);
            }
        };

        return label;
    }

    private static Border MakeWaveform(ReviewSpeechViewModel vm)
    {
        var audioVisualizer = CreateWaveformVisualizer();
        var audioVisualizerTts = CreateWaveformVisualizer();

        vm.AudioVisualizer = audioVisualizer;
        vm.AudioVisualizerTts = audioVisualizerTts;

        // Top waveform: the original video audio. Bottom: the generated speech of every row laid
        // out at its cue start. Both share one time axis (see WireWaveform).
        audioVisualizer.Bind(AudioVisualizer.WavePeaksProperty, new Binding(nameof(vm.WavePeakData)));
        audioVisualizerTts.Bind(AudioVisualizer.WavePeaksProperty, new Binding(nameof(vm.WavePeakDataTts)));

        // End-of-video line on both tracks, red when a clip runs past it (it will be trimmed).
        audioVisualizer.Bind(AudioVisualizer.VideoEndSecondsProperty, new Binding(nameof(vm.VideoEndSeconds)));
        audioVisualizerTts.Bind(AudioVisualizer.VideoEndSecondsProperty, new Binding(nameof(vm.VideoEndSeconds)));
        audioVisualizer.Bind(AudioVisualizer.VideoEndOverrunProperty, new Binding(nameof(vm.VideoEndOverrun)));
        audioVisualizerTts.Bind(AudioVisualizer.VideoEndOverrunProperty, new Binding(nameof(vm.VideoEndOverrun)));

        WireWaveform(vm, audioVisualizer, audioVisualizerTts, isTts: false);
        WireWaveform(vm, audioVisualizerTts, audioVisualizer, isTts: true);

        var labelOriginal = MakeWaveformLabel(Se.Language.Video.TextToSpeech.WaveformOriginalAudio);
        var labelTts = MakeWaveformLabel(Se.Language.Video.TextToSpeech.WaveformGeneratedAudio);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            RowSpacing = 4,
            ColumnSpacing = 6,
            Width = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        grid.Add(labelOriginal, 0, 0);
        grid.Add(audioVisualizer, 0, 1);
        grid.Add(labelTts, 1, 0);
        grid.Add(audioVisualizerTts, 1, 1);

        return new Border
        {
            Margin = new Thickness(2),
            Height = 240,
            Child = grid,
        };
    }

    private static TextBlock MakeWaveformLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            Opacity = 0.75,
        };
    }

    // Mirror the main window's waveform theme so the review waveforms look the same as the one
    // users are already used to.
    private static AudioVisualizer CreateWaveformVisualizer()
    {
        var settings = Se.Settings.Waveform;
        return new AudioVisualizer
        {
            DrawGridLines = settings.DrawGridLines,
            WaveformColor = settings.WaveformColor.FromHexToColor(),
            WaveformBackgroundColor = settings.WaveformBackgroundColor.FromHexToColor(),
            WaveformSelectedColor = settings.WaveformSelectedColor.FromHexToColor(),
            WaveformCursorColor = settings.WaveformCursorColor.FromHexToColor(),
            WaveformShotChangeColor = settings.WaveformShotChangeColor.FromHexToColor(),
            WaveformParagraphLeftColor = settings.WaveformParagraphLeftColor.FromHexToColor(),
            WaveformParagraphRightColor = settings.WaveformParagraphRightColor.FromHexToColor(),
            WaveformFancyHighColor = settings.WaveformFancyHighColor.FromHexToColor(),
            ParagraphBackground = settings.ParagraphBackground.FromHexToColor(),
            ParagraphSelectedBackground = settings.ParagraphSelectedBackground.FromHexToColor(),
            InvertMouseWheel = settings.InvertMouseWheel,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = double.NaN,
            WaveformDrawStyle = InitWaveform.GetWaveformDrawStyle(settings.WaveformDrawStyle),
            MinGapSeconds = Se.Settings.General.MinimumBetweenLines.GetMilliseconds() / 1000.0,
            FocusOnMouseOver = settings.FocusOnMouseOver,
            IsReadOnly = Se.Settings.General.LockTimeCodes,
            WaveformHeightPercentage = settings.SpectrogramCombinedWaveformHeight,
        };
    }

    // Wires one waveform and keeps it on the same time axis as the other: scrolling, zooming or
    // seeking on either one is copied across, and both are handed the same blocks/selection.
    private static void WireWaveform(ReviewSpeechViewModel vm, AudioVisualizer av, AudioVisualizer other, bool isTts)
    {
        av.PropertyChanged += (_, e) =>
        {
            if (e.Property == AudioVisualizer.WavePeaksProperty)
            {
                // Re-center on the selected paragraph when the original peaks arrive (e.g.,
                // async-loaded); the generated track keeps the current view so a late arrival
                // does not yank the user away from where they are looking.
                if (isTts)
                {
                    vm.ReloadWaveformParagraphs();
                }
                else
                {
                    vm.RefreshWaveformPosition();
                }

                vm.SyncWaveformView(av, other);
            }
            else if (e.Property == AudioVisualizer.CurrentVideoPositionSecondsProperty)
            {
                vm.UpdatePositionText(av.CurrentVideoPositionSeconds);
            }
            else if (e.Property == AudioVisualizer.StartPositionSecondsProperty ||
                     e.Property == AudioVisualizer.ZoomFactorProperty ||
                     e.Property == AudioVisualizer.VerticalZoomFactorProperty)
            {
                // Scrolling/zooming must show the other waveform at the same place, but rebuilding
                // the block lists happens only on a user scroll (see NeedsParagraphReload) - the
                // center-ease writes StartPositionSeconds on every frame and rebuilding there was
                // what made the timeline stutter.
                vm.SyncWaveformView(av, other);

                if (e.Property != AudioVisualizer.StartPositionSecondsProperty || av.NeedsParagraphReload())
                {
                    vm.ReloadWaveformParagraphs();
                }
            }
            else if (e.Property == BoundsProperty)
            {
                vm.ReloadWaveformParagraphs();
            }

            // CurrentVideoPositionSeconds is deliberately absent: SetWaveformPlayhead writes it on
            // both controls itself, and reacting to it here only re-invalidated them every frame.
        };

        // Clicking or grabbing a block selects its row (#14000). The control only raises
        // OnPrimarySingleClicked when something listens to OnVideoPositionChanged, hence the
        // empty playhead handler. OnDragStarted fires on press so the row is selected before a
        // move/resize mutates it; OnSelectRequested covers right-click-selects.
        av.OnVideoPositionChanged += (_, _) => { };
        av.OnPrimarySingleClicked += (_, e) =>
        {
            vm.SelectFromWaveform(e.Paragraph);
            vm.OnWaveformPositionClicked(e.Seconds);
        };
        av.OnDragStarted += (_, e) => vm.SelectFromWaveform(e.Paragraph);
        av.OnSelectRequested += (_, e) => vm.SelectFromWaveform(e.Paragraph);

        // A drag moved a cue: rebuild the generated track so its clip follows the cue.
        av.OnDragEnded += (_, _) => vm.ScheduleTtsWaveformRebuild();

        // Generated-clip length bar under each block (green fits / red overrun).
        av.ParagraphAudioLengthProvider = vm.GetWaveformParagraphAudioLength;

        av.MenuFlyout = MakeWaveformMenu(vm, av);
    }

    // Context menu: the row actions from the grid plus the two timing fixes that only make sense
    // here. The target is the row under the pointer (selected on open), so the items take it as
    // CommandParameter rather than relying on SelectedLine.
    private static MenuFlyout MakeWaveformMenu(ReviewSpeechViewModel vm, AudioVisualizer av)
    {
        var menuPlay = new MenuItem { Header = Se.Language.Video.TextToSpeech.PlayLine, Command = vm.PlayRowCommand };
        var menuRegenerate = new MenuItem { Header = Se.Language.Video.TextToSpeech.RegenerateAudio, Command = vm.RegenerateAudioCommand };
        var menuHistory = new MenuItem { Header = Se.Language.General.ShowHistory, Command = vm.ShowHistoryCommand };
        var menuFit = new MenuItem { Header = Se.Language.Video.TextToSpeech.FitDurationToGeneratedAudio, Command = vm.FitDurationToAudioCommand };
        var menuReset = new MenuItem { Header = Se.Language.Video.TextToSpeech.ResetTiming, Command = vm.ResetTimingCommand };
        var menuSplit = new MenuItem { Header = Se.Language.General.SplitLineAtVideoAndTextBoxPosition, Command = vm.SplitLineCommand };
        var flyout = new MenuFlyout();
        // Regenerate first: it is the most used action on this menu.
        flyout.Items.Add(menuRegenerate);
        flyout.Items.Add(menuPlay);
        flyout.Items.Add(menuHistory);
        flyout.Items.Add(new Separator());
        flyout.Items.Add(menuFit);
        flyout.Items.Add(menuReset);
        flyout.Items.Add(new Separator());
        flyout.Items.Add(menuSplit);
        av.FlyoutMenuOpening += (_, e) =>
        {
            var row = vm.SelectRowAtWaveformPosition(e.PositionInSeconds);
            foreach (var item in new[] { menuPlay, menuRegenerate, menuHistory, menuFit, menuReset, menuSplit })
            {
                item.CommandParameter = row;
                item.IsEnabled = row != null;
            }

            if (row != null)
            {
                menuPlay.IsEnabled = row.IsPlayingEnabled && !row.IsPlaying;
                menuRegenerate.IsEnabled = vm.IsRegenerateEnabled && row.IsPlayingEnabled;
                menuHistory.IsEnabled = row.HasHistory;
                menuFit.IsEnabled = vm.GetGeneratedAudioLengthSeconds(row) > 0 && !av.IsReadOnly;
                menuReset.IsEnabled = !av.IsReadOnly;
                menuSplit.IsEnabled = vm.IsRegenerateEnabled;
            }
        };

        return flyout;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && FocusManager?.GetFocusedElement() is AudioVisualizer && _vm.OnWaveformKeyDown(e))
        {
            e.Handled = true;
            return;
        }

        _vm.OnKeyDown(e);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        _vm.OnClosing(e);
    }
}
