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
        BorderColor = theme.Border;
        FocusRing = theme.FocusRing;
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor HeaderBackground { get; set; } = BColor.Transparent;

    public BColor SelectedHeaderBackground { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BFontStyle Font { get; set; } = StandardControlPaint.Theme.FontBody;

    private BFontStyle _themeFont = StandardControlPaint.Theme.FontBody;

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

            if (index == SelectedIndex)
            {
                if (double.IsFinite(contentRect.Width) && Math.Abs(content.Bounds.Width - contentRect.Width) > 0.001 && Math.Abs(content.DesiredSize.Width - contentRect.Width) > 0.001)
                {
                    content.Measure(contentRect.Size);
                }
                content.Arrange(contentRect);
            }
            else
            {
                content.Arrange(BRect.Empty);
            }
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRect content = new(Bounds.Left, Bounds.Top + EffectiveHeaderHeight, Bounds.Width, Math.Max(0, Bounds.Height - EffectiveHeaderHeight));
        StandardControlPaint.FillRounded(context.RenderList, content, Background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, content, BorderColor, CornerRadius, 1);

        double x = Bounds.Left;
        for (int index = 0; index < Tabs.Count; index++)
        {
            BRect header = GetHeaderBounds(index, x);
            x = header.Right;
            bool selected = index == SelectedIndex;
            BColor headerBackground = selected ? SelectedHeaderBackground : HeaderBackground;
            if (!headerBackground.IsEmpty && headerBackground.A > 0)
                StandardControlPaint.FillRounded(context.RenderList, header, headerBackground, CornerRadius);

            BColor headerForeground = selected ? StandardControlPaint.Accent : Foreground;
            context.RenderList.DrawText(new BTextRun(Tabs[index].Header, Font, headerForeground), new BPoint(header.Left + HeaderPaddingX, header.Top + Math.Max(0, (EffectiveHeaderHeight - BTextMeasurer.GetLineHeight(Font)) / 2)));
        }

        SelectedTab?.Content?.Render(context);
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
