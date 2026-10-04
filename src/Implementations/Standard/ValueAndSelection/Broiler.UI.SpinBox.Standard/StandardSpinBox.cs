using System;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.SpinBox.Standard;

/// <summary>
/// A spin box drawn as one framed field: a text edit on the left, a stacked pair of arrows on the
/// right.
/// </summary>
/// <remarks>
/// <para>
/// The text field is a real <see cref="StandardEdit"/> child rather than a second text
/// implementation, so a spin box gets marking, the clipboard, an IME and the context menu for
/// free. It is stripped of its own frame and focus ring — the box draws one frame around both
/// halves, because two nested rounded rectangles is not what a spin box looks like anywhere.
/// </para>
/// <para>
/// Holding an arrow does not repeat. Auto-repeat needs a clock the framework only ticks when a host
/// runs <see cref="StandardAnimationScheduler"/>, and a control whose behaviour depends on whether
/// the host happens to have wired one up is worse than one that always steps once. Up/Down on the
/// keyboard repeat on their own, and the wheel is faster than either.
/// </para>
/// </remarks>
public sealed class StandardSpinBox : UiSpinBox, IStandardThemedControl
{
    private readonly StandardEdit _edit;
    private SpinArrow _hovered;
    private SpinArrow _pressed;
    private bool _syncing;

    public StandardSpinBox()
    {
        _edit = new StandardEdit
        {
            PaddingX = 6,
            PaddingY = 4,
            CornerRadius = 0,
            MaxLength = 16,
        };
        _edit.TextChanged += (_, _) => CommitEditedText();
        _edit.Submitted += (_, _) => CommitAndNormalize();
        ApplyEditChrome();
        AddChild(_edit);
        SyncEditText();
    }

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        Foreground = theme.Text;
        BorderColor = theme.Border;
        ArrowColor = theme.TextMuted;
        ArrowHoverBackground = theme.StateFill;
        ArrowPressedBackground = theme.SurfaceDisabled;
        DisabledForeground = theme.TextDisabled;
        FocusRing = theme.FocusRing;

        BColor? arrowHoverColor = ArrowHoverLabel(theme);
        if (_arrowHoverColor != arrowHoverColor)
        {
            _arrowHoverColor = arrowHoverColor;
            Invalidate(UiInvalidationKind.Render);
        }

