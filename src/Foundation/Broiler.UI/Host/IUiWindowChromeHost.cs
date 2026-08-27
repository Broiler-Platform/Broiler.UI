using System;
using Broiler.Graphics;

namespace Broiler.UI;

/// <summary>
/// Optional host capability that lets the UI draw and drive a native window's chrome — the title
/// bar, icon, and system buttons — instead of the window manager. Discovered via
/// <c>Host is IUiWindowChromeHost</c>, mirroring the other optional host capabilities, and
/// implemented by a primary <see cref="IUiHost"/> as well as by an <see cref="IUiHostWindow"/>.
/// </summary>
/// <remarks>
/// The capability trades only in neutral types: no native handle crosses it (ADR 0002). A host
/// that cannot suppress its platform frame still implements the interface and reports
/// <see cref="Chrome"/> as <see cref="UiHostWindowChrome.System"/>, and the UI then leaves the
/// title bar to the window manager rather than drawing a second one.
/// </remarks>
public interface IUiWindowChromeHost
{
    /// <summary>
    /// Who actually draws this window's frame. <see cref="UiHostWindowChrome.Owner"/> means the
    /// platform frame is suppressed and the UI is responsible for the title bar and buttons.
    /// </summary>
    UiHostWindowChrome Chrome { get; }

    /// <summary>Whether the user may resize and maximize the window.</summary>
    bool IsResizable { get; }

    /// <summary>The current native show state.</summary>
    UiHostWindowState WindowState { get; }

    /// <summary>
    /// Raised when the native window is minimized, maximized, or restored — including by the OS
    /// (Win+Down, snap, the taskbar), so owner-drawn chrome can repaint its restore button.
    /// </summary>
    event EventHandler? WindowStateChanged;

    /// <summary>Minimizes, maximizes, or restores the native window.</summary>
    void SetWindowState(UiHostWindowState state);

    /// <summary>Sets the native window title, which the taskbar and Alt+Tab still show.</summary>
    void SetTitle(string title);

    /// <summary>
    /// Sets the native window icon (taskbar, Alt+Tab) from straight-alpha RGBA pixels, or clears
    /// it when <paramref name="icon"/> is null. The icon drawn in the title bar is separate: the
    /// UI paints that one itself.
    /// </summary>
    void SetIcon(BPixelBuffer? icon);

    /// <summary>
    /// Asks the host to close the native window, as the system close button would. The host
    /// reports it back through its own close-request path so the framework can veto.
    /// </summary>
    void RequestClose();

    /// <summary>
    /// Hands the in-progress pointer press to the window manager as a window move. An owner-drawn
    /// title bar calls this on press, so dragging, snapping, and shake keep behaving natively
    /// instead of being simulated from pointer deltas.
    /// </summary>
    void BeginMoveDrag();

    /// <summary>
    /// Hands the in-progress pointer press to the window manager as a resize of
    /// <paramref name="edge"/>.
    /// </summary>
    void BeginResizeDrag(UiWindowEdge edge);
}
