using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Controls.VideoPlayer;
using Nikse.SubtitleEdit.Controls.AudioVisualizerControl;
using Nikse.SubtitleEdit.Features.Main.Layout;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.ElevenLabsSettings;
using Nikse.SubtitleEdit.Features.Video.TextToSpeech.Engines;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.ValueConverters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;

public class ReviewSpeechWindow : Window
{
    private readonly ReviewSpeechViewModel _vm;

    // One entry per palette button: tracks whether its tag is currently "on" so the button can be
    // tinted. Kept here (not in the VM) because the buttons are view-only.
    private readonly List<TagChip> _tagChips = new();

    // The three resizable columns of the three-column layout, kept so their splitter positions can
    // be saved on close and restored on open.
    private ColumnDefinition? _colTag;
    private ColumnDefinition? _colGrid;
    private ColumnDefinition? _colControls;

    private sealed class TagChip
    {
        public string Tag { get; }
        public Button? Button { get; set; }

        public TagChip(string tag)
        {
            Tag = tag.Trim().TrimStart('[').TrimEnd(']').Trim();
        }
    }

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

        var controls = MakeControls(vm, includeTags: !Se.Settings.Video.TextToSpeech.ThreeColumnReview);
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

        var threeColumn = Fork.TtsLayout && Se.Settings.Video.TextToSpeech.ThreeColumnReview;