        _edit.ApplyTheme(theme);
        ApplyEditChrome();
    }

    private BColor _background = StandardControlPaint.Surface;
    private BColor _foreground = StandardControlPaint.Text;
    private BColor _borderColor = StandardControlPaint.Border;
    private BColor _arrowColor = StandardControlPaint.TextMuted;
    private BColor _arrowHoverBackground = StandardControlPaint.StateFill;
    private BColor? _arrowHoverColor = ArrowHoverLabel(StandardControlPaint.Theme);
    private BColor _arrowPressedBackground = StandardControlPaint.SurfaceDisabled;
    private BColor _disabledForeground = StandardControlPaint.TextDisabled;
    private BColor _focusRing = StandardControlPaint.Focus;
    private double _cornerRadius = StandardControlPaint.ControlRadius;
    private double _arrowWidth = 18;

    // The frame's width while the box has focus, when it is the focus ring.
    private const double FocusedFrameThickness = 2;

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

    public BColor ArrowColor
    {
        get => _arrowColor;
        set
        {
            if (_arrowColor == value) return;
            _arrowColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor ArrowHoverBackground
    {
        get => _arrowHoverBackground;
        set
        {
            if (_arrowHoverBackground == value) return;
            _arrowHoverBackground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    /// <summary>
    /// The color of a hovered arrow, drawn on <see cref="ArrowHoverBackground"/>. Until it is set it is
    /// <see cref="ArrowColor"/>. <see cref="ApplyTheme"/> sets it to the theme's
    /// <see cref="StandardThemeTokens.StateText"/> when that differs from the theme's text color, or when the
    /// theme's muted text, the color <see cref="ArrowColor"/> takes, is the state fill itself; otherwise it lets it
    /// follow <see cref="ArrowColor"/> again. A spin box that is never themed takes it from the shared palette in
    /// the same way, as it takes its hover fill.
    /// </summary>
    public BColor ArrowHoverColor
    {
        get => _arrowHoverColor ?? _arrowColor;
        set
        {
            if (_arrowHoverColor == value) return;
            _arrowHoverColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    /// <summary>
    /// The hovered arrow color a theme gives: its state text, or null to follow <see cref="ArrowColor"/>, the
    /// theme's muted text, while the state text is the theme's text color. The muted text is replaced all the
    /// same when it is the state fill's own color: the arrow would not show at all.
    /// </summary>
    private static BColor? ArrowHoverLabel(StandardThemeTokens theme) =>
        theme.StateText != theme.Text || theme.TextMuted == theme.StateFill ? theme.StateText : null;

    public BColor ArrowPressedBackground
    {
        get => _arrowPressedBackground;
        set
        {
            if (_arrowPressedBackground == value) return;
            _arrowPressedBackground = value;
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

    /// <summary>
    /// The color of the frame, 2 DIP wide, while the box has focus. Where it does not stand out from the field's fill
    /// (3:1), the frame is drawn in the field's text color instead (<see cref="StandardControlPaint.FocusRingColor"/>),
    /// and a hovered or pressed arrow whose fill it does not stand out from is filled inside it rather than up to it.
    /// </summary>
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
        get => _edit.Font;
        set => _edit.Font = value;
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

    /// <summary>How wide the arrow column is. Both arrows share it, one above the other.</summary>
    public double ArrowWidth
    {
        get => _arrowWidth;
        set
        {
            if (_arrowWidth == value) return;
            _arrowWidth = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>The text half, exposed so a dialog can reach its selection and placeholder.</summary>
    public StandardEdit Edit => _edit;

    /// <summary>Where the up arrow is, after arrangement.</summary>
    public BRect UpArrowBounds => GetArrowBounds(SpinArrow.Up);

    /// <summary>Where the down arrow is, after arrangement.</summary>
    public BRect DownArrowBounds => GetArrowBounds(SpinArrow.Down);

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize editAvailable = new(
            double.IsInfinity(availableSize.Width) ? availableSize.Width : Math.Max(0, availableSize.Width - ArrowWidth),
            availableSize.Height);
        BSize edit = _edit.Measure(editAvailable);
        return new BSize(
            ClampDesired(Math.Max(PreferredSize.Width, edit.Width + ArrowWidth), availableSize.Width),
            ClampDesired(Math.Max(PreferredSize.Height, edit.Height), availableSize.Height));
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        double arrows = Math.Min(ArrowWidth, finalRect.Width);
        _edit.Arrange(new BRect(
            finalRect.Left,
            finalRect.Top,
            Math.Max(0, finalRect.Width - arrows),
            finalRect.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BColor background = IsEnabled ? Background : StandardControlPaint.SurfaceDisabled;
        StandardControlPaint.FillRounded(context.RenderList, Bounds, background, CornerRadius);

        // The edit paints over this, so it has to know the fill it is sitting on.
        _edit.Background = background;
        base.RenderCore(context);

        // While focused the frame is the focus ring. It is drawn on the field, so it takes the field's text color
        // where its own would not show there (ADR 0032).
        bool focused = Session?.FocusedElement == _edit || Session?.FocusedElement == this;
        BColor ring = StandardControlPaint.FocusRingColor(FocusRing, background, _edit.Foreground);

        DrawArrow(context, SpinArrow.Up, focused ? ring : null);
        DrawArrow(context, SpinArrow.Down, focused ? ring : null);

        StandardControlPaint.StrokeRounded(
            context.RenderList,
            Bounds,
            focused ? ring : BorderColor,
            CornerRadius,
            focused ? FocusedFrameThickness : 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerMove => HandlePointerMove(input),
            UiInputEventKind.PointerWheel => HandlePointerWheel(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    protected override void OnEnabledChanged() => _edit.IsEnabled = IsEnabled;

    protected override void OnValueChanged() => SyncEditText();

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.SpinBox,
            ValueText,
            Bounds,
            CreateSemanticState(),
            []);

    /// <summary>
    /// The text half draws no frame of its own: the box draws one around both halves. Re-applied
    /// after a theme change, which resets the edit's own chrome colors.
    /// </summary>
    private void ApplyEditChrome()
    {
        _edit.BorderColor = BColor.Empty;
        _edit.FocusRing = BColor.Empty;
        _edit.Background = Background;
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            if (_pressed == SpinArrow.None)
                return false;

            _pressed = SpinArrow.None;
            Invalidate(UiInvalidationKind.Render);
            return true;
        }

        if (input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        SpinArrow arrow = ArrowAt(input.Position);
        if (arrow == SpinArrow.None)
            return false;

        // Focus goes to the text half, so that what the user types next lands in the field they can
        // see a caret in — pressing an arrow is still working on the number.
        Session?.SetFocus(_edit);
        _pressed = arrow;
        Step(arrow);
        Invalidate(UiInvalidationKind.Render);
        return true;
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        SpinArrow arrow = ArrowAt(input.Position);
        if (arrow == _hovered)
            return false;

        _hovered = arrow;
        Invalidate(UiInvalidationKind.Render);
        return false;
    }

    private bool HandlePointerWheel(UiInputEvent input)
    {
        if (input.WheelAxis != MouseWheelAxis.Vertical || input.WheelDeltaNotches == 0 || !Bounds.Contains(input.Position))
            return false;

        return input.WheelDeltaNotches > 0 ? StepUp() : StepDown();
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.Up, "Up"))
            return StepUp();
        if (IsKey(input, BVirtualKey.Down, "Down"))
            return StepDown();
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
            return PageUp();
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
            return PageDown();

        return false;
    }

    private void Step(SpinArrow arrow)
    {
        if (arrow == SpinArrow.Up)
            StepUp();
        else if (arrow == SpinArrow.Down)
            StepDown();
    }

    /// <summary>
    /// Takes what the user has typed so far without writing it back. Re-formatting mid-word would
    /// fight the caret: in a box that starts at 8, typing "1" towards "12" would clamp to 8 and put
    /// the 8 back under the caret.
    /// </summary>
    private void CommitEditedText()
    {
        if (_syncing)
            return;

        _syncing = true;
        try
        {
            TryCommitText(_edit.Text);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Commits and then shows the number the box settled on. Enter is where that belongs.</summary>
    private void CommitAndNormalize()
    {
        CommitEditedText();
        SyncEditText();
    }

    private void SyncEditText()
    {
        if (_syncing)
            return;

        _syncing = true;
        try
        {
            _edit.Text = ValueText;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// Draws an arrow, on its pressed or hover fill when it has one. <paramref name="ring"/> is the color the frame
    /// is drawn in while the box has focus, or null while it has none.
    /// </summary>
    private void DrawArrow(UiRenderContext context, SpinArrow arrow, BColor? ring)
    {
        BRect bounds = GetArrowBounds(arrow);
        if (bounds.IsEmpty)
            return;

        BColor color = IsEnabled ? ArrowColor : DisabledForeground;
        if (IsEnabled && _pressed == arrow)
        {
            FillArrow(context, bounds, ArrowPressedBackground, ring);
        }
        else if (IsEnabled && _hovered == arrow)
        {
            FillArrow(context, bounds, ArrowHoverBackground, ring);
            color = ArrowHoverColor;
        }

        double width = Math.Min(7, Math.Max(4, bounds.Width - 8));
        double height = Math.Min(4, Math.Max(3, bounds.Height / 3));
        double centerX = bounds.Left + (bounds.Width / 2);
        double centerY = bounds.Top + (bounds.Height / 2);

        if (arrow == SpinArrow.Up)
        {
            context.RenderList.FillTriangle(
                new BPoint(centerX, centerY - (height / 2)),
                new BPoint(centerX + (width / 2), centerY + (height / 2)),
                new BPoint(centerX - (width / 2), centerY + (height / 2)),
                color);
        }
        else
        {
            context.RenderList.FillTriangle(
                new BPoint(centerX, centerY + (height / 2)),
                new BPoint(centerX - (width / 2), centerY - (height / 2)),
                new BPoint(centerX + (width / 2), centerY - (height / 2)),
                color);
        }
    }

    /// <summary>
    /// Fills an arrow's cell, which meets the frame. While the box has focus the frame is the focus ring, and a fill
    /// the ring does not stand out from (3:1) would swallow it along that edge, as when a palette built from a system
    /// highlight pair uses the highlight for both. That fill is drawn inside the ring instead: inset by the ring's
    /// width and rounded with the frame, so the field shows between them.
    /// </summary>
    private void FillArrow(UiRenderContext context, BRect bounds, BColor fill, BColor? ring)
    {
        if (ring is { } color && fill.A == 255 && StandardContrast.Ratio(color, fill) < StandardContrast.AaLargeOrUi)
        {
            BRect inside = StandardControlPaint.Inset(bounds, FocusedFrameThickness);
            StandardControlPaint.FillRounded(context.RenderList, inside, fill, Math.Max(0, CornerRadius - FocusedFrameThickness));
            return;
        }

        context.RenderList.FillRect(bounds, fill);
    }

    private BRect GetArrowBounds(SpinArrow arrow)
    {
        if (Bounds.IsEmpty || arrow == SpinArrow.None)
            return BRect.Empty;

        double width = Math.Min(ArrowWidth, Bounds.Width);
        double left = Bounds.Right - width;
        double half = Bounds.Height / 2;
        return arrow == SpinArrow.Up
            ? new BRect(left, Bounds.Top, width, half)
            : new BRect(left, Bounds.Top + half, width, Bounds.Height - half);
    }

    private SpinArrow ArrowAt(BPoint position)
    {
        if (GetArrowBounds(SpinArrow.Up).Contains(position))
            return SpinArrow.Up;
        if (GetArrowBounds(SpinArrow.Down).Contains(position))
            return SpinArrow.Down;

        return SpinArrow.None;
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private enum SpinArrow
    {
        None = 0,
        Up,
        Down,
    }
}
