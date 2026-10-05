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
    // Whether the view was a FocusWhenScrollable stop when it was last drawn. Kept while it is hidden, so a view
    // shown again as no stop still hands on the focus it kept.
    private bool _wasScrollStop;
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

    // The translucent bars the view has always drawn, kept while the theme says nothing about scrollbars.
    private static readonly BColor OwnScrollbarTrack = BColor.FromArgb(0x33, 0x94, 0xA3, 0xB8);
    private static readonly BColor OwnScrollbarThumb = BColor.FromArgb(0xAA, 0x7D, 0x8D, 0xA3);

    // The bars the shared palette gives a view that is not themed yet.
    private static (BColor Track, BColor Thumb) PaletteScrollbars =>
        StandardControlPaint.ScrollbarColors(StandardControlPaint.Theme, OwnScrollbarTrack, OwnScrollbarThumb);

    // The bars the last theme gave the view. ApplyTheme moves a color on to the next theme's only while it still
    // holds this one, so a color the application set outlives a theme change, as a font does (StandardThemeFonts).
    private (BColor Track, BColor Thumb) _themeScrollbars = PaletteScrollbars;

    /// <summary>
    /// The track of the scrollbars, also filling the corner where the two meet. A translucent gray until a theme
    /// gives scrollbars colors of their own (<see cref="StandardControlPaint.ScrollbarColors"/>), as a high-contrast
    /// theme does; then <see cref="StandardThemeTokens.ScrollbarTrack"/>. A color set here is kept by
    /// <see cref="ApplyTheme"/>.
    /// </summary>
    public BColor ScrollbarTrack { get; set; } = PaletteScrollbars.Track;

    /// <summary>
    /// The thumb of the scrollbars. A translucent gray until a theme gives scrollbars colors of their own; then
    /// <see cref="StandardThemeTokens.ScrollbarThumb"/>. A color set here is kept by <see cref="ApplyTheme"/>.
    /// </summary>
    public BColor ScrollbarThumb { get; set; } = PaletteScrollbars.Thumb;

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
    /// nothing to scroll would take focus with nothing to do or announce. A view that stops being a
    /// stop while it has focus (its content shrinks, its viewport grows, a control inside it can take
    /// focus, or this is turned off) hands focus on when it is next drawn, through
    /// <see cref="IUiDispatcher.Post"/>: to the next tab stop in the focus scope, or the previous one
    /// when none follows. That stop is scrolled into view only when none of it can be seen, and
    /// focus that is elsewhere by then is left alone. A view hidden while it has focus is left to
    /// what hid it, and hands focus on once it is shown again if it is no stop then.
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
            // Drawn again: the ring follows the stop, and a focused view that is no stop now hands focus on.
            Invalidate(UiInvalidationKind.Semantic | UiInvalidationKind.Render);
        }
    }

    public override bool CanFocus => base.CanFocus || (FocusWhenScrollable && IsKeyboardScrollStop());

    /// <summary>
    /// Re-derives the focus ring and the scrollbars from <paramref name="theme"/>. The bars take the theme's
    /// scrollbar roles when it gives scrollbars colors of their own, as a high-contrast theme does, and
    /// otherwise their translucent defaults, so the Light and Dark presets draw them as before. A bar color the
    /// application set is kept.
    /// </summary>
    public void ApplyTheme(StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _theme = theme;
        FocusRing = theme.FocusRing;
        (BColor track, BColor thumb) = StandardControlPaint.ScrollbarColors(theme, OwnScrollbarTrack, OwnScrollbarThumb);
        if (ScrollbarTrack == _themeScrollbars.Track)
            ScrollbarTrack = track;
        if (ScrollbarThumb == _themeScrollbars.Thumb)
            ScrollbarThumb = thumb;
        _themeScrollbars = (track, thumb);
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
        HandFocusOnIfNoLongerAStop();

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
            {
                context.RenderList.StrokeRect(ring, FocusRing, _theme.FocusRingThickness);
                RenderRingAcrossThumb(context, ring, ScrollbarAxis.Vertical);
                RenderRingAcrossThumb(context, ring, ScrollbarAxis.Horizontal);
            }
        }
    }

    /// <summary>
    /// Draws the stretch of the ring that crosses an opaque thumb of too nearly its color again, in a color that
    /// stands out on the thumb (<see cref="StandardControlPaint.FocusRingColor"/>), so the ring stays whole. A
    /// high-contrast theme draws its thumb in the text color, which is the ring's color in HighContrastLight.
    /// </summary>
    private void RenderRingAcrossThumb(UiRenderContext context, BRect ring, ScrollbarAxis axis)
    {
        BColor across = StandardControlPaint.FocusRingColor(FocusRing, ScrollbarThumb, _theme.Surface);
        BRect thumb = GetThumbBounds(axis);
        if (across == FocusRing || thumb.IsEmpty)
            return;

        context.RenderList.PushClip(thumb);
        context.RenderList.StrokeRect(ring, across, _theme.FocusRingThickness);
        context.RenderList.PopClip();
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

    private bool IsKeyboardScrollStop()
    {
        if (!IsShown())
            return false;

        bool scrolls = ExtentSize.Height > ViewportSize.Height + 0.5 || ExtentSize.Width > ViewportSize.Width + 0.5;
        return scrolls && !HasFocusableDescendant(this);
    }

    // The same visibility rules as UiElement.CanFocus, without its Focusable requirement.
    private bool IsShown()
    {
        if (IsDisposed || Session is null || Visibility != UiVisibility.Visible)
            return false;
        for (UiElement? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.Visibility != UiVisibility.Visible)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Called as the view is drawn, once the frame's layout is done, so it also sees a change that needs no layout,
    /// such as a control inside that was enabled, or <see cref="FocusWhenScrollable"/> turned off. A view that has
    /// focus and was a <see cref="FocusWhenScrollable"/> stop when it was last drawn, but is none now, would keep
    /// focus with no ring and nothing for the keys to do, so focus moves on, as <see cref="FocusWhenScrollable"/>
    /// describes. It moves through the session's dispatcher: a host that queues posts moves it after the frame.
    /// </summary>
    private void HandFocusOnIfNoLongerAStop()
    {
        if (!IsShown())
            return;

        bool wasStop = _wasScrollStop;
        _wasScrollStop = FocusWhenScrollable && IsKeyboardScrollStop();
        if (wasStop && !_wasScrollStop && Session is { } session && session.FocusedElement == this && !base.CanFocus)
            session.Dispatcher.Post(HandFocusOn);
    }

    /// <summary>
    /// Moves focus to the next tab stop in the focus scope, or to the previous one when none follows, unless it has
    /// moved elsewhere since the view was drawn, or this view is a stop again. The stop is scrolled into view, as Tab
    /// would, only when none of it can be seen: a ring that shows in part shows where focus went, and this follows a
    /// change of layout, not the user's navigation, so content the user is reading is not moved for it.
    /// </summary>
    private void HandFocusOn()
    {
        if (IsDisposed || Session is not { } session || session.FocusedElement != this || CanFocus || !IsShown())
            return;

        var scope = new StandardFocusScope(session);
        UiElement? target = scope.FindAdjacentStop(this, 1) ?? scope.FindAdjacentStop(this, -1);
        if (target is null)
            return;

        session.SetFocus(target);
        if (session.FocusedElement == target && target.GetVisibleBounds().IsEmpty)
            target.BringIntoView();
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
        double amount = input.WheelDeltaNotches * LineScrollAmount;
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);

        // A wheel tilted right (a positive horizontal notch, as Win32 reports it) scrolls right.
        if (input.WheelAxis == MouseWheelAxis.Horizontal && !shift)
            return ScrollBy(amount, 0);

        // A wheel turned away from the user scrolls towards the start. Shift turns it sideways with
        // the same sign, so turning it towards the user scrolls right, as in other Windows applications.
        // A host may already have turned it sideways (Broiler.Hosting.Windows reports it as a
        // horizontal notch that keeps Shift and the vertical sign), and it means the same then.
        return shift
            ? ScrollBy(-amount, 0)
            : ScrollBy(0, -amount);
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
