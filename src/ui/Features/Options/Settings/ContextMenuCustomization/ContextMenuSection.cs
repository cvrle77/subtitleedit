using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nikse.SubtitleEdit.Features.Options.Settings.ContextMenuCustomization;

/// <summary>
/// One item row in the "Customize right-click menus" settings: the item's header text and a tick
/// for whether it is shown in the menu (unticked = hidden).
/// </summary>
public partial class ContextMenuItemDisplay : ObservableObject
{
    public string Name { get; }

    [ObservableProperty] private bool _isVisible;

    public ContextMenuItemDisplay(string name, bool isVisible)
    {
        Name = name;
        _isVisible = isVisible;
    }
}

/// <summary>
/// One right-click menu's section in the settings: its key ("textbox"/"grid"/"waveform"), its item
/// rows, the select-all helpers, and the save back into <see cref="SeGeneral.HiddenContextMenuItems"/>.
/// </summary>
public partial class ContextMenuSection : ObservableObject
{
    public string Key { get; }

    public ObservableCollection<ContextMenuItemDisplay> Items { get; } = new();

    /// <summary>
    /// Short text for the closed drop-down box: "All items" when everything is shown, otherwise how
    /// many are hidden. Recomputed whenever a tick changes.
    /// </summary>
    public string Summary
    {
        get
        {
            var hidden = Items.Count(i => !i.IsVisible);
            return hidden == 0 ? "All items" : $"{hidden} hidden";
        }
    }

    public ContextMenuSection(string key)
    {
        Key = key;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Summary));
    }

    /// <summary>Recalculates the summary after a row's tick changes.</summary>
    internal void RefreshSummary() => OnPropertyChanged(nameof(Summary));
    /// <summary>
    /// Rebuilds the rows from the live menu headers. A header is ticked unless it is in the saved
    /// hidden list for this menu.
    /// </summary>
    internal void Fill(List<string> headers)
    {
        Se.Settings.General.HiddenContextMenuItems.TryGetValue(Key, out var hidden);
        var hiddenSet = new HashSet<string>(hidden ?? new List<string>(), StringComparer.Ordinal);

        Items.Clear();
        foreach (var header in headers)
        {
            var row = new ContextMenuItemDisplay(header, !hiddenSet.Contains(header));
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ContextMenuItemDisplay.IsVisible))
                {
                    OnPropertyChanged(nameof(Summary));
                }
            };
            Items.Add(row);
        }
    }

    /// <summary>
    /// Writes the unticked headers of this menu's rows into the settings. Empty sections are removed.
    /// </summary>
    internal void Save(Dictionary<string, List<string>> target)
    {
        var off = Items.Where(i => !i.IsVisible).Select(i => i.Name).ToList();
        if (off.Count > 0)
        {
            target[Key] = off;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Items)
        {
            item.IsVisible = true;
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var item in Items)
        {
            item.IsVisible = false;
        }
    }
}
