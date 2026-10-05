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
        _theme = theme;
    }

    // The focus ring's offset and thickness.
    private StandardThemeTokens _theme = StandardControlPaint.Theme;
    private BColor _selectedHeaderForeground = StandardControlPaint.AccentText;
    private BColor? _selectedIndicatorColor;
    private double _selectedIndicatorThickness = 2;

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor HeaderBackground { get; set; } = BColor.Transparent;

    public BColor SelectedHeaderBackground { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    /// <summary>
    /// The selected tab's label. <see cref="ApplyTheme"/> sets it to the theme's
    /// <see cref="StandardThemeTokens.AccentText"/>; until then it is the shared palette's when the view was
    /// built, as the fills it is drawn on are, so a later change of the shared palette does not put one
    /// palette's label on another's fill.
    /// </summary>
    public BColor SelectedHeaderForeground
    {
        get => _selectedHeaderForeground;
        set
        {
            if (_selectedHeaderForeground == value)
                return;

            _selectedHeaderForeground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    /// <summary>
    /// The thickness of the bar drawn under the selected tab's label, along the bottom of its header: a mark
    /// of the selected tab that does not rely on the label's color alone (WCAG 1.4.1). 2 DIP by default, which
    /// leaves the focus ring room between the bar and the label's descenders; 0 draws none. It is drawn no
    /// thicker than the room between the label's line and the bottom of the header, so it never covers the
    /// label. The bar lies inside the header, so <see cref="EffectiveHeaderHeight"/>, the header bounds and hit
    /// testing are the same with or without it.
    /// </summary>
    public double SelectedIndicatorThickness
    {
        get => _selectedIndicatorThickness;
        set
        {
            if (_selectedIndicatorThickness.Equals(value))
                return;

            _selectedIndicatorThickness = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    /// <summary>
    /// The color of the bar under the selected tab's label: <see cref="SelectedHeaderForeground"/> until the
    /// application sets it. That is the theme's accent text, which in every preset has at least 4.5:1 against
    /// the selected header's fill, more than the 3:1 a mark that is not text needs.
    /// </summary>
    public BColor SelectedIndicatorColor
    {
        get => _selectedIndicatorColor ?? SelectedHeaderForeground;
        set
        {
            if (_selectedIndicatorColor == value)
                return;

            _selectedIndicatorColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    /// <summary>
    /// The color of the ring drawn around the selected tab's header while the view has focus. The ring's
    /// offset and thickness come from the theme. When this color has less than 3:1 against the selected
    /// header's fill, the ring takes <see cref="SelectedHeaderForeground"/> instead.
    /// </summary>
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
            headersWidth += Math.Max(MinHeaderWidth, BTextMeasurer.MeasureAdvance(Tabs[index].Header, Font) + HeaderPaddingX * 2);

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
            context.RenderList.DrawText(new BTextRun(Tabs[index].Header, Font, headerForeground), new BPoint(header.Left + HeaderPaddingX, header.Top + LabelMargin));

            BRect indicator = selected ? GetSelectedIndicatorBounds(header) : BRect.Empty;
            if (!indicator.IsEmpty)
                StandardControlPaint.FillRounded(context.RenderList, indicator, SelectedIndicatorColor, indicator.Height / 2);
        }

        if (SelectedTab?.Content is { } selectedContent)
        {
            context.RenderList.PushClip(PageClipBounds);
            selectedContent.Render(context);
            context.RenderList.PopClip();
        }

        if (Session?.FocusedElement == this)
            DrawFocusRing(context);
    }

    /// <summary>
    /// The focus ring marks the selected tab's header, which is what the strip's keys act on. Around the whole
    /// view it enclosed every control on the page, did not say which tab had focus, and crossed the page's
    /// content. It is drawn the theme's ring offset inside the header, above the bar under the label, in the
    /// theme's ring thickness; around the visible strip when too little of the selected header lies inside the
    /// view, and around the view only while no tab is selected.
    /// </summary>
    private void DrawFocusRing(UiRenderContext context)
    {
        // A high-contrast palette draws its borders in its text color too, so a ring as thin as they are would
        // read as one more border: it is at least 2 DIP there, as in the high-contrast presets.
        double thickness = _theme.IsHighContrast ? Math.Max(2, _theme.FocusRingThickness) : _theme.FocusRingThickness;
        if (!(thickness > 0))
            return;

        double radius = Math.Max(0, CornerRadius - _theme.FocusRingOffset);
        BRect header = GetTabHeaderBounds(SelectedIndex);
        if (header.IsEmpty)
        {
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, _theme.FocusRingOffset), FocusRing, radius, thickness);
            return;
        }

        // Only the part of the header inside the view is ringed. Headers are laid out from the view's left edge
        // whatever its width, so in a strip wider than the view the selected one can lie partly or wholly past
        // its right edge, where a ring would cover a neighbor or not show at all. When too little of it shows to
        // read as a tab, the ring marks the visible strip, where the strip's keys still act.
        BRect visible = header.Intersect(Bounds);
        bool ringsHeader = !visible.IsEmpty && visible.Width >= Math.Min(header.Width, MinHeaderWidth);
        BRect target = ringsHeader ? visible : new BRect(Bounds.Left, header.Top, Bounds.Width, header.Height).Intersect(Bounds);
        BRect ring = GetFocusRingBounds(header, target, thickness);
        if (ring.IsEmpty)
            return;

        // The ring is drawn on the selected header's fill. A focus color that does not stand out from it
        // (3:1, as a focus indicator needs) would hide the ring, which then takes the selected label's
        // color, chosen to be read there.
        BColor color = ringsHeader
            ? StandardControlPaint.FocusRingColor(FocusRing, SelectedHeaderBackground, SelectedHeaderForeground)
            : FocusRing;

        StandardControlPaint.StrokeRounded(context.RenderList, ring, color, radius, thickness);
    }

    /// <summary>
    /// The ring around <paramref name="target"/> (the visible part of the selected header, or the visible
    /// strip): the theme's ring offset in from its sides and top, and the offset above the bar under the label.
    /// Its strokes stay out of the label's line, which is centered in the header: where a small
    /// <see cref="HeaderHeight"/> leaves less room around the label than the offset, the stroke and the bar
    /// need, the ring moves toward the header's edge, over the bar if it must, but not into the label's
    /// descenders.
    /// </summary>
    private BRect GetFocusRingBounds(BRect header, BRect target, double thickness)
    {
        double offset = _theme.FocusRingOffset;
        double half = thickness / 2;
        double margin = LabelMargin;
        double barTop = header.Bottom - SelectedIndicatorExtent;

        double top = Math.Max(target.Top + half, Math.Min(target.Top + offset, header.Top + margin - half));
        double bottom = Math.Min(target.Bottom - half, Math.Max(barTop - offset, header.Bottom - margin + half));
        double left = target.Left + offset;
        double right = target.Right - offset;
        return right > left && bottom > top ? BRect.FromLTRB(left, top, right, bottom) : BRect.Empty;
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
        return new BRect(left, Bounds.Top, Math.Max(MinHeaderWidth, width), EffectiveHeaderHeight);
    }

    // The narrowest a header is laid out.
    private const double MinHeaderWidth = 48;

    /// <summary>
    /// The room between a header's label and its top, and between the label and its bottom: the label's line
    /// is centered in <see cref="EffectiveHeaderHeight"/>.
    /// </summary>
    private double LabelMargin => Math.Max(0, (EffectiveHeaderHeight - BTextMeasurer.GetLineHeight(Font)) / 2);

    /// <summary>
    /// How thick the bar under the selected label is drawn: <see cref="SelectedIndicatorThickness"/>, but no
    /// more than the room under the label, so a thick bar does not cover the label's descenders; 0 for none.
    /// </summary>
    private double SelectedIndicatorExtent
    {
        get
        {
            double thickness = Math.Min(SelectedIndicatorThickness, LabelMargin);
            return thickness > 0 ? thickness : 0;
        }
    }

    /// <summary>
    /// The bar under the selected label: along the bottom of the header, on the page's frame, and as wide as
    /// the header less its padding on each side (the whole header when that leaves nothing). Only the part
    /// inside the view is drawn, since a strip wider than the view lays its last headers out past its edge.
    /// </summary>
    private BRect GetSelectedIndicatorBounds(BRect header)
    {
        double thickness = SelectedIndicatorExtent;
        if (!(thickness > 0) || header.IsEmpty)
            return BRect.Empty;

        double inset = header.Width > HeaderPaddingX * 2 ? Math.Max(0, HeaderPaddingX) : 0;
        return new BRect(header.Left + inset, header.Bottom - thickness, header.Width - (inset * 2), thickness).Intersect(Bounds);
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
