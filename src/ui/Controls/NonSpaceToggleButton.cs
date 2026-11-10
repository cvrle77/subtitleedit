using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Nikse.SubtitleEdit.Logic.Config;
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
/// <para>
/// With the fork features off this behaves exactly like the upstream <see cref="ToggleButton"/>
/// (Space included), so the control can stay in place at every call site.
/// </para>
/// <para>
/// A <see cref="ToggleButton"/> that never reacts to the Space key (see <see cref="NonSpaceButton"/>)
/// and does not flip <see cref="ToggleButton.IsChecked"/> itself when clicked: the owner sets it
/// from the state the click's command changed, so a command that does nothing (e.g. a player
/// without subtitle support) leaves the button as it was.
/// </para>
/// </summary>
public class NonSpaceToggleButton : ToggleButton
{
    // Keep the default ToggleButton styling instead of looking for a NonSpaceToggleButton style.
    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void Toggle()
    {
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Fork.Enabled && e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (Fork.Enabled && e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }

        base.OnKeyUp(e);
    }
}
