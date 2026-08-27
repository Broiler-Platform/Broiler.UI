namespace Broiler.UI;

/// <summary>Who draws the title bar and border of a native host window.</summary>
public enum UiHostWindowChrome
{
    /// <summary>The window manager draws its own title bar, border, and system buttons.</summary>
    System = 0,

    /// <summary>
    /// The platform frame is suppressed and the UI draws the title bar, icon, and system buttons
    /// itself. This is what Broiler.UI asks for, so a window looks the same wherever it is hosted
    /// and a broken-out window does not end up with two stacked title bars.
    /// </summary>
    Owner,
}
