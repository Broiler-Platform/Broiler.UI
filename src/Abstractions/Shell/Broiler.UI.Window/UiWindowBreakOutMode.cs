namespace Broiler.UI.Window;

/// <summary>Whether a subwindow promotes itself into its own native window when it opens.</summary>
public enum UiWindowBreakOutMode
{
    /// <summary>
    /// Break out as soon as the window opens, whenever the session host supports it (ADR 0025).
    /// This is the default: an owned window or dialog is a real OS window the user can move to
    /// another monitor, and falls back to a logical subwindow on a host without the capability.
    /// </summary>
    Automatic = 0,

    /// <summary>
    /// Stay a logical subwindow rendered inside the owner's viewport until
    /// <see cref="UiWindow.BreakOut"/> is called explicitly. Popups, menus, and tooltips use this.
    /// </summary>
    Manual,
}
