using System;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.RadioButton.Standard;

public sealed class StandardRadioButton : UiRadioButton, IStandardThemedControl
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
    private BColor _background = BColor.Transparent;
    private BColor _foreground = StandardControlPaint.Text;
    private BColor _borderColor = StandardControlPaint.BorderStrong;
    private BColor _accent = StandardControlPaint.Accent;
    private BColor _disabledForeground = StandardControlPaint.TextDisabled;
    private BColor _focusRing = StandardControlPaint.Focus;
    private BFontStyle _font = StandardControlPaint.Theme.FontBody;
    private BFontStyle _themeFont = StandardControlPaint.Theme.FontBody;
    private double _markSize = 18;
    private double _spacing = 8;
    private double _paddingX = 6;
    private double _paddingY = 6;

    public BColor Background
    {
        get => _background;
        set
        {
            if (_background == value) return;
            _background = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor Foreground
    {
        get => _foreground;
        set
        {
            if (_foreground == value) return;
            _foreground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor == value) return;
            _borderColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor Accent
    {
        get => _accent;
        set
        {
            if (_accent == value) return;
            _accent = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor DisabledForeground
    {
        get => _disabledForeground;
        set
        {
            if (_disabledForeground == value) return;
            _disabledForeground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor FocusRing
    {
        get => _focusRing;
        set
        {
            if (_focusRing == value) return;
            _focusRing = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BFontStyle Font
    {
        get => _font;
        set
        {
            if (_font == value) return;
            _font = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double MarkSize
    {
        get => _markSize;
        set
        {
            if (_markSize == value) return;
            _markSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double Spacing
    {
        get => _spacing;
        set
        {
            if (_spacing == value) return;
            _spacing = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double PaddingX
    {
        get => _paddingX;
        set
        {
            if (_paddingX == value) return;
            _paddingX = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double PaddingY
    {
        get => _paddingY;
        set
        {
            if (_paddingY == value) return;
            _paddingY = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public bool IsPressed => _isPressed;

    protected override BSize MeasureCore(BSize availableSize)
    {
        double textWidth = string.IsNullOrEmpty(Text) ? 0 : BTextMeasurer.MeasureAdvance(Text, Font);
        double lineHeight = BTextMeasurer.GetLineHeight(Font);
        double width = Math.Max(PreferredSize.Width, PaddingX * 2 + MarkSize + (textWidth > 0 ? Spacing + textWidth : 0));
        double height = Math.Max(PreferredSize.Height, PaddingY * 2 + Math.Max(MarkSize, lineHeight));
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRect mark = GetMarkRect();
        BColor foreground = IsEnabled ? Foreground : DisabledForeground;
        BColor border = IsEnabled ? BorderColor : DisabledForeground;

        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        StandardControlPaint.FillRounded(context.RenderList, mark, StandardControlPaint.Surface, StandardControlPaint.PillRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, mark, border, StandardControlPaint.PillRadius, 1);
        if (IsChecked)
        {
            double inset = Math.Max(3, MarkSize * 0.28);
            StandardControlPaint.FillRounded(
                context.RenderList,
                new BRect(mark.Left + inset, mark.Top + inset, Math.Max(1, mark.Width - inset * 2), Math.Max(1, mark.Height - inset * 2)),
                Accent,
                StandardControlPaint.PillRadius);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            BSize textSize = BTextMeasurer.Measure(Text, Font).Size;
            double x = FlowDirection == UiFlowDirection.RightToLeft
                ? mark.Left - Spacing - textSize.Width
                : mark.Right + Spacing;
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
            bool shouldSelect = _isPressed && Bounds.Contains(input.Position);
            _isPressed = false;
            Session?.ReleaseInputCapture(this);
            Invalidate(UiInvalidationKind.Render);
            if (shouldSelect)
                Select();
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Space, "Space") || IsKey(input, BVirtualKey.Enter, "Enter"))
        {
            Session?.SetFocus(this);
            Select();
            return true;
        }

        return false;
    }

    private BRect GetMarkRect()
    {
        double x = FlowDirection == UiFlowDirection.RightToLeft
            ? Bounds.Right - PaddingX - MarkSize
            : Bounds.Left + PaddingX;
        double y = Bounds.Top + Math.Max(0, (Bounds.Height - MarkSize) / 2);
        return new BRect(x, y, MarkSize, MarkSize);
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
