using System;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button;
using Broiler.UI.Standard;

namespace Broiler.UI.ToggleButton.Standard;

public sealed class StandardToggleButton : UiToggleButton, IStandardThemedControl
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
        CheckedBackground = theme.StateFill;
        IndeterminateBackground = theme.StateFill;
        // The label is accent text on the surface and the hover fill, not the accent fill.
        Foreground = theme.AccentText;
        BorderColor = theme.Border;
        DisabledForeground = theme.TextDisabled;
        HoverBackground = theme.SurfaceAlt;
        PressedBackground = theme.StateFill;
        FocusRing = theme.FocusRing;

        BColor? checkedForeground = CheckedLabel(theme, theme.AccentText);
        if (_checkedForeground != checkedForeground)
        {
            _checkedForeground = checkedForeground;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    private bool _isPressed;
    private bool _isHovering;
    private BColor _background = StandardControlPaint.Surface;
    private BColor _checkedBackground = StandardControlPaint.StateFill;
    private BColor? _checkedForeground = CheckedLabel(StandardControlPaint.Theme, StandardControlPaint.Accent);
    private BColor _indeterminateBackground = UnthemedStateFill(BColor.FromArgb(0xFF, 0xF0, 0xF5, 0xFF));
    // Unthemed, the label stays the accent: the fixed hover and pressed fills below are light colors chosen for it.
    private BColor _foreground = StandardControlPaint.Accent;
    private BColor _borderColor = BColor.FromArgb(0xFF, 0x9B, 0xBA, 0xE0);
    private BColor _disabledForeground = StandardControlPaint.TextDisabled;
    private BColor _pressedBackground = UnthemedStateFill(BColor.FromArgb(0xFF, 0xD8, 0xE8, 0xFC));
    private BColor _hoverBackground = BColor.FromArgb(0xFF, 0xF2, 0xF7, 0xFF);
    private BColor _focusRing = StandardControlPaint.Focus;
    private BFontStyle _font = StandardControlPaint.Theme.FontBody;
    private BFontStyle _themeFont = StandardControlPaint.Theme.FontBody;
    private double _paddingX = 14;
    private double _paddingY = 7;
    private double _cornerRadius = StandardControlPaint.ControlRadius;
    private Action<BRenderList, BRect, BColor>? _iconPainter;
    private double _iconExtent = 16;

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

    public BColor CheckedBackground
    {
        get => _checkedBackground;
        set
        {
            if (_checkedBackground == value) return;
            _checkedBackground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor IndeterminateBackground
    {
        get => _indeterminateBackground;
        set
        {
            if (_indeterminateBackground == value) return;
            _indeterminateBackground = value;
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

    /// <summary>
    /// The color of the label and icon on the checked fill, and on the two fills drawn in its place: the
    /// indeterminate fill and the pressed fill. Until it is set it is <see cref="Foreground"/>.
    /// <see cref="ApplyTheme"/> sets it to the theme's <see cref="StandardThemeTokens.StateText"/> when that
    /// differs from the theme's text color, or when the theme's accent text, the color <see cref="Foreground"/>
    /// takes, is the state fill itself; otherwise it lets it follow <see cref="Foreground"/> again. A toggle button that
    /// is never themed takes it from the shared palette in the same way, as it takes its checked fill.
    /// </summary>
    public BColor CheckedForeground
    {
        get => _checkedForeground ?? _foreground;
        set
        {
            if (_checkedForeground == value) return;
            _checkedForeground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    /// <summary>
    /// The label a theme gives the state fills: its state text, or null to follow <see cref="Foreground"/>, the
    /// <paramref name="label"/> the button draws off (the theme's accent text, or the accent while unthemed),
    /// while the state text is the theme's text color. The label is replaced all the same when it is the state
    /// fill's own color, as in a system palette that maps the accent and the selection fill to its highlight: it
    /// would not show at all.
    /// </summary>
    private static BColor? CheckedLabel(StandardThemeTokens theme, BColor label) =>
        theme.StateText != theme.Text || label == theme.StateFill ? theme.StateText : null;

    /// <summary>
    /// The pressed or indeterminate fill of a toggle button that is never themed: a fixed color chosen for the
    /// default palette, unless the shared palette gives the state fills a label of their own. That label is
    /// chosen to be read on the state fill, so the fill comes with it.
    /// </summary>
    private static BColor UnthemedStateFill(BColor fixedFill) =>
        CheckedLabel(StandardControlPaint.Theme, StandardControlPaint.Accent) is null ? fixedFill : StandardControlPaint.StateFill;

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

    public BColor PressedBackground
    {
        get => _pressedBackground;
        set
        {
            if (_pressedBackground == value) return;
            _pressedBackground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor HoverBackground
    {
        get => _hoverBackground;
        set
        {
            if (_hoverBackground == value) return;
            _hoverBackground = value;
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

    public double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (_cornerRadius == value) return;
            _cornerRadius = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public StandardCommandDispatcher? CommandDispatcher { get; set; }

    public bool IsPressed => _isPressed;

    /// <summary>
    /// Draws the button's icon into the square box it is given, in the colour the button has
    /// already resolved for its current state - so hover, pressed, checked and disabled recolour
    /// the icon without it knowing anything about them. Null leaves the control drawing its
    /// caption, exactly as it did before icons existed.
    /// </summary>
    public Action<BRenderList, BRect, BColor>? IconPainter
    {
        get => _iconPainter;
        set
        {
            if (ReferenceEquals(_iconPainter, value)) return;
            _iconPainter = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>The side of the square box <see cref="IconPainter"/> draws into.</summary>
    public double IconExtent
    {
        get => _iconExtent;
        set
        {
            if (_iconExtent == value) return;
            _iconExtent = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
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
        StandardControlPaint.FillRounded(context.RenderList, Bounds, background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, IsDefault ? FocusRing : BorderColor, CornerRadius, IsDefault ? 2 : 1);

        if (IconPainter is { } painter)
        {
            BRect box = IconBox();
            if (!box.IsEmpty)
                painter(context.RenderList, box, foreground);
        }
        else
        {
            string display = string.IsNullOrEmpty(Text) ? ToggleState.ToString() : Text;
            BSize textSize = BTextMeasurer.Measure(display, Font).Size;
            double x = Bounds.Left + Math.Max(0, (Bounds.Width - textSize.Width) / 2);
            double y = Bounds.Top + Math.Max(0, (Bounds.Height - textSize.Height) / 2);
            context.RenderList.DrawText(new BTextRun(display, Font, foreground), new BPoint(x, y));
        }

        // Keyboard navigation only - see StandardButton.
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
        if (!string.IsNullOrWhiteSpace(CommandName) && CommandDispatcher is not null && !CommandDispatcher.TryExecute(CommandName))
            return false;

        return base.OnClicking(reason);
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

        bool isEnter = IsKey(input, BVirtualKey.Enter, "Enter");
        bool isSpace = IsKey(input, BVirtualKey.Space, "Space");
        bool isEscape = IsKey(input, BVirtualKey.Escape, "Escape");

        if (input.KeyTransition == KeyboardKeyTransition.Down)
        {
            if (isSpace)
            {
                Session?.SetFocus(this);
                _isPressed = true;
                Invalidate(UiInvalidationKind.Render);
                return true;
            }

            if (isEnter || (IsCancel && isEscape))
            {
                Session?.SetFocus(this);
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
        if (_isPressed)
            return PressedBackground;
        BColor toggledBackground = ToggleState switch
        {
            UiToggleState.On => CheckedBackground,
            UiToggleState.Indeterminate => IndeterminateBackground,
            _ => Background,
        };
        return ToggleState == UiToggleState.Off && _isHovering
            ? HoverBackground
            : toggledBackground;
    }

    /// <summary>The label color that goes with the fill <see cref="ResolveBackground"/> draws.</summary>
    private BColor ResolveForeground()
    {
        if (!IsEnabled)
            return DisabledForeground;

        return _isPressed || ToggleState != UiToggleState.Off ? CheckedForeground : Foreground;
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    /// <summary>The square the icon is drawn in: centred, and never larger than the control.</summary>
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
