using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;
using Broiler.UI.Window;

namespace Broiler.UI.Dialog.Standard;

public sealed class StandardDialog : UiDialog, IStandardThemedControl
{
    private readonly UiWindowChromeController _chrome;

    public StandardDialog()
    {
        _chrome = new UiWindowChromeController(this) { Metrics = UiWindowChromeMetrics.Compact };
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

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor TitleBarBackground { get; set; } = BColor.FromArgb(0xFF, 0xF2, 0xF6, 0xFB);

    public BColor TitleForeground { get; set; } = StandardControlPaint.Text;

    public BColor InactiveTitleForeground { get; set; } = StandardControlPaint.TextMuted;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor ActiveBorderColor { get; set; } = StandardControlPaint.Accent;

    public BFontStyle TitleFont { get; set; } = BFontStyle.Default;

    public BSize PreferredSize { get; set; } = new(320, 180);

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

    public double Padding { get; set; } = 12;

    public double CornerRadius { get; set; } = 8;

    /// <summary>Where the owner-drawn chrome currently sits, after the last arrange pass.</summary>
    public UiWindowChromeLayout ChromeLayout => _chrome.Layout;

    /// <summary>
    /// The title-bar height actually reserved. Zero once the dialog has broken out into a native
    /// window whose window manager draws the title bar itself, so the content is not pushed down
    /// by a strip that is no longer drawn.
    /// </summary>
    private double EffectiveTitleBarHeight => IsTitleBarVisible ? TitleBarHeight : 0;

    protected override BSize MeasureCore(BSize availableSize)
    {
        double titleBarHeight = EffectiveTitleBarHeight;
        BSize contentAvailable = new(
            Math.Max(0, availableSize.Width - (Padding * 2)),
            Math.Max(0, availableSize.Height - titleBarHeight - (Padding * 2)));
        BSize contentDesired = BSize.Empty;

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            child.Measure(contentAvailable);
            if (child is UiWindow ownedWindow && ReferenceEquals(ownedWindow.Owner, this))
                continue;

            contentDesired = new BSize(
                Math.Max(contentDesired.Width, child.DesiredSize.Width),
                Math.Max(contentDesired.Height, child.DesiredSize.Height));
        }

        double width = Math.Max(PreferredSize.Width, contentDesired.Width + (Padding * 2));
        double height = Math.Max(PreferredSize.Height, contentDesired.Height + titleBarHeight + (Padding * 2));
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Session is not null)
            BindViewport(new UiViewportBinding(finalRect.Size, Session.Host.Scale));

        BRect client = StandardControlPaint.Inset(_chrome.UpdateLayout(finalRect).Content, Padding);
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            if (child is UiWindow ownedWindow && ReferenceEquals(ownedWindow.Owner, this))
                child.Arrange(GetOwnedWindowBounds(ownedWindow, finalRect));
            else
                child.Arrange(client);
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        // A broken-out dialog fills its own native window, which has square corners; only a
        // logical subwindow floats above its owner and wants a rounded card.
        double radius = IsBrokenOut ? 0 : CornerRadius;
        StandardControlPaint.FillRounded(context.RenderList, Bounds, Background, radius);
        RenderChrome(context, radius);

        base.RenderCore(context);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, IsActive ? ActiveBorderColor : BorderColor, radius, IsActive ? 2 : 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        // The chrome runs first so a press on the close button is never taken for a title-bar
        // drag, and so a broken-out dialog moves its native window instead of its placement.
        if (_chrome.HandleInput(input))
            return true;

        if (base.OnInput(input))
            return true;

        if (input.Kind == UiInputEventKind.PointerButton)
            return HandlePointerButton(input);
        if (input.Kind == UiInputEventKind.KeyboardKey)
            return HandleKeyboard(input);

        return false;
    }

    protected override bool HitTestMoveGrip(BPoint position) =>
        _chrome.Layout.HitTest(position) == UiWindowChromePart.TitleBar;

    private void RenderChrome(UiRenderContext context, double cornerRadius)
    {
        UiWindowChromeLayout layout = _chrome.Layout;
        if (!layout.IsVisible)
            return;

        BRenderList renderList = context.RenderList;
        StandardWindowChromePaint.FillTitleBar(renderList, layout.TitleBar, TitleBarBackground, cornerRadius);
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

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Activate();
            Session?.SetFocus(this);
            if (!IsModal)
                Session?.CaptureInput(this);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            if (!IsModal)
                Session?.ReleaseInputCapture(this);
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
            return Cancel();
        if (IsKey(input, BVirtualKey.Enter, "Enter"))
            return Accept();

        return false;
    }

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

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
