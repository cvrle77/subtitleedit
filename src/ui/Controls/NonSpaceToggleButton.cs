using Avalonia.Controls.Primitives;
using Avalonia.Input;
using System;

namespace Nikse.SubtitleEdit.Controls;

/// <summary>
/// A <see cref="ToggleButton"/> that never reacts to the Space key.
/// <para>
/// Same reason as <see cref="NonSpaceButton"/>: Avalonia's <see cref="ToggleButton"/> toggles on
/// Space whenever it has focus, so a toolbar toggle that gained focus from a mouse click swallows
/// (or duplicates) the global Space shortcut. The waveform "center on video position" toggles and
/// "select current subtitle while playing" sat in the tab order and flipped on Space, so pressing
/// the play/space transport key silently changed a mode (upstream bug #12759, same root cause as
/// #12093). These toggles keep Space owned by the shortcut and never take it.
/// </para>
/// </summary>
public class NonSpaceToggleButton : ToggleButton
{
    // Keep the default ToggleButton styling instead of looking for a NonSpaceToggleButton style.
    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }

        base.OnKeyUp(e);
    }
}
