using System;
using Broiler.Graphics;

namespace Broiler.UI.Window;

/// <summary>
/// Where the parts of an owner-drawn title bar sit inside a window's bounds, and which of them
/// exist for that window. Implementations paint from this and hit-test against it, so every
/// control family lays its chrome out the same way.
/// </summary>
public readonly struct UiWindowChromeLayout
{
    private UiWindowChromeLayout(
        bool isVisible,
        BRect titleBar,
        BRect icon,
        BRect title,
        BRect minimize,
        BRect maximize,
        BRect close,
        BRect content)
    {
        IsVisible = isVisible;
        TitleBar = titleBar;
        Icon = icon;
        Title = title;
        MinimizeButton = minimize;
        MaximizeButton = maximize;
        CloseButton = close;
        Content = content;
    }

    /// <summary>False when the window draws no chrome at all; every rect is then empty.</summary>
    public bool IsVisible { get; }

    /// <summary>The full title bar strip across the top of the window.</summary>
    public BRect TitleBar { get; }

    /// <summary>The window icon, empty when the window has none.</summary>
    public BRect Icon { get; }

    /// <summary>The space left for the title text between the icon and the system buttons.</summary>
    public BRect Title { get; }

    /// <summary>The minimize button, empty when the window does not offer one.</summary>
    public BRect MinimizeButton { get; }

    /// <summary>The maximize/restore button, empty when the window does not offer one.</summary>
    public BRect MaximizeButton { get; }

    /// <summary>The close button, empty when the window does not offer one.</summary>
    public BRect CloseButton { get; }

    /// <summary>What is left of the window below the title bar.</summary>
    public BRect Content { get; }

    /// <summary>An empty layout for a window that draws no chrome.</summary>
    public static UiWindowChromeLayout Hidden(BRect bounds) =>
        new(false, BRect.Empty, BRect.Empty, BRect.Empty, BRect.Empty, BRect.Empty, BRect.Empty, bounds);

    /// <summary>
    /// Lays the chrome of <paramref name="window"/> out inside <paramref name="bounds"/>. System
    /// buttons are packed against the right edge in close, maximize, minimize order; the title
    /// takes whatever remains between the icon and the leftmost button.
    /// </summary>
    public static UiWindowChromeLayout Create(UiWindow window, BRect bounds, UiWindowChromeMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!window.IsTitleBarVisible || bounds.IsEmpty)
            return Hidden(bounds);

        double barHeight = Math.Min(Math.Max(0, metrics.TitleBarHeight), bounds.Height);
        if (barHeight <= 0)
            return Hidden(bounds);

        var titleBar = new BRect(bounds.Left, bounds.Top, bounds.Width, barHeight);
        double buttonWidth = Math.Max(0, metrics.ButtonWidth);

        double right = titleBar.Right;
        BRect close = BRect.Empty;
        BRect maximize = BRect.Empty;
        BRect minimize = BRect.Empty;

        if (window.ShowsCloseButton && right - buttonWidth >= titleBar.Left)
        {
            close = new BRect(right - buttonWidth, titleBar.Top, buttonWidth, barHeight);
            right = close.Left;
        }

        if (window.ShowsMaximizeButton && right - buttonWidth >= titleBar.Left)
        {
            maximize = new BRect(right - buttonWidth, titleBar.Top, buttonWidth, barHeight);
            right = maximize.Left;
        }

        if (window.ShowsMinimizeButton && right - buttonWidth >= titleBar.Left)
        {
            minimize = new BRect(right - buttonWidth, titleBar.Top, buttonWidth, barHeight);
            right = minimize.Left;
        }

        double left = titleBar.Left + Math.Max(0, metrics.Padding);
        BRect icon = BRect.Empty;
        if (window.Icon is not null && metrics.IconSize > 0)
        {
            double iconSize = Math.Min(metrics.IconSize, barHeight);
            icon = new BRect(left, titleBar.Top + ((barHeight - iconSize) / 2), iconSize, iconSize);
            left = icon.Right + Math.Max(0, metrics.Padding) / 2;
        }

        var title = new BRect(left, titleBar.Top, Math.Max(0, right - left), barHeight);
        var content = new BRect(
            bounds.Left,
            titleBar.Bottom,
            bounds.Width,
            Math.Max(0, bounds.Height - barHeight));

        return new UiWindowChromeLayout(true, titleBar, icon, title, minimize, maximize, close, content);
    }

    /// <summary>
    /// Which part of the chrome <paramref name="point"/> falls in. The buttons win over the title
    /// bar they sit in, so a press on a button never starts a window drag.
    /// </summary>
    public UiWindowChromePart HitTest(BPoint point)
    {
        if (!IsVisible || !TitleBar.Contains(point))
            return UiWindowChromePart.None;

        if (!CloseButton.IsEmpty && CloseButton.Contains(point))
            return UiWindowChromePart.Close;
        if (!MaximizeButton.IsEmpty && MaximizeButton.Contains(point))
            return UiWindowChromePart.Maximize;
        if (!MinimizeButton.IsEmpty && MinimizeButton.Contains(point))
            return UiWindowChromePart.Minimize;
        if (!Icon.IsEmpty && Icon.Contains(point))
            return UiWindowChromePart.Icon;

        return UiWindowChromePart.TitleBar;
    }
}
