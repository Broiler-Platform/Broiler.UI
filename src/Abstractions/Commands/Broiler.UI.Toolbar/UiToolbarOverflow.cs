namespace Broiler.UI.Toolbar;

/// <summary>What a toolbar does with the items that do not fit along it.</summary>
public enum UiToolbarOverflow
{
    /// <summary>
    /// They move into a drop-down opened from a chevron at the end of the bar.
    /// This is the default, because a bar that silently hides a command is a bar
    /// that does not have it: the control is drawn past the edge, clipped away,
    /// and there is nothing on screen to say it exists.
    /// </summary>
    Menu = 0,

    /// <summary>
    /// They are laid out where they fall and clipped by the bar. Only for a host
    /// that guarantees the bar is wide enough for everything on it.
    /// </summary>
    Clip = 1,
}
