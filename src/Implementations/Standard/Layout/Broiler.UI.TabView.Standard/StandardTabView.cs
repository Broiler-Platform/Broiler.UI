using System;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.TabView.Standard;

public sealed class StandardTabView : UiTabView, IStandardThemedControl
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        // Follow the theme's body font, including a text-scaled theme, unless the application set its own.
        BFontStyle themeFont = StandardThemeFonts.For(theme, StandardTextStyle.Body);
        BFontStyle followed = StandardThemeFonts.Follow(Font, _themeFont, themeFont);
        _themeFont = themeFont;
        if (followed != Font)
        {
            Font = followed;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
        Background = theme.Surface;
        SelectedHeaderBackground = theme.Surface;
        Foreground = theme.Text;
        SelectedHeaderForeground = theme.AccentText;
        BorderColor = theme.Border;
        FocusRing = theme.FocusRing;
    }

    private BColor? _selectedHeaderForeground;

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor HeaderBackground { get; set; } = BColor.Transparent;

    public BColor SelectedHeaderBackground { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    /// <summary>
    /// The selected tab's label. <see cref="ApplyTheme"/> sets it to the theme's
    /// <see cref="StandardThemeTokens.AccentText"/>; until then, or until the application sets it, it is the shared
    /// palette's, read when the view is drawn.
    /// </summary>
    public BColor SelectedHeaderForeground
    {
        get => _selectedHeaderForeground ?? StandardControlPaint.AccentText;
        set
        {
            if (_selectedHeaderForeground == value)
                return;

            _selectedHeaderForeground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BFontStyle Font { get; set; } = StandardControlPaint.Theme.FontBody;

    private BFontStyle _themeFont = StandardControlPaint.Theme.FontBody;

    // The size measure last offered every tab's content, and the shown content when arrange measured
    // it again at its own rectangle since then.
    private BSize _contentMeasureSize;
    private UiElement? _remeasuredContent;
    private BSize _remeasuredSize;

    public double HeaderHeight { get; set; } = 32;

    /// <summary>
    /// The height the header strip actually takes: <see cref="HeaderHeight"/>, or more when the font
    /// (for example, at a larger system text size) needs it, so tab names are never cut off.
    /// </summary>
    public double EffectiveHeaderHeight => Math.Max(HeaderHeight,
        Math.Ceiling(BTextMeasurer.GetLineHeight(Font) + Math.Max(0, HeaderHeight - BTextMeasurer.GetLineHeight(BFontStyle.Default))));

    public double HeaderPaddingX { get; set; } = 12;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    protected override BSize MeasureCore(BSize availableSize)
    {
        double availableContentWidth = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : double.PositiveInfinity;
        double availableContentHeight = double.IsFinite(availableSize.Height)
            ? Math.Max(0, availableSize.Height - EffectiveHeaderHeight)
            : double.PositiveInfinity;
        BSize contentAvailableSize = new(availableContentWidth, availableContentHeight);
        _contentMeasureSize = contentAvailableSize;
        _remeasuredContent = null;

        double maxContentWidth = 0;
        double maxContentHeight = 0;

        foreach (UiTabItem tab in Tabs)
        {
            if (tab.Content is null)
                continue;

            BSize desired = tab.Content.Measure(contentAvailableSize);
            maxContentWidth = Math.Max(maxContentWidth, desired.Width);
            maxContentHeight = Math.Max(maxContentHeight, desired.Height);
        }

        double headersWidth = 0;
        for (int index = 0; index < Tabs.Count; index++)
            headersWidth += Math.Max(48, BTextMeasurer.MeasureAdvance(Tabs[index].Header, Font) + HeaderPaddingX * 2);

        double desiredWidth = Math.Max(headersWidth, maxContentWidth);
        double desiredHeight = EffectiveHeaderHeight + maxContentHeight;

        if (PreferredSize.Width > 0 && double.IsInfinity(availableSize.Width))
            desiredWidth = Math.Max(desiredWidth, PreferredSize.Width);
        if (PreferredSize.Height > 0 && double.IsInfinity(availableSize.Height))
            desiredHeight = Math.Max(desiredHeight, PreferredSize.Height);

        return new BSize(ClampDesired(desiredWidth, availableSize.Width), ClampDesired(desiredHeight, availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        BRect contentRect = new(finalRect.Left, finalRect.Top + EffectiveHeaderHeight, finalRect.Width, Math.Max(0, finalRect.Height - EffectiveHeaderHeight));
        for (int index = 0; index < Tabs.Count; index++)
        {
            UiElement? content = Tabs[index].Content;
            if (content is null)
                continue;

            content.Visibility = InactiveContentPolicy == UiTabContentLifetimePolicy.CollapseInactive && index != SelectedIndex
                ? UiVisibility.Collapsed
                : UiVisibility.Visible;

            // A hidden tab keeps the arrangement it was last shown with. Arranging it at an empty
            // rectangle laid a hidden form out at no width and clamped its scroll positions, so the
            // user came back to a tab that was not as they had left it. It is neither drawn nor hit.
            if (index != SelectedIndex)
                continue;

            // Measured at the rectangle it is given. That is usually the size measure already used,
            // and the content's measure cache makes this free. When it is not, in either dimension
            // (a tab shown again after the window was resized, or a tab view measured at an unbounded
            // height), the content is laid out at the size it really has rather than the width alone.
            if (double.IsFinite(contentRect.Width) && double.IsFinite(contentRect.Height) && !FitsAsMeasured(content, contentRect.Size))
            {
                content.Measure(contentRect.Size);
                _remeasuredContent = content;
                _remeasuredSize = contentRect.Size;
            }
            content.Arrange(contentRect);
        }
    }

    /// <summary>
    /// Whether the content is already laid out for a rectangle no taller than the one it was last
    /// measured at: the same width, and a height between what it asked for and what it was offered.
    /// A parent that stacks the tab view arranges it at the height it asked for, not the height it
    /// offered, and measuring the content again there would measure it twice on every layout pass,
    /// at two sizes, changing nothing. Content that is not measured, was offered an unbounded height,
    /// does not fit, or is given more room than it was offered is measured again.
    /// </summary>
    private bool FitsAsMeasured(UiElement content, BSize size)
    {
        const double Tolerance = 0.001;
        // Only the tab view measures its content: every tab at what measure offered, then the shown
        // one again here when it did not fit.
        BSize measuredAt = ReferenceEquals(_remeasuredContent, content) ? _remeasuredSize : _contentMeasureSize;
        return content.IsMeasureValid
            && Math.Abs(measuredAt.Width - size.Width) <= Tolerance
            && double.IsFinite(measuredAt.Height)
            && size.Height <= measuredAt.Height + Tolerance
            && content.DesiredSize.Height <= size.Height + Tolerance;
    }

    // The page's frame. The page itself is drawn and hit-tested inside it.
    private const double FrameThickness = 1;

    /// <summary>The page below the header strip, where the selected tab's content is arranged.</summary>
    private BRect PageBounds => new(Bounds.Left, Bounds.Top + EffectiveHeaderHeight, Bounds.Width, Math.Max(0, Bounds.Height - EffectiveHeaderHeight));

    /// <summary>
    /// The part of the page the selected content shows in: the page inside its frame. The content is
    /// arranged at the whole page, so what it draws at its own edges (the last, partly scrolled row of a
    /// list, a scrollbar) used to paint over the frame. It is drawn, hit-tested and reported as visible
    /// (<see cref="UiElement.GetVisibleBounds"/>) here.
    /// </summary>
    private BRect PageClipBounds => StandardControlPaint.Inset(PageBounds, FrameThickness);

    protected override BRect? GetClipBoundsForChild(UiElement child) => PageClipBounds;

    protected override bool ShouldHitTestChildren(BPoint point) => PageClipBounds.Contains(point);

    protected override void RenderCore(UiRenderContext context)
    {
        BRect content = PageBounds;
        StandardControlPaint.FillRounded(context.RenderList, content, Background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, content, BorderColor, CornerRadius, FrameThickness);

        double x = Bounds.Left;
        for (int index = 0; index < Tabs.Count; index++)
        {
            BRect header = GetHeaderBounds(index, x);
            x = header.Right;
            bool selected = index == SelectedIndex;
            BColor headerBackground = selected ? SelectedHeaderBackground : HeaderBackground;
            if (!headerBackground.IsEmpty && headerBackground.A > 0)
                StandardControlPaint.FillRounded(context.RenderList, header, headerBackground, CornerRadius);

            BColor headerForeground = selected ? SelectedHeaderForeground : Foreground;
            context.RenderList.DrawText(new BTextRun(Tabs[index].Header, Font, headerForeground), new BPoint(header.Left + HeaderPaddingX, header.Top + Math.Max(0, (EffectiveHeaderHeight - BTextMeasurer.GetLineHeight(Font)) / 2)));
        }

        if (SelectedTab?.Content is { } selectedContent)
        {
            context.RenderList.PushClip(PageClipBounds);
            selectedContent.Render(context);
            context.RenderList.PopClip();
        }

        if (Session?.FocusedElement == this)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, Math.Max(0, CornerRadius - 2), 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointer(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    private bool HandlePointer(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        double x = Bounds.Left;
        for (int index = 0; index < Tabs.Count; index++)
        {
            BRect header = GetHeaderBounds(index, x);
            x = header.Right;
            if (header.Contains(input.Position))
            {
                Session?.SetFocus(this);
                SelectIndex(index);
                return true;
            }
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down || Tabs.Count == 0)
            return false;

        // The strip's keys, read only while the strip has focus. A key the focused control inside a
        // tab leaves unhandled (Right on a button) bubbles up to here, and switching tabs for it took
        // the user out of the tab they were working in. A key that reaches the view while nothing has
        // focus was not aimed at the strip either.
        if (Session?.FocusedElement != this)
            return false;

        if (IsKey(input, BVirtualKey.Right, "Right"))
            return SelectAndFocus((SelectedIndex + 1) % Tabs.Count);
        if (IsKey(input, BVirtualKey.Left, "Left"))
            return SelectAndFocus((SelectedIndex + Tabs.Count - 1) % Tabs.Count);
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return SelectAndFocus(0);
        if (IsKey(input, BVirtualKey.End, "End"))
            return SelectAndFocus(Tabs.Count - 1);

        return false;
    }

    private bool SelectAndFocus(int index)
    {
        Session?.SetFocus(this);
        return SelectIndex(index);
    }

    /// <summary>
    /// The header as it is drawn and hit-tested: as wide as its text plus <see cref="HeaderPaddingX"/>
    /// on each side (at least 48 DIP), laid out left to right from the view's left edge, and
    /// <see cref="EffectiveHeaderHeight"/> tall.
    /// </summary>
    public override BRect GetTabHeaderBounds(int index)
    {
        if ((uint)index >= (uint)Tabs.Count || Visibility != UiVisibility.Visible || Bounds.IsEmpty)
            return BRect.Empty;

        double x = Bounds.Left;
        for (int previous = 0; previous < index; previous++)
            x = GetHeaderBounds(previous, x).Right;
        return GetHeaderBounds(index, x);
    }

    private BRect GetHeaderBounds(int index, double left)
    {
        double width = BTextMeasurer.MeasureAdvance(Tabs[index].Header, Font) + HeaderPaddingX * 2;
        return new BRect(left, Bounds.Top, Math.Max(48, width), EffectiveHeaderHeight);
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
