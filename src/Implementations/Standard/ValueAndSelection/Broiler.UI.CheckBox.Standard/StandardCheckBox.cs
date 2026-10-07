using System;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.CheckBox.Standard;

public sealed class StandardCheckBox : UiCheckBox, IStandardThemedControl
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
        Foreground = theme.Text;
        BorderColor = theme.BorderStrong;
        Accent = theme.Accent;
        DisabledForeground = theme.TextDisabled;
        FocusRing = theme.FocusRing;
    }

    private bool _isPressed;

    public BColor Background { get; set; } = BColor.Transparent;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.BorderStrong;

    public BColor Accent { get; set; } = StandardControlPaint.Accent;

    public BColor DisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BFontStyle Font { get; set; } = StandardControlPaint.Theme.FontBody;
    private BFontStyle _themeFont = StandardControlPaint.Theme.FontBody;

    public double BoxSize { get; set; } = 18;

    public double Spacing { get; set; } = 8;

    public double PaddingX { get; set; } = 6;

    public double PaddingY { get; set; } = 6;

    public double CornerRadius { get; set; } = StandardControlPaint.SmallRadius;

    public bool IsPressed => _isPressed;

    protected override BSize MeasureCore(BSize availableSize)
    {
        double textWidth = string.IsNullOrEmpty(Text) ? 0 : BTextMeasurer.MeasureAdvance(Text, Font);
        double lineHeight = BTextMeasurer.GetLineHeight(Font);
        double width = Math.Max(PreferredSize.Width, PaddingX * 2 + BoxSize + (textWidth > 0 ? Spacing + textWidth : 0));
        double height = Math.Max(PreferredSize.Height, PaddingY * 2 + Math.Max(BoxSize, lineHeight));
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRect box = GetBoxRect();
        BColor foreground = IsEnabled ? Foreground : DisabledForeground;
        BColor border = IsEnabled ? BorderColor : DisabledForeground;
        // A checked or indeterminate box is filled, and its white mark drawn over the fill: in the accent,
        // or in the disabled colour while disabled. A disabled box was filled with the surface, which its
        // white mark vanished into, so a disabled checked box looked unchecked.
        bool marked = CheckState != UiCheckState.Unchecked;
        BColor markFill = IsEnabled ? Accent : DisabledForeground;

        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        StandardControlPaint.FillRounded(context.RenderList, box, marked ? markFill : StandardControlPaint.Surface, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, box, marked ? markFill : border, CornerRadius, 1);

        if (CheckState == UiCheckState.Checked)
            DrawCenteredText(context, box, "\u2713", BColor.White);
        else if (CheckState == UiCheckState.Indeterminate)
            DrawCenteredText(context, box, "-", BColor.White);

        if (!string.IsNullOrEmpty(Text))
        {
            BSize textSize = BTextMeasurer.Measure(Text, Font).Size;
            double x = FlowDirection == UiFlowDirection.RightToLeft
                ? box.Left - Spacing - textSize.Width
                : box.Right + Spacing;
            double y = Bounds.Top + Math.Max(0, (Bounds.Height - textSize.Height) / 2);
            context.RenderList.DrawText(new BTextRun(Text, Font, foreground), new BPoint(x, y));
        }

        if (Session?.FocusedElement == this)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, StandardControlPaint.ControlRadius, 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            Session?.CaptureInput(this);
            _isPressed = true;
            Invalidate(UiInvalidationKind.Render);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            bool shouldToggle = _isPressed && Bounds.Contains(input.Position);
            _isPressed = false;
            Session?.ReleaseInputCapture(this);
            Invalidate(UiInvalidationKind.Render);
            if (shouldToggle)
                Toggle();
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Space, "Space"))
        {
            Session?.SetFocus(this);
            Toggle();
            return true;
        }

        return false;
    }

    private BRect GetBoxRect()
    {
        double x = FlowDirection == UiFlowDirection.RightToLeft
            ? Bounds.Right - PaddingX - BoxSize
            : Bounds.Left + PaddingX;
        double y = Bounds.Top + Math.Max(0, (Bounds.Height - BoxSize) / 2);
        return new BRect(x, y, BoxSize, BoxSize);
    }

    private void DrawCenteredText(UiRenderContext context, BRect box, string text, BColor color)
    {
        BSize size = BTextMeasurer.Measure(text, Font).Size;
        context.RenderList.DrawText(new BTextRun(text, Font, color), new BPoint(box.Left + Math.Max(0, (box.Width - size.Width) / 2), box.Top + Math.Max(0, (box.Height - size.Height) / 2)));
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
