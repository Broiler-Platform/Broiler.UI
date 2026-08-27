using System;
using Broiler.Graphics;
using Broiler.UI.Standard;

namespace Broiler.UI.Window.Standard;

public sealed class StandardWindow : UiWindow, IStandardThemedControl
{
    private readonly UiWindowChromeController _chrome;

    public StandardWindow()
    {
        _chrome = new UiWindowChromeController(this) { Metrics = UiWindowChromeMetrics.Default };
    }

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        TitleBarBackground = theme.SurfaceAlt;
        TitleForeground = theme.Text;
        InactiveTitleForeground = theme.TextMuted;
        BorderColor = theme.Border;
        ActiveBorderColor = theme.Accent;
    }

    public BColor Background { get; set; } = BColor.White;

    public BColor TitleBarBackground { get; set; } = StandardControlPaint.SurfaceAlt;

    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;

    public BColor InactiveTitleForeground { get; set; } = StandardControlPaint.TextMuted;

    public BColor BorderColor { get; set; } = BColor.FromArgb(0xFF, 0x66, 0x66, 0x66);

    public BColor ActiveBorderColor { get; set; } = BColor.FromArgb(0xFF, 0x00, 0x66, 0xCC);

    public double BorderThickness { get; set; } = 1;

    public BFontStyle TitleFont { get; set; } = BFontStyle.Default;

    /// <summary>Height of the owner-drawn title bar. Ignored when the window draws no chrome.</summary>
    public double TitleBarHeight
    {
        get => _chrome.Metrics.TitleBarHeight;
        set => _chrome.Metrics = _chrome.Metrics with { TitleBarHeight = Math.Max(0, value) };
    }

    /// <summary>Width of one system button in the owner-drawn title bar.</summary>
    public double SystemButtonWidth
    {
        get => _chrome.Metrics.ButtonWidth;
        set => _chrome.Metrics = _chrome.Metrics with { ButtonWidth = Math.Max(0, value) };
    }

    /// <summary>Where the owner-drawn chrome currently sits, after the last arrange pass.</summary>
    public UiWindowChromeLayout ChromeLayout => _chrome.Layout;

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize contentAvailable = ReserveTitleBar(availableSize);
        foreach (UiElement child in Children)
        {
            if (child.Visibility != UiVisibility.Collapsed)
                child.Measure(contentAvailable);
        }

        return availableSize;
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect content = _chrome.UpdateLayout(finalRect).Content;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            if (child is UiWindow ownedWindow && ReferenceEquals(ownedWindow.Owner, this))
                child.Arrange(GetOwnedWindowBounds(ownedWindow, content));
            else
                child.Arrange(content);
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        RenderChrome(context);
        base.RenderCore(context);

        if (BorderThickness > 0)
            context.RenderList.StrokeRect(Bounds, IsActive ? ActiveBorderColor : BorderColor, BorderThickness);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        // The chrome gets first refusal: a press on a system button must never reach the content
        // beneath it, and a title-bar press starts a window drag rather than a click.
        if (_chrome.HandleInput(input))
            return true;

        return base.OnInput(input);
    }

    private BSize ReserveTitleBar(BSize availableSize)
    {
        if (!IsTitleBarVisible || double.IsInfinity(availableSize.Height))
            return availableSize;

        return new BSize(availableSize.Width, Math.Max(0, availableSize.Height - _chrome.Metrics.TitleBarHeight));
    }

    private void RenderChrome(UiRenderContext context)
    {
        UiWindowChromeLayout layout = _chrome.Layout;
        if (!layout.IsVisible)
            return;

        BRenderList renderList = context.RenderList;
        StandardWindowChromePaint.FillTitleBar(renderList, layout.TitleBar, TitleBarBackground, 0);
        if (Icon is not null)
            StandardWindowChromePaint.DrawIcon(renderList, layout.Icon, Icon.Image);

        BColor titleColor = IsActive ? TitleForeground : InactiveTitleForeground;
        StandardWindowChromePaint.DrawTitleText(renderList, layout.Title, Title, TitleFont, titleColor);

        DrawSystemButton(renderList, layout.MinimizeButton, StandardWindowChromeGlyph.Minimize, UiWindowChromePart.Minimize, titleColor);
        DrawSystemButton(
            renderList,
            layout.MaximizeButton,
            State == UiWindowState.Maximized ? StandardWindowChromeGlyph.Restore : StandardWindowChromeGlyph.Maximize,
            UiWindowChromePart.Maximize,
            titleColor);
        DrawSystemButton(renderList, layout.CloseButton, StandardWindowChromeGlyph.Close, UiWindowChromePart.Close, titleColor);
    }

    private void DrawSystemButton(
        BRenderList renderList,
        BRect bounds,
        StandardWindowChromeGlyph glyph,
        UiWindowChromePart part,
        BColor glyphColor) =>
        StandardWindowChromePaint.DrawButton(
            renderList,
            bounds,
            glyph,
            _chrome.HotPart == part,
            _chrome.PressedPart == part,
            glyphColor);

    private static BRect GetOwnedWindowBounds(UiWindow window, BRect ownerBounds)
    {
        BRect placement = window.Placement;
        if (placement.IsEmpty)
        {
            double width = window.DesiredSize.Width > 0 ? Math.Min(window.DesiredSize.Width, ownerBounds.Width) : Math.Max(0, ownerBounds.Width / 2);
            double height = window.DesiredSize.Height > 0 ? Math.Min(window.DesiredSize.Height, ownerBounds.Height) : Math.Max(0, ownerBounds.Height / 2);
            placement = new BRect(24, 24, width, height);
        }

        var absolute = new BRect(
            ownerBounds.Left + placement.X,
            ownerBounds.Top + placement.Y,
            Math.Min(placement.Width, ownerBounds.Width),
            Math.Min(placement.Height, ownerBounds.Height));

        return absolute.Intersect(ownerBounds);
    }
}
