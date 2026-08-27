namespace Broiler.UI.Window;

/// <summary>
/// Sizes an owner-drawn title bar. Kept separate from the colors so an implementation can restyle
/// the chrome without restating its geometry, and so the layout is testable without a renderer.
/// </summary>
/// <param name="TitleBarHeight">Height of the title bar strip.</param>
/// <param name="Padding">Inset from the window edge to the icon and title text.</param>
/// <param name="ButtonWidth">Width of one system button.</param>
/// <param name="IconSize">Edge length of the square window icon.</param>
public readonly record struct UiWindowChromeMetrics(
    double TitleBarHeight,
    double Padding,
    double ButtonWidth,
    double IconSize)
{
    /// <summary>Desktop-sized defaults, in device-independent pixels.</summary>
    public static UiWindowChromeMetrics Default { get; } = new(32, 10, 44, 16);

    /// <summary>The tighter chrome a dialog uses.</summary>
    public static UiWindowChromeMetrics Compact { get; } = new(30, 10, 36, 14);
}
