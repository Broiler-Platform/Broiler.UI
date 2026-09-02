using System;
using Broiler.Graphics;
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
        ArrowHoverBackground = theme.AccentSoft;
        ArrowPressedBackground = theme.SurfaceDisabled;
        DisabledForeground = theme.TextDisabled;
        FocusRing = theme.FocusRing;
        _edit.ApplyTheme(theme);
        ApplyEditChrome();
    }

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor ArrowColor { get; set; } = StandardControlPaint.TextMuted;

    public BColor ArrowHoverBackground { get; set; } = StandardControlPaint.AccentSoft;

    public BColor ArrowPressedBackground { get; set; } = StandardControlPaint.SurfaceDisabled;

    public BColor DisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    public BFontStyle Font
    {
        get => _edit.Font;
        set => _edit.Font = value;
    }

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    /// <summary>How wide the arrow column is. Both arrows share it, one above the other.</summary>
    public double ArrowWidth { get; set; } = 18;

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

        DrawArrow(context, SpinArrow.Up);
        DrawArrow(context, SpinArrow.Down);

        bool focused = Session?.FocusedElement == _edit || Session?.FocusedElement == this;
        StandardControlPaint.StrokeRounded(
            context.RenderList,
            Bounds,
            focused ? FocusRing : BorderColor,
            CornerRadius,
            focused ? 2 : 1);
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

    private void DrawArrow(UiRenderContext context, SpinArrow arrow)
    {
        BRect bounds = GetArrowBounds(arrow);
        if (bounds.IsEmpty)
            return;

        if (IsEnabled && _pressed == arrow)
            context.RenderList.FillRect(bounds, ArrowPressedBackground);
        else if (IsEnabled && _hovered == arrow)
            context.RenderList.FillRect(bounds, ArrowHoverBackground);

        double width = Math.Min(7, Math.Max(4, bounds.Width - 8));
        double height = Math.Min(4, Math.Max(3, bounds.Height / 3));
        double centerX = bounds.Left + (bounds.Width / 2);
        double centerY = bounds.Top + (bounds.Height / 2);
        BColor color = IsEnabled ? ArrowColor : DisabledForeground;

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
