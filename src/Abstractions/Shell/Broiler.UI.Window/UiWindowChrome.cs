namespace Broiler.UI.Window;

/// <summary>Who draws a window's title bar, icon, and system buttons.</summary>
public enum UiWindowChrome
{
    /// <summary>
    /// Draw owner-drawn chrome whenever the window is responsible for it: a logical subwindow
    /// rendered inside its owner, or a window whose host reports
    /// <see cref="UiHostWindowChrome.Owner"/>. A host that keeps its platform title bar gets no
    /// second, owner-drawn one.
    /// </summary>
    Auto = 0,

    /// <summary>Always draw owner-drawn chrome, whatever the host reports.</summary>
    Owner,

    /// <summary>Never draw chrome. The window is all content.</summary>
    None,
}
