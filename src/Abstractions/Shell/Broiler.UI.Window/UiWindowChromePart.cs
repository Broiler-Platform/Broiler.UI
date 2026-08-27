namespace Broiler.UI.Window;

/// <summary>A hit-testable region of owner-drawn window chrome.</summary>
public enum UiWindowChromePart
{
    /// <summary>Not chrome — content, or outside the window.</summary>
    None = 0,

    /// <summary>The draggable strip of the title bar.</summary>
    TitleBar,

    /// <summary>The window icon.</summary>
    Icon,

    /// <summary>The minimize button.</summary>
    Minimize,

    /// <summary>The maximize/restore button.</summary>
    Maximize,

    /// <summary>The close button.</summary>
    Close,
}