        // An active tag chip is tinted with its CATEGORY color (set inline in RefreshTagChips, so
        // each category has its own); this style only carries the weight, since per-chip colors
        // must not be overridden by a shared style.
        Styles.Add(new Style(x => x.OfType<Button>().Class("tag-chip").Class("active"))
        {
            Setters =
            {
                new Setter(Button.FontWeightProperty, FontWeight.SemiBold),
            },
        });

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
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
            },
            Margin = UiUtil.MakeWindowMargin(),
            ColumnSpacing = 10,
            RowSpacing = 10,
            Width = double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        if (threeColumn)
        {
            // Three equal, resizable columns: tags | line grid | engine/voice controls. The two
            // splitters redistribute the star widths, and every column keeps a MinWidth so nothing
            // collapses or clips out of the window.
            grid.ColumnDefinitions.Clear();
            var saved = Se.Settings.Video.TextToSpeech;
            var hasSavedWidths = saved.ThreeColumnTagWidth > 0 && saved.ThreeColumnGridWidth > 0 && saved.ThreeColumnControlsWidth > 0;
            _colTag = new ColumnDefinition { Width = hasSavedWidths ? new GridLength(saved.ThreeColumnTagWidth, GridUnitType.Star) : new GridLength(22, GridUnitType.Star), MinWidth = 150 };
            _colGrid = new ColumnDefinition { Width = hasSavedWidths ? new GridLength(saved.ThreeColumnGridWidth, GridUnitType.Star) : new GridLength(40, GridUnitType.Star), MinWidth = 200 };
            _colControls = new ColumnDefinition { Width = hasSavedWidths ? new GridLength(saved.ThreeColumnControlsWidth, GridUnitType.Star) : new GridLength(38, GridUnitType.Star), MinWidth = 380 };
            grid.ColumnDefinitions.Add(_colTag);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(_colGrid);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(_colControls);

            grid.ColumnSpacing = 0;

            var splitterLeft = new GridSplitter { Width = 6, Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
            var splitterRight = new GridSplitter { Width = 6, Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
            splitterLeft.DragCompleted += (_, _) => SaveColumnWidths();
            splitterRight.DragCompleted += (_, _) => SaveColumnWidths();

            var tagColumn = MakeTagColumn(vm);
            vm.TagPalette = tagColumn;
            grid.Add(tagColumn, 0, 0);
            vm.ActiveTags.CollectionChanged += (_, _) => RefreshTagChips();
            RefreshTagChips();
            grid.Add(splitterLeft, 0, 1);
            grid.Add(lineGridView, 0, 2);
            grid.Add(splitterRight, 0, 3);
            grid.Add(controls, 0, 4);

            grid.Add(waveform, 1, 0, 1, 5);
            grid.Add(panelButtons, 2, 4);

            // Options row, left of the button bar (row 2 spans the first four columns).
            var optionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { MakePositionLabel(vm) },
            };
            grid.Add(optionsPanel, 2, 0, 1, 4);
        }
        else
        {
            grid.Add(controls, 0, 0);
            grid.Add(lineGridView, 0, 1);
            grid.Add(waveform, 1, 0, 1, 3);
            grid.Add(panelButtons, 2, 0, 1, 3);
            grid.Add(MakePositionLabel(vm), 2, 0, 1, 2);
        }

        Content = grid;

        if (!Fork.TtsLayout)
        {
            // Upstream keeps "Auto-continue playing" in the bottom bar, below the waveform, instead
            // of in the controls column.
            grid.Add(new CheckBox
            {
                Content = Se.Language.Video.TextToSpeech.AutoContinuePlaying,
                [!CheckBox.IsCheckedProperty] = new Binding(nameof(vm.AutoContinue)) { Mode = BindingMode.TwoWay },
            }, 2, 0);
        }

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
        var lineGrid = TableViewExtras.MakeTableView(multiSelect: true);
        lineGrid.Margin = new Thickness(0, 10, 0, 0);
        lineGrid.Width = double.NaN;
        lineGrid.Height = double.NaN;
        lineGrid[!TableView.ItemsSourceProperty] = new Binding(nameof(vm.Lines));
        lineGrid[!TableView.SelectedItemProperty] = new Binding(nameof(vm.SelectedLine)) { Mode = BindingMode.TwoWay };

        // Multi-select feeds "Merge lines": two adjacent rows (shift+click) enable it.
        lineGrid.SelectionChanged += (_, _) =>
            vm.SetSelectedRows(lineGrid.SelectedItems?.OfType<ReviewRow>() ?? Enumerable.Empty<ReviewRow>());

        var gridFlyout = new MenuFlyout { Placement = PlacementMode.Pointer };
        var menuSplitLine = new MenuItem
        {
            Header = Se.Language.General.SplitLineAtVideoAndTextBoxPosition,
            Command = vm.SplitLineCommand,
        };
        if (Fork.ReviewSplitMerge)
        {
            var menuMergeLines = new MenuItem
            {
                Header = Se.Language.General.MergeLines,
                Command = vm.MergeLinesCommand,
            };

            gridFlyout.Items.Add(menuSplitLine);
            gridFlyout.Items.Add(menuMergeLines);

            // Only the action that applies is offered: Split wants a single row, Merge exactly the
            // two adjacent ones. Showing both - one of them dead - is what made the menu jump
            // between one and two rows and pick up a scroll bar.
            gridFlyout.Opening += (_, _) =>
            {
                menuSplitLine.CommandParameter = vm.SelectedLine;
                menuMergeLines.IsVisible = vm.CanMergeLines;
                menuSplitLine.IsVisible = !vm.CanMergeLines;
            };
        }

        lineGrid.ContextFlyout = gridFlyout;

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
            Header = Se.Language.General.NumberSymbol,
            Binding = new Binding(nameof(ReviewRow.Number)),
            Width = new GridLength(50),
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
            // The cell renders the text itself so each "[tag]" carries its category's colour (the
            // edit box does the same through its source highlighter). NameBinding keeps the row's
            // text readable for screen readers, which cannot read a CellTemplate back.
            NameBinding = new Binding(nameof(ReviewRow.Text)),
            Width = new GridLength(1, GridUnitType.Star),
            CellTheme = UiUtil.TableViewCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
            CellTemplate = new FuncDataTemplate<ReviewRow>((_, _) => MakeTaggedTextCell()),
        });
        // Voice and the row actions sit after the growing Text column so they stay pinned to the
        // right edge instead of being pushed off it.
        var voiceColumn = new SeTableViewColumn
        {
            Header = Se.Language.General.Voice,
            Binding = new Binding(nameof(ReviewRow.Voice)),
            Width = new GridLength(150),
            CellTheme = UiUtil.TableViewCellTheme,
            HeaderTheme = UiUtil.TableViewColumnHeaderTheme,
        };
        lineGrid.Columns.Add(voiceColumn);
        var iconsColumn = new SeTableViewColumn
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
        };
        lineGrid.Columns.Add(iconsColumn);
        // The fork's grid look: alternating rows, vertically centred cells and the fork column order
        // (Voice and the row actions pinned right). Off = upstream: no tint, no centring, and the
        // row actions right after Enabled with Voice next to Number.
        if (!Fork.ReviewGridLook)
        {
            lineGrid.Columns.Remove(iconsColumn);
            lineGrid.Columns.Insert(1, iconsColumn);
            lineGrid.Columns.Remove(voiceColumn);
            lineGrid.Columns.Insert(3, voiceColumn);
        }

        // Alternating row tint and vertically centred cells, so the review grid reads like the main
        // grid. The tint follows the same Appearance setting the main grid uses; the centring is
        // scoped to this grid, because its rows are tall (the action buttons) and that left the
        // plain text and number cells hugging the top of the row.
        if (Fork.ReviewGridLook && Se.Settings.Appearance.GridAlternatingRows)
        {
            var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            var hex = isDark
                ? Se.Settings.Appearance.GridAlternatingRowColorDark
                : Se.Settings.Appearance.GridAlternatingRowColor;
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    TableViewExtras.ApplyAlternatingRows(lineGrid, new SolidColorBrush(hex.FromHexToColor()));
                }
            }
            catch
            {
                // A malformed colour in the settings must not take the grid down - just no tint.
            }
        }

        if (Fork.ReviewGridLook)
        {
            lineGrid.Styles.Add(new Style(x => x.OfType<TableViewCell>())
            {
                Setters =
                {
                    new Setter(TableViewCell.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                },
            });
        }

        lineGrid.DoubleTapped += (_, e) =>
        {
            // Resolve the row from the pointer, not from SelectedLine: on the first double-click
            // after an import the selection has not reached the view model yet, so SelectedLine was
            // still the first row - and that row played instead of the clicked one.
            var index = TableViewExtras.GetRowIndexFromPoint(lineGrid, e.GetPosition(lineGrid));
            vm.LineGridDoubleClicked(index >= 0 && index < vm.Lines.Count ? vm.Lines[index] : vm.SelectedLine);
        };
        vm.LineGrid = lineGrid;

        var textBox = new SyntaxHighlightingTextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
            SourceHighlighter = Fork.ReviewTagPalette ? new TtsTagSyntaxHighlighter() : null,
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
        // Refresh the palette highlights as the user hand-edits the text (typing a tag in, deleting
        // one) so a tag typed manually lights up just like one inserted from the palette.
        textBox.TextChanged += (_, _) => vm.RefreshActiveTags();

        // A plain click (no drag-select) ON an "[...]" tag removes that tag - the whole group when it
        // is the only tag, otherwise just the tag under the click.
        textBox.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && textBox.SelectionStart == textBox.SelectionEnd)
            {
                vm.RemoveTagAtCaret();
            }
        };

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

    private static Border MakeControls(ReviewSpeechViewModel vm, bool includeTags)
    {
        // Narrow when the palette sits in its own left column (three-column layout), wide enough to
        // keep the label + combo box on one row otherwise.
        var labelMinWidth = includeTags ? 110 : 92;
        var controlMinWidth = includeTags ? 220 : 150;

        // In the original layout the combos are a fixed width next to a fixed label. In the
        // three-column layout the controls live in a narrow star column, so let them stretch to
        // fill the available width instead of clipping out of the panel.
        T ComboWidth<T>(T control) where T : Layoutable
        {
            if (includeTags)
            {
                control.Width = controlMinWidth;
            }
            else
            {
                control.MinWidth = controlMinWidth;
                control.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            return control;
        }

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
                ComboWidth(UiUtil.MakeComboBox(vm.Voices, vm, nameof(vm.SelectedVoice))),
            }
        };

        var comboBoxModels = ComboWidth(UiUtil.MakeComboBox(vm.Models, vm, nameof(vm.SelectedModel)));
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
                    MinWidth = labelMinWidth,
                    [!ContentProperty] = new Binding(nameof(vm.RegionLabel)) { Mode = BindingMode.OneWay },
                },
                ComboWidth(UiUtil.MakeComboBox(vm.Regions, vm, nameof(vm.SelectedRegion))),
            },
            [!StackPanel.IsVisibleProperty] = new Binding(nameof(vm.SelectedEngine) + "." + nameof(ITtsEngine.HasRegion)) { Mode = BindingMode.OneWay },
        };

        var comboBoxLanguages = ComboWidth(UiUtil.MakeComboBox(vm.Languages, vm, nameof(vm.SelectedLanguage)));
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
        var panelInstruction = MakeInstructionPanel(vm, labelMinWidth, includeTags);

        // ElevenLabs credit state at the top of the controls column: the balance captured before
        // Generate ("Fixed"), the live balance ("Now") and what this session spent ("Spent").
        var panelCredits = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 1,
            Margin = new Thickness(0, 0, 0, 10),
            [!Visual.IsVisibleProperty] = new Binding(nameof(ReviewSpeechViewModel.IsCreditsPanelVisible)),
            Children =
            {
                new TextBlock { Text = "ElevenLabs credit usage", FontSize = 13, FontWeight = FontWeight.SemiBold, Opacity = 0.8 },
                new TextBlock { FontSize = 15, FontWeight = FontWeight.Bold, [!TextBlock.TextProperty] = new Binding(nameof(ReviewSpeechViewModel.ElevenLabsCreditsFixedText)) },
                new TextBlock { FontSize = 15, FontWeight = FontWeight.Bold, [!TextBlock.TextProperty] = new Binding(nameof(ReviewSpeechViewModel.ElevenLabsCreditsNowText)) },
                new TextBlock { FontSize = 15, FontWeight = FontWeight.Bold, [!TextBlock.TextProperty] = new Binding(nameof(ReviewSpeechViewModel.ElevenLabsCreditsSpentText)) },
            },
        };

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) }, // credits
                new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
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

        grid.Add(panelCredits, 0, 0);
        grid.Add(panelEngine, 1, 0);
        // Model comes before Voice — same ordering as the main TTS window and Cast dialog so the
        // user picks a model first (which sometimes filters the voice list) and the dropdowns
        // line up across windows.
        grid.Add(panelModel, 2, 0);
        grid.Add(panelVoice, 3, 0);
        grid.Add(panelRegion, 4, 0);
        grid.Add(panelLanguage, 5, 0);
        if (includeTags)
        {
            // Original layout: the ElevenLabs sliders with the tag palette docked right below them.
            var tagPanel = MakeTagPanel(vm);
            tagPanel[!Visual.IsVisibleProperty] = new Binding(nameof(ReviewSpeechViewModel.ShowReviewTagPalette));
            grid.Add(new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children = { elevenLabsControls, tagPanel },
            }, 6, 0);
        }
        else
        {
            // Three-column layout: the tag palette lives in its own left column instead.
            grid.Add(elevenLabsControls, 6, 0);
        }

        // Playback options, under the ElevenLabs sliders behind a divider: the two that used to sit
        // in the bottom bar.
        var checkBoxAutoContinue = new CheckBox
        {
            Content = Se.Language.Video.TextToSpeech.AutoContinuePlaying,
            [!CheckBox.IsCheckedProperty] = new Binding(nameof(vm.AutoContinue)) { Mode = BindingMode.TwoWay },
        };
        var checkBoxSkipGeneralAccent = new CheckBox
        {
            Content = "Skip general accent",
            [!CheckBox.IsCheckedProperty] = new Binding(nameof(vm.SkipGeneralAccent)) { Mode = BindingMode.TwoWay },
        };
        ToolTip.SetTip(checkBoxSkipGeneralAccent, "Regenerate without the configured general accent for this line");

        // One checkbox bound to a review fork switch (see ReviewSpeechViewModel) - live, so the
        // switch takes effect without reopening the window.
        static CheckBox MakeReviewSwitch(string text, string property)
        {
            return new CheckBox
            {
                Content = text,
                [!CheckBox.IsCheckedProperty] = new Binding(property) { Mode = BindingMode.TwoWay },
            };
        }

        var panelPlaybackOptions = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2,
            Margin = new Thickness(0, 10, 0, 10),
            Children =
            {
                new Separator(),
                checkBoxAutoContinue,
                checkBoxSkipGeneralAccent,
                new Separator(),
                new Expander
                {
                    Header = "Fork features",
                    IsExpanded = true,
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Spacing = 2,
                        Children =
                        {
                            MakeReviewSwitch("Anchor marker above the waveform", nameof(vm.ShowReviewAnchorMarker)),
                            MakeReviewSwitch("Credit panel", nameof(vm.ShowReviewCredits)),
                            MakeReviewSwitch("Tag palette + coloured tags", nameof(vm.ShowReviewTagPalette)),
                            MakeReviewSwitch("Split / Merge in the grid menu", nameof(vm.ShowReviewSplitMerge)),
                            MakeReviewSwitch("Tint the playing track's play-head", nameof(vm.ShowReviewPlayheadTint)),
                            MakeReviewSwitch("Play from the block's beginning", nameof(vm.ShowReviewPlayFromBlockStart)),
                            MakeReviewSwitch("Regenerate previews only the clip", nameof(vm.ShowReviewRegeneratePreviewOnly)),
                        },
                    },
                },
            },
        };

        if (!Fork.Enabled)
        {
            // Fork: "Skip general accent" belongs to our ElevenLabs v4 accent support - upstream has
            // no such option, so it is not shown at all.
            panelPlaybackOptions.Children.Remove(checkBoxSkipGeneralAccent);
        }

        if (Fork.TtsLayout)
        {
            grid.Add(panelPlaybackOptions, 7, 0);
        }
        grid.Add(panelInstruction, 8, 0);
        // 9 is filler.

        return UiUtil.MakeBorderForControl(grid);
    }

    // Left column of the three-column review layout: a click-to-insert tag palette matching the
    // original layout (category title, then its tags to the right on the same row) plus a
    // Remove-all button. The block sits at the bottom, closest to the waveform.
    private Control MakeTagColumn(ReviewSpeechViewModel vm)
    {
        var column = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        foreach (var category in TtsTagPalette.Categories)
        {
            var tags = category.Tags;
            var headerLabel = new TextBlock
            {
                Text = category.Header,
                Width = 62,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(category.Color),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var wrap = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
            };

            foreach (var tag in tags)
            {
                var chip = new TagChip(tag);
                _tagChips.Add(chip);

                // Plain button look when off (same as "Remove all tags"); RefreshTagChips tints an
                // active chip with its category color.
                var button = new Button
                {
                    Content = tag,
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(2, 2, 2, 2),
                    FontSize = 12,
                };
                button.Classes.Add("tag-chip");
                chip.Button = button;
                button.Click += (_, _) => vm.InsertTagAtCaret(tag);
                ToolTip.SetTip(button, $"Toggle {tag} on the selected line");
                wrap.Children.Add(button);
            }

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(62) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
                ColumnSpacing = 4,
            };
            row.Add(headerLabel, 0, 0);
            row.Add(wrap, 0, 1);
            column.Children.Add(row);
        }

        var removeAllButton = new Button
        {
            Content = "Remove all tags",
            Padding = new Thickness(8, 1, 8, 1),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(66, 4, 0, 0),
        };
        removeAllButton.Click += (_, _) => vm.RemoveAllTags();
        ToolTip.SetTip(removeAllButton, "Remove the whole [..] tag group from the selected line");
        column.Children.Add(removeAllButton);

        // Bottom-align the palette without ScrollViewer.VerticalContentAlignment (which triggers a
        // layout/measure loop here): host it in a grid that is at least viewport-tall and align the
        // column to its bottom. Content taller than the viewport still scrolls.
        var host = new Grid();
        column.VerticalAlignment = VerticalAlignment.Bottom;
        host.Children.Add(column);

        // Live ElevenLabs credit balance now lives at the top of the controls column (MakeControls).

        var scroll = new ScrollViewer
        {
            Content = host,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 0, 4, 0),
        };
        host.Bind(Grid.MinHeightProperty, new Binding(nameof(ScrollViewer.Viewport))
        {
            Source = scroll,
            Converter = new Avalonia.Data.Converters.FuncValueConverter<Avalonia.Size, double>(s => s.Height),
        });
        // Only the tags are hidden with the tag feature - the video above them stays.
        scroll[!Visual.IsVisibleProperty] = new Binding(nameof(ReviewSpeechViewModel.ShowReviewTagPalette));

        var leftColumn = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // video (top third)
                new RowDefinition { Height = new GridLength(2, GridUnitType.Star) }, // tags (bottom two thirds)
            },
        };

        // Video surface above the palette: shows the loaded video and follows the playhead. Its
        // sound follows the "Original"/"Generated" waveform labels (muted unless Original). With the
        // fork switch off there is no video at all and the review plays clips the upstream way.
        if (Fork.ReviewVideoPlayer)
        {
            var videoPlayer = InitVideoPlayer.MakeVideoPlayer();
            videoPlayer.Margin = new Thickness(2);
            videoPlayer.FullScreenIsVisible = false;
            videoPlayer.ClickToTogglePlay = true;
            videoPlayer.HorizontalAlignment = HorizontalAlignment.Stretch;
            videoPlayer.VerticalAlignment = VerticalAlignment.Stretch;
            videoPlayer.SurfacePointerPressed += (_, _) => videoPlayer.TogglePlayPause();
            videoPlayer.HideVideoControls(); // transport/seek are not needed here - the labels drive playback
            vm.VideoPlayerControl = videoPlayer;
            Opened += (_, _) => _ = vm.OpenReviewVideoAsync();
            Grid.SetRow(videoPlayer, 0);
            leftColumn.Children.Add(videoPlayer);
        }
        else
        {
            // No video: the tags get the whole column.
            leftColumn.RowDefinitions[0].Height = new GridLength(0);
            leftColumn.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        }

        Grid.SetRow(scroll, 1);
        leftColumn.Children.Add(scroll);
        return leftColumn;
    }

    // Recomputes every chip's highlight from the VM's active tag set.
    private void RefreshTagChips()
    {
        foreach (var chip in _tagChips)
        {
            var active = _vm.ActiveTags.Any(t => string.Equals(t, chip.Tag, StringComparison.OrdinalIgnoreCase));
            chip.Button?.Classes.Set("active", active);

            // Tint the active chip with its CATEGORY color (pastel background, dark same-hue text);
            // inactive chips fall back to the normal button look.
            if (chip.Button is { } button)
            {
                if (active)
                {
                    var color = TtsTagPalette.ColorForTag(chip.Tag);
                    button.Background = new SolidColorBrush(TtsTagPalette.Pastel(color));
                    button.Foreground = new SolidColorBrush(TtsTagPalette.DarkText(color));
                }
                else
                {
                    button.ClearValue(Button.BackgroundProperty);
                    button.ClearValue(Button.ForegroundProperty);
                }
            }
        }
    }

    // A click-to-insert palette of the audio tags that make sense for a recipe voiceover. Each
    // button drops its tag at the caret of the selected line's text box.
    private static Control MakeTagPanel(ReviewSpeechViewModel vm)
    {
        var rows = TtsTagPalette.Categories;

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Thickness(0, 6, 0, 0),
        };

        // Remove-all: clears the leading "[...]" tag group from the selected line.
        var removeAllButton = new Button
        {
            Content = "Remove all tags",
            Padding = new Thickness(8, 1, 8, 1),
            FontSize = 11,
            Margin = new Thickness(74, 0, 0, 2),
        };
        removeAllButton.Click += (_, _) => vm.RemoveAllTags();
        ToolTip.SetTip(removeAllButton, "Remove the whole [..] tag group from the selected line");
        panel.Children.Add(removeAllButton);

        foreach (var category in rows)
        {
            var tags = category.Tags;
            var headerLabel = new TextBlock
            {
                Text = category.Header,
                Width = 70,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(category.Color),
            };

            // Split the category into two rows so it fits without a horizontal scrollbar.
            var half = (tags.Length + 1) / 2;
            var firstRow = tags.Take(half).ToArray();
            var secondRow = tags.Skip(half).ToArray();

            var row1 = MakeTagRow(vm, firstRow, headerLabel);
            panel.Children.Add(row1);

            if (secondRow.Length > 0)
            {
                panel.Children.Add(MakeTagRow(vm, secondRow, null, headerLabel.Width + 4));
            }
        }

        return panel;
    }

    private static StackPanel MakeTagRow(ReviewSpeechViewModel vm, string[] tags, TextBlock? label, double leftPad = 0)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
        };

        if (label != null)
        {
            row.Children.Add(label);
        }
        else if (leftPad > 0)
        {
            row.Children.Add(new TextBlock { Width = leftPad });
        }

        foreach (var tag in tags)
        {
            var button = new Button
            {
                Content = tag,
                Padding = new Thickness(6, 1, 6, 1),
                FontSize = 11,
            };
            button.Click += (_, _) => vm.InsertTagAtCaret(tag);
            ToolTip.SetTip(button, $"Insert {tag} at the start of the line");
            row.Children.Add(button);
        }

        return row;
    }

    // Voice-design controls shared with the main TTS window: free-text instruction (Qwen3
    // VoiceDesign model) and OmniVoice keyword picker. Visibility flags on the VM mirror those
    // in TextToSpeechViewModel — see ReviewSpeechViewModel.UpdateInstructionVisibility.
    private static StackPanel MakeInstructionPanel(ReviewSpeechViewModel vm, int labelMinWidth, bool includeTags)
    {
        var textBoxInstruction = new TextBox
        {
            Width = includeTags ? 260 : double.NaN,
            MinWidth = includeTags ? 0 : 120,
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
                MakeInstructionKeywordPicker(vm, includeTags),
            },
            [!StackPanel.IsVisibleProperty] = new Binding(nameof(vm.HasInstruction)) { Mode = BindingMode.OneWay },
        };
    }

    // OmniVoice keyword picker — gender/age/pitch/accent combos plus a whisper checkbox and a
    // "doesn't apply to cloned voices" hint. Mirrors the picker in the main TTS window so the
    // user sees the same control set regardless of which window they regenerate from.
    private static Control MakeInstructionKeywordPicker(ReviewSpeechViewModel vm, bool includeTags)
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

        AddPickerRow(grid, 0, Se.Language.Video.TextToSpeech.VoiceGender, vm, vm.OmniVoiceGenders, nameof(vm.SelectedOmniVoiceGender), includeTags);
        AddPickerRow(grid, 1, Se.Language.Video.TextToSpeech.VoiceAge, vm, vm.OmniVoiceAges, nameof(vm.SelectedOmniVoiceAge), includeTags);
        AddPickerRow(grid, 2, Se.Language.Video.TextToSpeech.VoicePitch, vm, vm.OmniVoicePitches, nameof(vm.SelectedOmniVoicePitch), includeTags);
        AddPickerRow(grid, 3, Se.Language.Video.TextToSpeech.VoiceAccent, vm, vm.OmniVoiceAccents, nameof(vm.SelectedOmniVoiceAccent), includeTags);

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
        System.Collections.ObjectModel.ObservableCollection<string> items, string selectedPropertyPath, bool includeTags)
    {
        grid.Add(new Label { Content = label, MinWidth = 60, VerticalAlignment = VerticalAlignment.Center }, row, 0);
        var combo = UiUtil.MakeComboBox(items, vm, selectedPropertyPath);
        if (includeTags)
        {
            combo.Width = 200;
        }
        else
        {
            combo.MinWidth = 90;
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        grid.Add(combo, row, 1);
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

        // eleven_v3 / eleven_v4 accept only Stability and Similarity - hide SpeakerBoost, Speed
        // and Style for them so no slider is shown that the API would silently ignore.
        foreach (var c in new Control[] { labelSpeakerBoost, sliderSpeakerBoost, labelSpeakerBoostValue, buttonSpeakerBoost, labelSpeed, sliderSpeed, labelSpeedValue, buttonSpeed, labelStyleExaggeration, sliderStyleExaggeration, labelStyleExaggerationValue, buttonStyleExaggeration })
        {
            c.Bind(Visual.IsVisibleProperty, new Binding(nameof(vm.IsElevenLabsV3OrV4Selected))
            {
                Converter = InverseBooleanConverter.Instance,
                Mode = BindingMode.OneWay,
            });
        }

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

    // The line's text with every "[tag]" in its category's colour (see TtsTagPalette), matching the
    // edit box's highlighter. Rebuilt when the row's text changes and when a recycled cell is handed
    // another row, so the grid always shows the current text.
    private static Control MakeTaggedTextCell()
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        ReviewRow? bound = null;

        void Rebuild()
        {
            block.Inlines!.Clear();
            if (bound?.Text is not { } text)
            {
                return;
            }

            // With the tag feature off the cell is plain text, exactly like upstream.
            var segments = Fork.ReviewTagPalette
                ? TtsTagPalette.Segments(text)
                : new List<(string Text, Color? Color)> { (text, null) };
            foreach (var (segment, color) in segments)
            {
                var run = new Run(segment);
                if (color.HasValue)
                {
                    // Only the tags get an explicit brush: setting Foreground to null on the plain
                    // runs would override the TextBlock's inherited colour and hide the text.
                    run.Foreground = new SolidColorBrush(color.Value);
                    run.FontWeight = FontWeight.SemiBold;
                }

                block.Inlines.Add(run);
            }
        }

        void RowChanged(object? _, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReviewRow.Text))
            {
                Rebuild();
            }
        }

        block.PropertyChanged += (_, e) =>
        {
            if (e.Property != StyledElement.DataContextProperty)
            {
                return;
            }

            if (bound != null)
            {
                bound.PropertyChanged -= RowChanged;
            }

            bound = block.DataContext as ReviewRow;
            if (bound != null)
            {
                bound.PropertyChanged += RowChanged;
            }

            Rebuild();
        };

        return block;
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
        vm.AudioVisualizer = audioVisualizer;

        // The fork's second track: the generated speech of every row laid out at its cue start.
        // Off = upstream's single waveform (the original video audio only).
        var audioVisualizerTts = Fork.ReviewTwoWaveforms ? CreateWaveformVisualizer() : null;
        vm.AudioVisualizerTts = audioVisualizerTts;

        audioVisualizer.Bind(AudioVisualizer.WavePeaksProperty, new Binding(nameof(vm.WavePeakData)));

        // End-of-video line, red when a clip runs past it (it will be trimmed).
        audioVisualizer.Bind(AudioVisualizer.VideoEndSecondsProperty, new Binding(nameof(vm.VideoEndSeconds)));
        audioVisualizer.Bind(AudioVisualizer.VideoEndOverrunProperty, new Binding(nameof(vm.VideoEndOverrun)));

        if (audioVisualizerTts != null)
        {
            // Both tracks share one time axis (see WireWaveform).
            audioVisualizerTts.Bind(AudioVisualizer.WavePeaksProperty, new Binding(nameof(vm.WavePeakDataTts)));
            audioVisualizerTts.Bind(AudioVisualizer.VideoEndSecondsProperty, new Binding(nameof(vm.VideoEndSeconds)));
            audioVisualizerTts.Bind(AudioVisualizer.VideoEndOverrunProperty, new Binding(nameof(vm.VideoEndOverrun)));
            WireWaveform(vm, audioVisualizerTts, audioVisualizer, isTts: true);
        }

        // With a single track the sync target is the track itself, so the shared helpers stay
        // null-free.
        WireWaveform(vm, audioVisualizer, audioVisualizerTts ?? audioVisualizer, isTts: false);

        var labelOriginal = MakeWaveformLabel(Se.Language.Video.TextToSpeech.WaveformOriginalAudio, vm.UseOriginalAudio);
        var labelTts = MakeWaveformLabel(Se.Language.Video.TextToSpeech.WaveformGeneratedAudio, vm.UseTtsAudio);
        if (!Fork.ReviewVideoPlayer)
        {
            // Without the video there is no original audio to switch to, so the labels would do nothing.
            labelOriginal.IsVisible = false;
            labelTts.IsVisible = false;
        }

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
        if (audioVisualizerTts != null)
        {
            grid.Add(labelTts, 1, 0);
            grid.Add(audioVisualizerTts, 1, 1);
        }
        else
        {
            // Single (upstream) waveform: one row, the generated label gone.
            grid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            grid.RowDefinitions[1].Height = new GridLength(0);
            labelTts.IsVisible = false;
        }

        // The red marker the play-head is anchored to. It has no layout row of its own: it sits in
        // the original track's cell with a negative top margin, so its body overlaps the gap above
        // the waveform and its tip touches the waveform's top edge. Dragged left/right it moves the
        // anchor - the spot the view scrolls the play-head to (see SetWaveformPlayhead).
        const double markerWidth = 12;
        const double markerHeight = 9;
        var anchorMarker = new Path
        {
            Data = Geometry.Parse($"M 0,0 L {markerWidth},0 L {markerWidth / 2},{markerHeight} Z"),
            Fill = Brushes.Red,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
        };
        grid.Add(anchorMarker, 0, 1);
        anchorMarker[!Visual.IsVisibleProperty] = new Binding(nameof(ReviewSpeechViewModel.ShowReviewAnchorMarker));

        void PlaceMarker() => anchorMarker.Margin = new Thickness(
            audioVisualizer.Bounds.Width * vm.AnchorFraction - markerWidth / 2, -markerHeight, 0, 0);

        audioVisualizer.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.BoundsProperty)
            {
                PlaceMarker();
            }
        };

        var draggingMarker = false;
        anchorMarker.PointerPressed += (_, e) =>
        {
            draggingMarker = true;
            e.Pointer.Capture(anchorMarker);
            e.Handled = true;
        };
        anchorMarker.PointerMoved += (_, e) =>
        {
            if (!draggingMarker || audioVisualizer.Bounds.Width <= 0)
            {
                return;
            }

            vm.SetAnchorFraction(e.GetPosition(audioVisualizer).X / audioVisualizer.Bounds.Width);
            PlaceMarker();
            e.Handled = true;
        };
        anchorMarker.PointerReleased += (_, e) =>
        {
            draggingMarker = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        };

        PlaceMarker();

        return new Border
        {
            Margin = new Thickness(2),
            Height = 240,
            Child = grid,
        };
    }

    private static TextBlock MakeWaveformLabel(string text, Action? onClick = null)
    {
        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            Opacity = 0.75,
        };

        if (onClick != null)
        {
            label.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(label, "Click to hear this track's sound");
            label.PointerPressed += (_, _) => onClick();
        }

        return label;
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
            WaveformGridColor = settings.WaveformGridColor.FromHexToColor(),
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
            // TTS review blocks must not overlap: a dragged block stops at its neighbour's edge.
            PreventOverlap = true,
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
            // The playhead goes exactly where the user clicked, even inside a block, so a split can
            // be placed mid-block. The click still selects the block for editing.
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
        SaveColumnWidths();
        _vm.OnClosing(e);
    }

    // Persists the three-column splitter positions as relative weights. Using the ratio (not a
    // fixed pixel width) keeps the layout responsive when the window itself is resized.
    private void SaveColumnWidths()
    {
        if (_colTag == null || _colGrid == null || _colControls == null)
        {
            return;
        }

        var total = _colTag.ActualWidth + _colGrid.ActualWidth + _colControls.ActualWidth;
        if (total <= 0)
        {
            return;
        }

        var settings = Se.Settings.Video.TextToSpeech;
        settings.ThreeColumnTagWidth = _colTag.ActualWidth / total * 100.0;
        settings.ThreeColumnGridWidth = _colGrid.ActualWidth / total * 100.0;
        settings.ThreeColumnControlsWidth = _colControls.ActualWidth / total * 100.0;
    }
}
