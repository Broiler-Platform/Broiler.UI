using System;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.Button.Standard;

public sealed class StandardButton : UiButton, IStandardThemedControl
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        Foreground = theme.Text;
        BorderColor = theme.Border;
        DisabledForeground = theme.TextDisabled;
        PressedBackground = theme.AccentPressed;
        HoverBackground = theme.AccentHover;
        FocusRing = theme.FocusRing;
        PrimaryBackground = theme.Accent;
        PrimaryForeground = theme.OnAccent;
        SecondaryHoverBackground = theme.AccentSoft;
        SecondaryPressedBackground = theme.SurfaceDisabled;
    }

    private bool _isPressed;
    private bool _isHovering;

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor DisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor PressedBackground { get; set; } = StandardControlPaint.AccentPressed;

    public BColor HoverBackground { get; set; } = StandardControlPaint.AccentHover;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BColor PrimaryBackground { get; set; } = StandardControlPaint.Accent;

    public BColor PrimaryForeground { get; set; } = BColor.White;

    public BColor SecondaryHoverBackground { get; set; } = StandardControlPaint.AccentSoft;

    public BColor SecondaryPressedBackground { get; set; } = StandardControlPaint.SurfaceDisabled;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    public double PaddingX { get; set; } = 14;

    public double PaddingY { get; set; } = 7;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public StandardCommand? Command { get; set; }

    public StandardCommandDispatcher? CommandDispatcher { get; set; }

    public bool IsPressed => _isPressed;

    /// <summary>
    /// Draws the button's icon into the square box it is given, in the colour the button has
    /// already resolved for its current state - so hover, pressed and disabled recolour the icon
    /// without it knowing anything about them.
    /// </summary>
    /// <remarks>
    /// A delegate rather than an icon type, because Broiler.UI has no opinion about what an icon
    /// is: the toolkit owns where the icon goes and what colour it is, and the application owns
    /// what it looks like. When this is null the control behaves exactly as it did before icons
    /// existed. <see cref="UiButton.Text"/> is still the accessible name when an icon replaces it
    /// on screen, which is what keeps an icon-only bar reachable by screen reader and by name.
    /// </remarks>
    public Action<BRenderList, BRect, BColor>? IconPainter { get; set; }

    /// <summary>The side of the square box <see cref="IconPainter"/> draws into.</summary>
    public double IconExtent { get; set; } = 16;

    protected override BSize MeasureCore(BSize availableSize)
    {
        // An icon button is sized by its icon, not by the caption it still carries as its name.
        BSize content = IconPainter is null
            ? BTextMeasurer.Measure(Text, Font).Size
            : new BSize(IconExtent, IconExtent);
        double width = Math.Max(PreferredSize.Width, content.Width + (PaddingX * 2));
        double height = Math.Max(PreferredSize.Height, content.Height + (PaddingY * 2));
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BColor background = ResolveBackground();
        BColor foreground = ResolveForeground();
        BColor border = IsDefault && IsEnabled ? background : BorderColor;
        StandardControlPaint.FillRounded(context.RenderList, Bounds, background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, border, CornerRadius, 1);

        if (IconPainter is { } painter)
        {
            // The icon replaces the caption rather than joining it: a bar of icon buttons that
            // also drew their text would be neither.
            BRect box = IconBox();
            if (!box.IsEmpty)
                painter(context.RenderList, box, foreground);
        }
        else if (!string.IsNullOrEmpty(Text))
        {
            BSize textSize = BTextMeasurer.Measure(Text, Font).Size;
            double x = Bounds.Left + Math.Max(0, (Bounds.Width - textSize.Width) / 2);
            double y = Bounds.Top + Math.Max(0, (Bounds.Height - textSize.Height) / 2);

            // A label wider than the button it names is cut off at the button's own edge rather
            // than painted out across its neighbours — a button in a resizable dialog can end up
            // narrower than the caption it was sized for.
            context.RenderList.PushClip(Bounds);
            context.RenderList.DrawText(new BTextRun(Text, Font, foreground), new BPoint(x, y));
            context.RenderList.PopClip();
        }

        // Only under keyboard navigation: a bar whose buttons ring themselves on click reads as
        // a page of boxes, and the user already knows which one they clicked.
        if (Session?.FocusedElement == this && Session.IsFocusVisible)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, Math.Max(0, CornerRadius - 2), 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        switch (input.Kind)
        {
            case UiInputEventKind.PointerMove:
                _isHovering = Bounds.Contains(input.Position);
                Invalidate(UiInvalidationKind.Render);
                return _isPressed;

            case UiInputEventKind.PointerButton:
                return HandlePointerButton(input);

            case UiInputEventKind.KeyboardKey:
                return HandleKeyboard(input);

            default:
                return false;
        }
    }

    protected override bool OnClicking(UiButtonActivationReason reason)
    {
        if (Command is not null)
            return Command.TryExecute();

        if (!string.IsNullOrWhiteSpace(CommandName) && CommandDispatcher is not null)
            return CommandDispatcher.TryExecute(CommandName);

        return true;
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
            _isHovering = true;
            Invalidate(UiInvalidationKind.Render);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            bool shouldClick = _isPressed && Bounds.Contains(input.Position);
            _isPressed = false;
            _isHovering = Bounds.Contains(input.Position);
            Session?.ReleaseInputCapture(this);
            Invalidate(UiInvalidationKind.Render);
            if (shouldClick)
                Click(UiButtonActivationReason.Pointer);
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down && input.KeyTransition != KeyboardKeyTransition.Up)
            return false;

        bool isEnter = IsKey(input, 0x0D, "Enter");
        bool isSpace = IsKey(input, 0x20, "Space");
        bool isEscape = IsKey(input, 0x1B, "Escape");

        if (input.KeyTransition == KeyboardKeyTransition.Down)
        {
            if (isSpace)
            {
                _isPressed = true;
                Invalidate(UiInvalidationKind.Render);
                return true;
            }

            if (isEnter || (IsDefault && isEnter) || (IsCancel && isEscape))
            {
                Click(UiButtonActivationReason.Keyboard);
                return true;
            }
        }

        if (input.KeyTransition == KeyboardKeyTransition.Up && isSpace)
        {
            bool wasPressed = _isPressed;
            _isPressed = false;
            Invalidate(UiInvalidationKind.Render);
            if (wasPressed)
                Click(UiButtonActivationReason.Keyboard);
            return true;
        }

        return false;
    }

    private BColor ResolveBackground()
    {
        if (!IsEnabled)
            return StandardControlPaint.SurfaceDisabled;
        if (IsDefault)
        {
            if (_isPressed)
                return PressedBackground;
            if (_isHovering)
                return HoverBackground;
            return PrimaryBackground;
        }

        if (_isPressed)
            return SecondaryPressedBackground;
        if (_isHovering)
            return SecondaryHoverBackground;
        return Background;
    }

    private BColor ResolveForeground()
    {
        if (!IsEnabled)
            return DisabledForeground;

        return IsDefault ? PrimaryForeground : Foreground;
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    /// <summary>
    /// The square the icon is drawn in: centred, and never larger than the control it sits in.
    /// </summary>
    private BRect IconBox()
    {
        double extent = Math.Min(IconExtent, Math.Min(Bounds.Width, Bounds.Height));
        if (!(extent > 0))
            return BRect.Empty;

        return new BRect(
            Bounds.Left + ((Bounds.Width - extent) / 2),
            Bounds.Top + ((Bounds.Height - extent) / 2),
            extent,
            extent);
    }
}
