using System;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Touch;
using Broiler.UI.Standard;

namespace Broiler.UI.ScrollView.Standard;

public sealed class StandardScrollView : UiScrollView, IStandardThemedControl
{
    private StandardThemeTokens _theme = StandardControlPaint.Theme;
    private bool _focusWhenScrollable;
    private BSize _contentDesiredExtent;
    private BRect _verticalTrackBounds = BRect.Empty;
    private BRect _horizontalTrackBounds = BRect.Empty;
    private BRect _scrollbarCornerBounds = BRect.Empty;
    private double _scrollbarThickness = 12;
    private double _minimumThumbLength = 18;
    private bool _showScrollbars = true;
    private ScrollbarAxis _dragAxis = ScrollbarAxis.None;
    private double _dragPointerOffsetWithinThumb;
    private long? _touchContactId;
    private BPoint _touchStart;
    private BPoint _touchLast;
    private bool _isTouchDragging;

    private const double TouchDragThreshold = 6;

    public BColor Background { get; set; } = BColor.Transparent;

    public BColor ScrollbarTrack { get; set; } = BColor.FromArgb(0x33, 0x94, 0xA3, 0xB8);

    public BColor ScrollbarThumb { get; set; } = BColor.FromArgb(0xAA, 0x7D, 0x8D, 0xA3);

