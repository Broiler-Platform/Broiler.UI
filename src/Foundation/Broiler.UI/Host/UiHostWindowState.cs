namespace Broiler.UI;

/// <summary>
/// Show state of the native window behind a host, as reported and driven through
/// <see cref="IUiWindowChromeHost"/>. The neutral mirror of the control-level
/// <c>UiWindowState</c>, which lives in the window contract assembly and is not visible here.
/// </summary>
public enum UiHostWindowState
{
    Normal = 0,
    Minimized,
    Maximized,
}
