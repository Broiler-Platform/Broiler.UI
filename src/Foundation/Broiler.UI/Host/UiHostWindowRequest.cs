using Broiler.Graphics;

namespace Broiler.UI;

/// <summary>
/// Neutral parameters for <see cref="IUiWindowHost.CreateHostWindow"/> when a logical window
/// breaks out into its own native top-level window.
/// </summary>
/// <param name="Title">Initial window title.</param>
/// <param name="Placement">
/// Requested initial placement in device-independent pixels. May be empty, in which case the
/// host chooses a default size and position.
/// </param>
/// <param name="IsModal">
/// True when the broken-out window is application-modal to its origin window. The origin session
/// blocks its own input regardless; the host may additionally apply native ownership/modality.
/// </param>
/// <param name="Chrome">
/// Who should draw the frame. Broiler.UI asks for <see cref="UiHostWindowChrome.Owner"/> so the
/// window keeps the title bar it already draws instead of gaining a second, native one. A host
/// that cannot suppress its platform frame may ignore this and report
/// <see cref="UiHostWindowChrome.System"/> from <see cref="IUiWindowChromeHost.Chrome"/>.
/// </param>
/// <param name="Resizable">Whether the user may resize and maximize the new window.</param>
public readonly record struct UiHostWindowRequest(
    string Title,
    BRect Placement,
    bool IsModal,
    UiHostWindowChrome Chrome = UiHostWindowChrome.Owner,
    bool Resizable = true);