    /// <summary>
    /// The color of the ring drawn around the scroll view while it has focus and is a keyboard stop
    /// (<see cref="CanFocus"/>, through <see cref="UiElement.Focusable"/> or
    /// <see cref="FocusWhenScrollable"/>). Follows the theme's focus ring color; the ring's offset
    /// and thickness come from the theme as well.
    /// </summary>
    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    /// <summary>
    /// Makes the scroll view a keyboard stop of its own while it has something to scroll and nothing
    /// inside it can take focus, as for a read-only status area or message header: then
    /// <see cref="CanFocus"/> is true, Tab reaches it, and the arrow, Page, Home and End keys scroll
    /// it. Off by default, where only <see cref="UiElement.Focusable"/> decides.
    /// </summary>
    /// <remarks>
    /// A scroll view whose content has a control of its own is left to that control, and one with
    /// nothing to scroll would take focus with nothing to do or announce.
    /// </remarks>
    public bool FocusWhenScrollable
    {
        get => _focusWhenScrollable;
        set
        {
            ThrowIfDisposed();
            if (_focusWhenScrollable == value)
                return;

            _focusWhenScrollable = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    public override bool CanFocus => base.CanFocus || (FocusWhenScrollable && IsKeyboardScrollStop());

    /// <summary>
    /// Re-derives the focus ring from <paramref name="theme"/>. The scrollbar colors keep their
    /// values, so a themed session draws scrollbars exactly as before.
    /// </summary>
    public void ApplyTheme(StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _theme = theme;
        FocusRing = theme.FocusRing;
        Invalidate(UiInvalidationKind.Render);
    }

    public double ScrollbarThickness
    {
        get => _scrollbarThickness;
        set
        {
            ThrowIfDisposed();
            ValidateNonNegativeFinite(value, nameof(value));
            if (_scrollbarThickness == value)
                return;

            _scrollbarThickness = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double MinimumThumbLength
    {
        get => _minimumThumbLength;
        set
        {
            ThrowIfDisposed();
            ValidateNonNegativeFinite(value, nameof(value));
            if (_minimumThumbLength == value)
                return;

            _minimumThumbLength = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public bool ShowScrollbars
    {
        get => _showScrollbars;
        set
        {
            ThrowIfDisposed();
            if (_showScrollbars == value)
                return;

            _showScrollbars = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BRect ContentBounds { get; private set; } = BRect.Empty;

    public bool HasVerticalScrollbar { get; private set; }

    public bool HasHorizontalScrollbar { get; private set; }

    public override bool MakeVisible(BRect targetRect)
    {
        ThrowIfDisposed();
        if (targetRect.IsEmpty)
            return false;

        BRect viewport = ContentBounds.IsEmpty ? Bounds : ContentBounds;
        if (viewport.IsEmpty)
            return false;

        double deltaX = 0;
        if (targetRect.Left < viewport.Left)
            deltaX = targetRect.Left - viewport.Left;
        else if (targetRect.Right > viewport.Right)
            deltaX = targetRect.Right - viewport.Right;

        double deltaY = 0;
        if (targetRect.Top < viewport.Top)
            deltaY = targetRect.Top - viewport.Top;
        else if (targetRect.Bottom > viewport.Bottom)
            deltaY = targetRect.Bottom - viewport.Bottom;

        if (deltaX != 0 || deltaY != 0)
        {
            return ScrollBy(deltaX, deltaY);
        }

        return false;
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize outerSize = new(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));

        if (Constraint == UiScrollConstraint.ConstrainWidth && (double.IsFinite(availableSize.Width) || double.IsFinite(PreferredSize.Width)))
        {
            double availableWidth = double.IsFinite(availableSize.Width) ? availableSize.Width : (PreferredSize.Width > 0 ? PreferredSize.Width : 320);
            double thickness = ShowScrollbars ? Math.Min(ScrollbarThickness, Math.Max(0, Math.Min(availableWidth, outerSize.Height))) : 0;

            bool hasVertical = VerticalScrollBarVisibility == UiScrollBarVisibility.Visible;
            double contentWidth = hasVertical ? Math.Max(1, availableWidth - thickness) : Math.Max(1, availableWidth);

            double extentWidth = 0;
            double extentHeight = 0;
            foreach (UiElement child in Children)
            {
                if (child.Visibility == UiVisibility.Collapsed)
                    continue;

                BSize desired = child.Measure(new BSize(contentWidth, double.PositiveInfinity));
                extentWidth = Math.Max(extentWidth, desired.Width);
                extentHeight = Math.Max(extentHeight, desired.Height);
            }

            if (VerticalScrollBarVisibility == UiScrollBarVisibility.Auto && !hasVertical && extentHeight > outerSize.Height && thickness > 0)
            {
                hasVertical = true;
                contentWidth = Math.Max(1, availableWidth - thickness);
                extentWidth = 0;
                extentHeight = 0;
                foreach (UiElement child in Children)
                {
                    if (child.Visibility == UiVisibility.Collapsed)
                        continue;

                    BSize desired = child.Measure(new BSize(contentWidth, double.PositiveInfinity));
                    extentWidth = Math.Max(extentWidth, desired.Width);
                    extentHeight = Math.Max(extentHeight, desired.Height);
                }
            }

            _contentDesiredExtent = new BSize(extentWidth, extentHeight);
            ScrollbarLayout layout = CalculateLayout(new BRect(0, 0, outerSize.Width, outerSize.Height), _contentDesiredExtent);
            SetViewportAndExtent(layout.ContentBounds.Size, layout.ExtentSize);
            return outerSize;
        }
        else if (Constraint == UiScrollConstraint.ConstrainHeight && (double.IsFinite(availableSize.Height) || double.IsFinite(PreferredSize.Height)))
        {
            double availableHeight = double.IsFinite(availableSize.Height) ? availableSize.Height : (PreferredSize.Height > 0 ? PreferredSize.Height : 240);
            double thickness = ShowScrollbars ? Math.Min(ScrollbarThickness, Math.Max(0, Math.Min(outerSize.Width, availableHeight))) : 0;

            bool hasHorizontal = HorizontalScrollBarVisibility == UiScrollBarVisibility.Visible;
            double contentHeight = hasHorizontal ? Math.Max(1, availableHeight - thickness) : Math.Max(1, availableHeight);

            double extentWidth = 0;
            double extentHeight = 0;
            foreach (UiElement child in Children)
            {
                if (child.Visibility == UiVisibility.Collapsed)
                    continue;

                BSize desired = child.Measure(new BSize(double.PositiveInfinity, contentHeight));
                extentWidth = Math.Max(extentWidth, desired.Width);
                extentHeight = Math.Max(extentHeight, desired.Height);
            }

            if (HorizontalScrollBarVisibility == UiScrollBarVisibility.Auto && !hasHorizontal && extentWidth > outerSize.Width && thickness > 0)
            {
                hasHorizontal = true;
                contentHeight = Math.Max(1, availableHeight - thickness);
                extentWidth = 0;
                extentHeight = 0;
                foreach (UiElement child in Children)
                {
                    if (child.Visibility == UiVisibility.Collapsed)
                        continue;

                    BSize desired = child.Measure(new BSize(double.PositiveInfinity, contentHeight));
                    extentWidth = Math.Max(extentWidth, desired.Width);
                    extentHeight = Math.Max(extentHeight, desired.Height);
                }
            }

            _contentDesiredExtent = new BSize(extentWidth, extentHeight);
            ScrollbarLayout layout = CalculateLayout(new BRect(0, 0, outerSize.Width, outerSize.Height), _contentDesiredExtent);
            SetViewportAndExtent(layout.ContentBounds.Size, layout.ExtentSize);
            return outerSize;
        }
        else
        {
            double extentWidth = 0;
            double extentHeight = 0;
            foreach (UiElement child in Children)
            {
                if (child.Visibility == UiVisibility.Collapsed)
                    continue;

                BSize desired = child.Measure(new BSize(double.PositiveInfinity, double.PositiveInfinity));
                extentWidth = Math.Max(extentWidth, desired.Width);
                extentHeight = Math.Max(extentHeight, desired.Height);
            }

            _contentDesiredExtent = new BSize(extentWidth, extentHeight);
            ScrollbarLayout layout = CalculateLayout(new BRect(0, 0, outerSize.Width, outerSize.Height), _contentDesiredExtent);
            SetViewportAndExtent(layout.ContentBounds.Size, layout.ExtentSize);
            return outerSize;
        }
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (Constraint == UiScrollConstraint.ConstrainWidth && double.IsFinite(finalRect.Width))
        {
            double thickness = ShowScrollbars ? Math.Min(ScrollbarThickness, Math.Max(0, Math.Min(finalRect.Width, finalRect.Height))) : 0;
            bool hasVertical = VerticalScrollBarVisibility == UiScrollBarVisibility.Visible;
            double contentWidth = hasVertical ? Math.Max(1, finalRect.Width - thickness) : Math.Max(1, finalRect.Width);

            double extentWidth = 0;
            double extentHeight = 0;
            foreach (UiElement child in Children)
            {
                if (child.Visibility == UiVisibility.Collapsed)
                    continue;

                BSize desired = child.Measure(new BSize(contentWidth, double.PositiveInfinity));
                extentWidth = Math.Max(extentWidth, desired.Width);
                extentHeight = Math.Max(extentHeight, desired.Height);
            }

            if (VerticalScrollBarVisibility == UiScrollBarVisibility.Auto && !hasVertical && extentHeight > finalRect.Height && thickness > 0)
            {
                hasVertical = true;
                contentWidth = Math.Max(1, finalRect.Width - thickness);
                extentWidth = 0;
                extentHeight = 0;
                foreach (UiElement child in Children)
                {
                    if (child.Visibility == UiVisibility.Collapsed)
                        continue;

                    BSize desired = child.Measure(new BSize(contentWidth, double.PositiveInfinity));
                    extentWidth = Math.Max(extentWidth, desired.Width);
                    extentHeight = Math.Max(extentHeight, desired.Height);
                }
            }

            _contentDesiredExtent = new BSize(extentWidth, extentHeight);
        }
        else if (Constraint == UiScrollConstraint.ConstrainHeight && double.IsFinite(finalRect.Height))
        {
            double thickness = ShowScrollbars ? Math.Min(ScrollbarThickness, Math.Max(0, Math.Min(finalRect.Width, finalRect.Height))) : 0;
            bool hasHorizontal = HorizontalScrollBarVisibility == UiScrollBarVisibility.Visible;
            double contentHeight = hasHorizontal ? Math.Max(1, finalRect.Height - thickness) : Math.Max(1, finalRect.Height);

            double extentWidth = 0;
            double extentHeight = 0;
            foreach (UiElement child in Children)
            {
                if (child.Visibility == UiVisibility.Collapsed)
                    continue;

                BSize desired = child.Measure(new BSize(double.PositiveInfinity, contentHeight));
                extentWidth = Math.Max(extentWidth, desired.Width);
                extentHeight = Math.Max(extentHeight, desired.Height);
            }

            if (HorizontalScrollBarVisibility == UiScrollBarVisibility.Auto && !hasHorizontal && extentWidth > finalRect.Width && thickness > 0)
            {
                hasHorizontal = true;
                contentHeight = Math.Max(1, finalRect.Height - thickness);
                extentWidth = 0;
                extentHeight = 0;
                foreach (UiElement child in Children)
                {
                    if (child.Visibility == UiVisibility.Collapsed)
                        continue;

                    BSize desired = child.Measure(new BSize(double.PositiveInfinity, contentHeight));
                    extentWidth = Math.Max(extentWidth, desired.Width);
                    extentHeight = Math.Max(extentHeight, desired.Height);
                }
            }

            _contentDesiredExtent = new BSize(extentWidth, extentHeight);
        }

        ScrollbarLayout layout = CalculateLayout(finalRect, _contentDesiredExtent);
        ContentBounds = layout.ContentBounds;
        HasVerticalScrollbar = layout.HasVerticalScrollbar;
        HasHorizontalScrollbar = layout.HasHorizontalScrollbar;
        _verticalTrackBounds = layout.VerticalTrackBounds;
        _horizontalTrackBounds = layout.HorizontalTrackBounds;
        _scrollbarCornerBounds = layout.ScrollbarCornerBounds;
        SetViewportAndExtent(ContentBounds.Size, layout.ExtentSize);

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            double arrangeWidth = Constraint == UiScrollConstraint.ConstrainWidth
                ? ContentBounds.Width
                : Math.Max(child.DesiredSize.Width, ContentBounds.Width);
            double arrangeHeight = Constraint == UiScrollConstraint.ConstrainHeight
                ? ContentBounds.Height
                : Math.Max(child.DesiredSize.Height, ContentBounds.Height);

            child.Arrange(new BRect(
                ContentBounds.Left - HorizontalOffset,
                ContentBounds.Top - VerticalOffset,
                arrangeWidth,
                arrangeHeight));
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        context.RenderList.PushClip(ContentBounds);
        foreach (UiElement child in Children)
            child.Render(context);
        context.RenderList.PopClip();

        RenderScrollbars(context);

        // Over the scrollbars, so the ring is whole. Drawn whenever a keyboard stop has focus, as for an
        // editor, since it shows where the arrow keys go; whatever the last input was. A view that is
        // no stop draws none: a click on blank form space focuses the scroll view behind it, and a
        // later shortcut key must not ring the whole form.
        if (Session?.FocusedElement == this && CanFocus)
        {
            BRect ring = StandardControlPaint.Inset(Bounds, _theme.FocusRingOffset);
            if (!ring.IsEmpty && _theme.FocusRingThickness > 0)
                context.RenderList.StrokeRect(ring, FocusRing, _theme.FocusRingThickness);
        }
    }

    protected override bool OnInput(UiInputEvent input)
    {
        return input.Kind switch
        {
            UiInputEventKind.PointerMove => HandlePointerMove(input),
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerWheel => HandleWheel(input),
            UiInputEventKind.TouchContact => HandleTouch(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    private bool HandleTouch(UiInputEvent input)
    {
        if (input.TouchContactState is not TouchContactState state)
            return false;

        if (state == TouchContactState.Pressed)
        {
            if (_touchContactId is not null)
                return false;

            _touchContactId = input.ContactId;
            _touchStart = input.Position;
            _touchLast = input.Position;
            _isTouchDragging = false;
            return false;
        }

        if (_touchContactId != input.ContactId)
            return false;

        if (state == TouchContactState.Moved)
        {
            double totalX = input.Position.X - _touchStart.X;
            double totalY = input.Position.Y - _touchStart.Y;
            if (!_isTouchDragging && Math.Sqrt((totalX * totalX) + (totalY * totalY)) >= TouchDragThreshold)
                _isTouchDragging = true;

            if (!_isTouchDragging)
            {
                _touchLast = input.Position;
                return false;
            }

            double deltaX = _touchLast.X - input.Position.X;
            double deltaY = _touchLast.Y - input.Position.Y;
            _touchLast = input.Position;
            _ = ScrollBy(deltaX, deltaY);
            return true;
        }

        if (state is TouchContactState.Released or TouchContactState.Cancelled)
        {
            bool handled = _isTouchDragging;
            _touchContactId = null;
            _isTouchDragging = false;
            return handled;
        }

        return false;
    }

    protected override bool ShouldHitTestChildren(BPoint point) =>
        ContentBounds.IsEmpty ? Bounds.Contains(point) : ContentBounds.Contains(point);

    // The same area children are drawn and hit-tested in: the content bounds, beside the scrollbars.
    protected override BRect? GetClipBoundsForChild(UiElement child) =>
        ContentBounds.IsEmpty ? Bounds : ContentBounds;

    // The same visibility rules as UiElement.CanFocus, without its Focusable requirement.
    private bool IsKeyboardScrollStop()
    {
        if (IsDisposed || Session is null || Visibility != UiVisibility.Visible)
            return false;
        for (UiElement? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.Visibility != UiVisibility.Visible)
                return false;
        }

        bool scrolls = ExtentSize.Height > ViewportSize.Height + 0.5 || ExtentSize.Width > ViewportSize.Width + 0.5;
        return scrolls && !HasFocusableDescendant(this);
    }

    private static bool HasFocusableDescendant(UiElement element)
    {
        foreach (UiElement child in element.Children)
        {
            if (child.Visibility != UiVisibility.Visible || child.IsHiddenFromAccessibility)
                continue;
            if (child.CanFocus || HasFocusableDescendant(child))
                return true;
        }

        return false;
    }

    private bool HandleWheel(UiInputEvent input)
    {
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);
        double delta = -input.WheelDeltaNotches * LineScrollAmount;
        return (input.WheelAxis == MouseWheelAxis.Horizontal || shift)
            ? ScrollBy(delta, 0)
            : ScrollBy(0, delta);
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
            return ScrollBy(0, ViewportSize.Height * PageScrollFraction);
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
            return ScrollBy(0, -ViewportSize.Height * PageScrollFraction);
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return ScrollToStart();
        if (IsKey(input, BVirtualKey.End, "End"))
            return ScrollToEnd();
        if (IsKey(input, BVirtualKey.Down, "Down"))
            return ScrollBy(0, LineScrollAmount);
        if (IsKey(input, BVirtualKey.Up, "Up"))
            return ScrollBy(0, -LineScrollAmount);
        if (IsKey(input, BVirtualKey.Right, "Right"))
            return ScrollBy(LineScrollAmount, 0);
        if (IsKey(input, BVirtualKey.Left, "Left"))
            return ScrollBy(-LineScrollAmount, 0);

        return false;
    }

    private void RenderScrollbars(UiRenderContext context)
    {
        if (HasVerticalScrollbar)
            RenderScrollbar(context, ScrollbarAxis.Vertical);

        if (HasHorizontalScrollbar)
            RenderScrollbar(context, ScrollbarAxis.Horizontal);

        if (!_scrollbarCornerBounds.IsEmpty)
            context.RenderList.FillRect(_scrollbarCornerBounds, ScrollbarTrack);
    }

    private void RenderScrollbar(UiRenderContext context, ScrollbarAxis axis)
    {
        BRect track = GetTrackBounds(axis);
        BRect thumb = GetThumbBounds(axis);
        StandardControlPaint.FillRounded(context.RenderList, track, ScrollbarTrack, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(context.RenderList, thumb, ScrollbarThumb, StandardControlPaint.PillRadius);
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            return HandlePointerDown(input.Position);
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up && _dragAxis != ScrollbarAxis.None)
        {
            EndScrollbarDrag();
            return true;
        }

        return false;
    }

    private bool HandlePointerDown(BPoint position)
    {
        if (HasVerticalScrollbar && TryHandleScrollbarPointerDown(ScrollbarAxis.Vertical, position))
            return true;

        return HasHorizontalScrollbar && TryHandleScrollbarPointerDown(ScrollbarAxis.Horizontal, position);
    }

    private bool TryHandleScrollbarPointerDown(ScrollbarAxis axis, BPoint position)
    {
        BRect track = GetTrackBounds(axis);
        if (track.IsEmpty || !track.Contains(position))
            return false;

        BRect thumb = GetThumbBounds(axis);
        if (thumb.Contains(position))
        {
            BeginScrollbarDrag(axis, position, thumb);
            return true;
        }

        double positionOnAxis = GetAxisPosition(axis, position);
        double pageDelta = axis == ScrollbarAxis.Vertical
            ? ViewportSize.Height * PageScrollFraction
            : ViewportSize.Width * PageScrollFraction;

        if (positionOnAxis < GetAxisStart(axis, thumb))
            pageDelta = -pageDelta;

        if (axis == ScrollbarAxis.Vertical)
            ScrollBy(0, pageDelta);
        else
            ScrollBy(pageDelta, 0);

        return true;
    }

    private void BeginScrollbarDrag(ScrollbarAxis axis, BPoint position, BRect thumb)
    {
        _dragAxis = axis;
        _dragPointerOffsetWithinThumb = GetAxisPosition(axis, position) - GetAxisStart(axis, thumb);
        Session?.CaptureInput(this);
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (_dragAxis == ScrollbarAxis.None)
            return false;

        DragScrollbar(input.Position);
        return true;
    }

    private void DragScrollbar(BPoint position)
    {
        BRect track = GetTrackBounds(_dragAxis);
        BRect thumb = GetThumbBounds(_dragAxis);
        double scrollableTrack = Math.Max(0, GetAxisLength(_dragAxis, track) - GetAxisLength(_dragAxis, thumb));
        if (scrollableTrack <= 0)
            return;

        double thumbStart = GetAxisPosition(_dragAxis, position) - _dragPointerOffsetWithinThumb;
        double normalized = (thumbStart - GetAxisStart(_dragAxis, track)) / scrollableTrack;
        double maxOffset = _dragAxis == ScrollbarAxis.Vertical ? MaxVerticalOffset : MaxHorizontalOffset;
        double offset = Math.Clamp(normalized, 0, 1) * maxOffset;

        if (_dragAxis == ScrollbarAxis.Vertical)
            SetOffset(new BPoint(HorizontalOffset, offset));
        else
            SetOffset(new BPoint(offset, VerticalOffset));
    }

    private void EndScrollbarDrag()
    {
        _dragAxis = ScrollbarAxis.None;
        _dragPointerOffsetWithinThumb = 0;
        Session?.ReleaseInputCapture(this);
    }

    private ScrollbarLayout CalculateLayout(BRect outerBounds, BSize desiredExtent)
    {
        double thickness = ShowScrollbars ? Math.Min(ScrollbarThickness, Math.Max(0, Math.Min(outerBounds.Width, outerBounds.Height))) : 0;
        bool hasVertical = ResolveInitialVisibility(VerticalScrollBarVisibility, desiredExtent.Height, outerBounds.Height, thickness);
        bool hasHorizontal = ResolveInitialVisibility(HorizontalScrollBarVisibility, desiredExtent.Width, outerBounds.Width, thickness);

        for (int index = 0; index < 3; index++)
        {
            BSize viewport = GetViewportSize(outerBounds.Size, hasVertical, hasHorizontal, thickness);
            bool nextVertical = ResolveVisibility(VerticalScrollBarVisibility, desiredExtent.Height, viewport.Height, outerBounds.Height, thickness);
            bool nextHorizontal = ResolveVisibility(HorizontalScrollBarVisibility, desiredExtent.Width, viewport.Width, outerBounds.Width, thickness);
            if (nextVertical == hasVertical && nextHorizontal == hasHorizontal)
                break;

            hasVertical = nextVertical;
            hasHorizontal = nextHorizontal;
        }

        BSize contentSize = GetViewportSize(outerBounds.Size, hasVertical, hasHorizontal, thickness);
        BRect contentBounds = new(outerBounds.Left, outerBounds.Top, contentSize.Width, contentSize.Height);
        BSize extentSize = new(Math.Max(desiredExtent.Width, contentSize.Width), Math.Max(desiredExtent.Height, contentSize.Height));
        BRect verticalTrack = hasVertical
            ? new BRect(contentBounds.Right, outerBounds.Top, thickness, contentBounds.Height)
            : BRect.Empty;
        BRect horizontalTrack = hasHorizontal
            ? new BRect(outerBounds.Left, contentBounds.Bottom, contentBounds.Width, thickness)
            : BRect.Empty;
        BRect corner = hasVertical && hasHorizontal
            ? new BRect(contentBounds.Right, contentBounds.Bottom, thickness, thickness)
            : BRect.Empty;

        return new ScrollbarLayout(contentBounds, extentSize, hasVertical, hasHorizontal, verticalTrack, horizontalTrack, corner);
    }

    private static BSize GetViewportSize(BSize outerSize, bool hasVertical, bool hasHorizontal, double thickness) =>
        new(
            Math.Max(0, outerSize.Width - (hasVertical ? thickness : 0)),
            Math.Max(0, outerSize.Height - (hasHorizontal ? thickness : 0)));

    private static bool ResolveInitialVisibility(UiScrollBarVisibility visibility, double desiredLength, double outerLength, double thickness) =>
        ResolveVisibility(visibility, desiredLength, outerLength, outerLength, thickness);

    private static bool ResolveVisibility(UiScrollBarVisibility visibility, double desiredLength, double viewportLength, double outerLength, double thickness)
    {
        if (thickness <= 0 || outerLength <= 0 || visibility == UiScrollBarVisibility.Hidden)
            return false;

        return visibility == UiScrollBarVisibility.Visible || desiredLength > viewportLength;
    }

    private BRect GetTrackBounds(ScrollbarAxis axis) =>
        axis == ScrollbarAxis.Vertical ? _verticalTrackBounds : _horizontalTrackBounds;

    private BRect GetThumbBounds(ScrollbarAxis axis)
    {
        BRect track = GetTrackBounds(axis);
        if (track.IsEmpty)
            return BRect.Empty;

        double trackLength = GetAxisLength(axis, track);
        double viewportLength = axis == ScrollbarAxis.Vertical ? ViewportSize.Height : ViewportSize.Width;
        double extentLength = axis == ScrollbarAxis.Vertical ? ExtentSize.Height : ExtentSize.Width;
        double maxOffset = axis == ScrollbarAxis.Vertical ? MaxVerticalOffset : MaxHorizontalOffset;
        double offset = axis == ScrollbarAxis.Vertical ? VerticalOffset : HorizontalOffset;
        double rawThumbLength = trackLength * (viewportLength / Math.Max(viewportLength, extentLength));
        double minThumbLength = Math.Min(Math.Max(0, MinimumThumbLength), trackLength);
        double thumbLength = Math.Clamp(rawThumbLength, minThumbLength, trackLength);
        double scrollableTrack = Math.Max(0, trackLength - thumbLength);
        double thumbStart = GetAxisStart(axis, track) + (maxOffset <= 0 ? 0 : scrollableTrack * (offset / maxOffset));

        return axis == ScrollbarAxis.Vertical
            ? new BRect(track.Left, thumbStart, track.Width, thumbLength)
            : new BRect(thumbStart, track.Top, thumbLength, track.Height);
    }

    private static double GetAxisPosition(ScrollbarAxis axis, BPoint point) =>
        axis == ScrollbarAxis.Vertical ? point.Y : point.X;

    private static double GetAxisStart(ScrollbarAxis axis, BRect rect) =>
        axis == ScrollbarAxis.Vertical ? rect.Top : rect.Left;

    private static double GetAxisLength(ScrollbarAxis axis, BRect rect) =>
        axis == ScrollbarAxis.Vertical ? rect.Height : rect.Width;

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Scrollbar metrics must be finite non-negative values.");
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private readonly record struct ScrollbarLayout(
        BRect ContentBounds,
        BSize ExtentSize,
        bool HasVerticalScrollbar,
        bool HasHorizontalScrollbar,
        BRect VerticalTrackBounds,
        BRect HorizontalTrackBounds,
        BRect ScrollbarCornerBounds);

    private enum ScrollbarAxis
    {
        None,
        Vertical,
        Horizontal,
    }
}
