using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.Toolbar.Standard;

public sealed class StandardToolbar : UiToolbar, IStandardThemedControl
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
        Background = theme.SurfaceAlt;
        BorderColor = theme.Border;
        SeparatorColor = theme.BorderStrong;
        Foreground = theme.Text;
        PopupBackground = theme.Surface;
        _overflowOpenBackground = theme.StateFill;

        // The chevron keeps following Foreground unless the theme gives state text a color of its own.
        _overflowOpenForeground = theme.StateText == theme.Text ? null : theme.StateText;
    }

    /// <summary>The items that did not fit, in bar order, and the boxes they are reached through.</summary>
    private readonly List<UiElement> _overflowed = [];
    private readonly HashSet<UiElement> _isOverflowed = [];
    private BRect _overflowButtonBounds = BRect.Empty;
    private BRect _overflowPopupBounds = BRect.Empty;
    private UiElement? _pressedOverflowItem;
    private BColor? _overflowOpenBackground;
    private BColor? _overflowOpenForeground;

    /// <summary>The chevron a bar with overflow ends in, and how wide it is drawn.</summary>
    private const string OverflowGlyph = "»";

    public BColor Background { get; set; } = StandardControlPaint.SurfaceAlt;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor SeparatorColor { get; set; } = StandardControlPaint.BorderStrong;

    /// <summary>The surface the overflow drop-down is drawn on.</summary>
    public BColor PopupBackground { get; set; } = StandardControlPaint.Surface;

    /// <summary>
    /// The fill behind the chevron while the drop-down is open. Until it is set, or a theme is applied, it is
    /// the shared palette's <see cref="StandardControlPaint.StateFill"/>; <see cref="ApplyTheme"/> sets it to
    /// the theme's <see cref="StandardThemeTokens.StateFill"/>.
    /// </summary>
    public BColor OverflowOpenBackground
    {
        get => _overflowOpenBackground ?? StandardControlPaint.StateFill;
        set => _overflowOpenBackground = value;
    }

    /// <summary>
    /// The chevron's color while the drop-down is open, drawn on <see cref="OverflowOpenBackground"/>. Until it
    /// is set it is <see cref="Foreground"/>. <see cref="ApplyTheme"/> sets it to the theme's
    /// <see cref="StandardThemeTokens.StateText"/> when that differs from the theme's text color, and otherwise
    /// lets it follow <see cref="Foreground"/> again.
    /// </summary>
    public BColor OverflowOpenForeground
    {
        get => _overflowOpenForeground ?? Foreground;
        set => _overflowOpenForeground = value;
    }

    /// <summary>The font the overflow chevron is drawn in.</summary>
    public BFontStyle Font { get; set; } = StandardControlPaint.Theme.FontBody;
    private BFontStyle _themeFont = StandardControlPaint.Theme.FontBody;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public double SeparatorExtent { get; set; } = 9;

    /// <summary>
    /// The extra space a <see cref="UiToolbarBreak.Gap"/> opens in front of an item, on top of
    /// <see cref="UiToolbar.Spacing"/>. Enough that the eye finds the groups; not so much that the
    /// bar looks broken into pieces.
    /// </summary>
    public double GroupExtent { get; set; } = 8;

    /// <summary>How much of the bar the overflow chevron takes when there is one.</summary>
    public double OverflowButtonExtent { get; set; } = 24;

    /// <summary>The items that did not fit along the bar, in the order they sit on it.</summary>
    public IReadOnlyList<UiElement> OverflowItems => _overflowed;

    /// <summary>The chevron's box, empty when nothing has overflowed.</summary>
    public BRect OverflowButtonBounds => _overflowButtonBounds;

    /// <summary>The drop-down's box, empty when nothing has overflowed.</summary>
    public BRect OverflowPopupBounds => _overflowPopupBounds;

    /// <summary>The drop-down, while it is down.</summary>
    public override BRect OverlayBounds => IsOverflowOpen ? _overflowPopupBounds : BRect.Empty;

    public override bool OpenOverflow() => _overflowed.Count > 0 && base.OpenOverflow();

    protected override BSize MeasureCore(BSize availableSize)
    {
        BSize contentAvailable = new(
            Math.Max(0, availableSize.Width - Padding * 2),
            Math.Max(0, availableSize.Height - Padding * 2));

        if (Overflow == UiToolbarOverflow.Wrap)
        {
            if (Orientation == UiToolbarOrientation.Horizontal && double.IsFinite(contentAvailable.Width) && contentAvailable.Width > 0)
            {
                double maxLineWidth = 0;
                double totalHeight = 0;
                double currentLineWidth = 0;
                double currentLineHeight = 0;
                int itemsOnLine = 0;

                foreach (UiElement child in Children)
                {
                    if (child.Visibility == UiVisibility.Collapsed)
                        continue;

                    BSize desired = child.Measure(contentAvailable);
                    double lead = itemsOnLine > 0 ? BreakExtent(child) + Spacing : 0;
                    if (itemsOnLine > 0 && currentLineWidth + lead + desired.Width > contentAvailable.Width)
                    {
                        maxLineWidth = Math.Max(maxLineWidth, currentLineWidth);
                        totalHeight += currentLineHeight + Spacing;
                        currentLineWidth = desired.Width;
                        currentLineHeight = desired.Height;
                        itemsOnLine = 1;
                    }
                    else
                    {
                        currentLineWidth += lead + desired.Width;
                        currentLineHeight = Math.Max(currentLineHeight, desired.Height);
                        itemsOnLine++;
                    }
                }

                if (itemsOnLine > 0)
                {
                    maxLineWidth = Math.Max(maxLineWidth, currentLineWidth);
                    totalHeight += currentLineHeight;
                }

                double wrapWidth = Math.Max(maxLineWidth + Padding * 2, PreferredSize.Width);
                double wrapHeight = Math.Max(totalHeight + Padding * 2, PreferredSize.Height);
                return new BSize(ClampDesired(wrapWidth, availableSize.Width), ClampDesired(wrapHeight, availableSize.Height));
            }
            else if (Orientation == UiToolbarOrientation.Vertical && double.IsFinite(contentAvailable.Height) && contentAvailable.Height > 0)
            {
                double maxColHeight = 0;
                double totalWidth = 0;
                double currentColHeight = 0;
                double currentColWidth = 0;
                int itemsOnCol = 0;

                foreach (UiElement child in Children)
                {
                    if (child.Visibility == UiVisibility.Collapsed)
                        continue;

                    BSize desired = child.Measure(contentAvailable);
                    double lead = itemsOnCol > 0 ? BreakExtent(child) + Spacing : 0;
                    if (itemsOnCol > 0 && currentColHeight + lead + desired.Height > contentAvailable.Height)
                    {
                        maxColHeight = Math.Max(maxColHeight, currentColHeight);
                        totalWidth += currentColWidth + Spacing;
                        currentColHeight = desired.Height;
                        currentColWidth = desired.Width;
                        itemsOnCol = 1;
                    }
                    else
                    {
                        currentColHeight += lead + desired.Height;
                        currentColWidth = Math.Max(currentColWidth, desired.Width);
                        itemsOnCol++;
                    }
                }

                if (itemsOnCol > 0)
                {
                    maxColHeight = Math.Max(maxColHeight, currentColHeight);
                    totalWidth += currentColWidth;
                }

                double wrapWidth = Math.Max(totalWidth + Padding * 2, PreferredSize.Width);
                double wrapHeight = Math.Max(maxColHeight + Padding * 2, PreferredSize.Height);
                return new BSize(ClampDesired(wrapWidth, availableSize.Width), ClampDesired(wrapHeight, availableSize.Height));
            }
        }

        double primary = 0;
        double cross = 0;
        int visibleCount = 0;
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                continue;

            BSize desired = child.Measure(contentAvailable);
            if (visibleCount > 0)
                primary += BreakExtent(child) + Spacing;

            if (Orientation == UiToolbarOrientation.Horizontal)
            {
                primary += desired.Width;
                cross = Math.Max(cross, desired.Height);
            }
            else
            {
                primary += desired.Height;
                cross = Math.Max(cross, desired.Width);
            }

            visibleCount++;
        }

        double width = Orientation == UiToolbarOrientation.Horizontal ? primary + Padding * 2 : cross + Padding * 2;
        double height = Orientation == UiToolbarOrientation.Horizontal ? cross + Padding * 2 : primary + Padding * 2;
        width = Math.Max(width, PreferredSize.Width);
        height = Math.Max(height, PreferredSize.Height);
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    /// <summary>
    /// Lays the bar out, and works out where it runs out of room. Everything from
    /// the first item that does not fit onward goes to the drop-down, rather than
    /// only the items that happen not to fit: a bar whose order on screen differs
    /// from its order in the menu is a bar the user cannot navigate.
    /// </summary>
    protected override void ArrangeCore(BRect finalRect)
    {
        BRect content = GetContentBounds(finalRect);
        bool horizontal = Orientation == UiToolbarOrientation.Horizontal;
        List<UiElement> visible = GetVisibleChildren();

        _overflowed.Clear();
        _isOverflowed.Clear();
        _overflowButtonBounds = BRect.Empty;
        _overflowPopupBounds = BRect.Empty;

        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Collapsed)
                child.Arrange(BRect.Empty);
        }

        if (Overflow == UiToolbarOverflow.Wrap)
        {
            CloseOverflow();
            if (horizontal)
            {
                double currentLineWidth = 0;
                double currentLineHeight = 0;
                int itemsOnLine = 0;
                var currentLineItems = new List<(UiElement Element, double Width, double Height, double Lead)>();
                double lineY = content.Top;

                void FlushLine()
                {
                    if (currentLineItems.Count == 0) return;
                    double x = content.Left;
                    foreach (var item in currentLineItems)
                    {
                        x += item.Lead;
                        double y = lineY + Math.Max(0, (currentLineHeight - item.Height) / 2);
                        item.Element.Arrange(new BRect(x, y, item.Width, item.Height));
                        x += item.Width;
                    }
                    lineY += currentLineHeight + Spacing;
                    currentLineItems.Clear();
                    currentLineWidth = 0;
                    currentLineHeight = 0;
                    itemsOnLine = 0;
                }

                foreach (UiElement child in visible)
                {
                    double lead = itemsOnLine > 0 ? BreakExtent(child) + Spacing : 0;
                    double w = child.DesiredSize.Width;
                    double h = child.DesiredSize.Height;

                    if (itemsOnLine > 0 && currentLineWidth + lead + w > content.Width)
                    {
                        FlushLine();
                        lead = 0;
                    }

                    currentLineItems.Add((child, w, h, lead));
                    currentLineWidth += lead + w;
                    currentLineHeight = Math.Max(currentLineHeight, h);
                    itemsOnLine++;
                }

                FlushLine();
            }
            else
            {
                double currentColHeight = 0;
                double currentColWidth = 0;
                int itemsOnCol = 0;
                var currentColItems = new List<(UiElement Element, double Width, double Height, double Lead)>();
                double colX = content.Left;

                void FlushColumn()
                {
                    if (currentColItems.Count == 0) return;
                    double y = content.Top;
                    foreach (var item in currentColItems)
                    {
                        y += item.Lead;
                        double x = colX + Math.Max(0, (currentColWidth - item.Width) / 2);
                        item.Element.Arrange(new BRect(x, y, item.Width, item.Height));
                        y += item.Height;
                    }
                    colX += currentColWidth + Spacing;
                    currentColItems.Clear();
                    currentColHeight = 0;
                    currentColWidth = 0;
                    itemsOnCol = 0;
                }

                foreach (UiElement child in visible)
                {
                    double lead = itemsOnCol > 0 ? BreakExtent(child) + Spacing : 0;
                    double w = child.DesiredSize.Width;
                    double h = child.DesiredSize.Height;

                    if (itemsOnCol > 0 && currentColHeight + lead + h > content.Height)
                    {
                        FlushColumn();
                        lead = 0;
                    }

                    currentColItems.Add((child, w, h, lead));
                    currentColHeight += lead + h;
                    currentColWidth = Math.Max(currentColWidth, w);
                    itemsOnCol++;
                }

                FlushColumn();
            }

            return;
        }

        double available = Math.Max(0, horizontal ? content.Width : content.Height);
        bool overflows = Overflow == UiToolbarOverflow.Menu && RunExtent(visible, horizontal) > available;
        double limit = overflows
            ? Math.Max(0, available - OverflowButtonExtent - Spacing)
            : available;

        double origin = horizontal ? content.Left : content.Top;
        double cursor = origin;
        int placed = 0;
        for (int index = 0; index < visible.Count; index++)
        {
            UiElement child = visible[index];
            double lead = placed > 0 ? BreakExtent(child) + Spacing : 0;
            double extent = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;

            if (overflows && cursor + lead + extent - origin > limit)
            {
                for (int rest = index; rest < visible.Count; rest++)
                {
                    _overflowed.Add(visible[rest]);
                    _isOverflowed.Add(visible[rest]);
                }

                break;
            }

            cursor += lead;
            ArrangeAlongBar(child, content, cursor, horizontal);
            cursor += extent;
            placed++;
        }

        if (_overflowed.Count == 0)
        {
            CloseOverflow();
            return;
        }

        _overflowButtonBounds = horizontal
            ? new BRect(content.Right - OverflowButtonExtent, content.Top, OverflowButtonExtent, content.Height)
            : new BRect(content.Left, content.Bottom - OverflowButtonExtent, content.Width, OverflowButtonExtent);
        ArrangeOverflowPopup(horizontal);
    }

    private void ArrangeAlongBar(UiElement child, BRect content, double cursor, bool horizontal)
    {
        if (horizontal)
        {
            double height = Math.Min(child.DesiredSize.Height, content.Height);
            double top = content.Top + Math.Max(0, (content.Height - height) / 2);
            child.Arrange(new BRect(cursor, top, child.DesiredSize.Width, height));
        }
        else
        {
            double width = Math.Min(child.DesiredSize.Width, content.Width);
            double left = content.Left + Math.Max(0, (content.Width - width) / 2);
            child.Arrange(new BRect(left, cursor, width, child.DesiredSize.Height));
        }
    }

    /// <summary>
    /// Stacks the overflowed items under the chevron, in as many columns as it
    /// takes to fit the window. One column is what nearly every bar needs; a bar
    /// that overflowed twenty items into a short window would otherwise get a
    /// drop-down taller than the screen, whose foot is exactly as unreachable as
    /// the clipped end this replaced.
    /// </summary>
    /// <remarks>
    /// A closed drop-down arranges its items empty, so they are neither drawn nor
    /// hit-tested while it is shut, and the bar keeps the one row it had.
    /// </remarks>
    private void ArrangeOverflowPopup(bool horizontal)
    {
        double itemWidth = 0;
        double rowHeight = 0;
        foreach (UiElement child in _overflowed)
        {
            itemWidth = Math.Max(itemWidth, child.DesiredSize.Width);
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
        }

        double barTop = horizontal ? Bounds.Top : _overflowButtonBounds.Top;
        double barBottom = horizontal ? Bounds.Bottom : _overflowButtonBounds.Bottom;
        double room = double.PositiveInfinity;
        if (Session is not null)
        {
            BSize viewport = Session.Host.ViewportSize;
            room = Math.Max(0, Math.Max(viewport.Height - barBottom, barTop)) - (Padding * 2);
        }

        int perColumn = _overflowed.Count;
        if (double.IsFinite(room) && rowHeight > 0)
        {
            perColumn = (int)Math.Floor((room + Spacing) / (rowHeight + Spacing));
            perColumn = Math.Clamp(perColumn, 1, _overflowed.Count);
        }

        int columns = (int)Math.Ceiling(_overflowed.Count / (double)perColumn);
        int rows = Math.Min(perColumn, _overflowed.Count);
        double width = (columns * itemWidth) + ((columns - 1) * Spacing) + (Padding * 2);
        double height = (rows * rowHeight) + ((rows - 1) * Spacing) + (Padding * 2);

        double left = (horizontal ? _overflowButtonBounds.Right : Bounds.Right) - width;
        double top = barBottom;
        if (Session is not null)
        {
            BSize viewport = Session.Host.ViewportSize;
            left = Math.Clamp(left, 0, Math.Max(0, viewport.Width - width));

            // Above the bar rather than off the bottom of the window, the way the
            // combo box flips its own drop-down.
            if (top + height > viewport.Height)
                top = Math.Max(0, barTop - height);
        }
        else
        {
            left = Math.Max(0, left);
        }

        _overflowPopupBounds = new BRect(left, top, width, height);

        for (int index = 0; index < _overflowed.Count; index++)
        {
            UiElement child = _overflowed[index];
            if (!IsOverflowOpen)
            {
                child.Arrange(BRect.Empty);
                continue;
            }

            int column = index / perColumn;
            int row = index % perColumn;
            child.Arrange(new BRect(
                left + Padding + (column * (itemWidth + Spacing)),
                top + Padding + (row * (rowHeight + Spacing)),
                itemWidth,
                child.DesiredSize.Height));
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        StandardControlPaint.FillRounded(context.RenderList, Bounds, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, CornerRadius, 1);
        if (Session?.FocusedElement == this)
            StandardControlPaint.DrawFocusRing(context.RenderList, Bounds, CornerRadius);

        DrawSeparators(context);

        BRect content = GetContentBounds(Bounds);
        context.RenderList.PushClip(content);
        foreach (UiElement child in Children)
        {
            if (!_isOverflowed.Contains(child))
                child.Render(context);
        }

        context.RenderList.PopClip();

        if (_overflowed.Count == 0)
            return;

        DrawOverflowButton(context.RenderList);

        // Deferred, so the drop-down is drawn over whatever the bar sits above
        // rather than under it, and outside the clip the bar keeps its own row in.
        if (IsOverflowOpen)
            context.Defer(RenderOverflowPopup);
    }

    private void DrawOverflowButton(BRenderList renderList)
    {
        BColor foreground = !IsEnabled ? StandardControlPaint.TextDisabled : IsOverflowOpen ? OverflowOpenForeground : Foreground;
        if (IsOverflowOpen)
            StandardControlPaint.FillRounded(renderList, _overflowButtonBounds, OverflowOpenBackground, CornerRadius);

        BSize glyph = BTextMeasurer.Measure(OverflowGlyph, Font).Size;
        renderList.DrawText(
            new BTextRun(OverflowGlyph, Font, foreground),
            new BPoint(
                _overflowButtonBounds.Left + Math.Max(0, (_overflowButtonBounds.Width - glyph.Width) / 2),
                _overflowButtonBounds.Top + Math.Max(0, (_overflowButtonBounds.Height - glyph.Height) / 2)));
    }

    private void RenderOverflowPopup(UiRenderContext context)
    {
        StandardControlPaint.FillRounded(context.RenderList, _overflowPopupBounds, PopupBackground, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, _overflowPopupBounds, BorderColor, CornerRadius, 1);
        foreach (UiElement child in _overflowed)
            child.Render(context);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        // An item showing a list of its own is working outside this drop-down,
        // and the input that works it is that item's, not the bar's. Reading it
        // as a press on the bar would dismiss the list under the finger choosing
        // from it.
        if (IsOverflowOpen && ShowingItem() is UiElement busy && IsForItem(busy, input))
            return ForwardToShowingItem(busy, input);

        if (input.Kind == UiInputEventKind.PointerMove)
            return HandlePointerMove(input);

        if (input.Kind == UiInputEventKind.PointerButton &&
            input.MouseButton == MouseButton.Left)
        {
            return HandlePointerButton(input);
        }

        if (input.Kind == UiInputEventKind.KeyboardKey)
        {
            bool pressed = input.KeyTransition == KeyboardKeyTransition.Down;
            if (IsOverflowOpen && pressed && IsKey(input, BVirtualKey.Escape, "Escape"))
            {
                CloseManagedOverflow();
                return true;
            }

            // The bar holds the input while its drop-down is down, so a key meant
            // for the item that has focus in there has to be handed on. What the
            // item does not answer - the arrows - falls through to the bar's own
            // navigation, which is what moves focus off it again.
            if (IsOverflowOpen &&
                Session?.FocusedElement is UiElement focused &&
                _isOverflowed.Contains(focused) &&
                focused.DispatchInput(input))
            {
                return true;
            }

            return pressed && HandleKeyboard(input);
        }

        return false;
    }

    /// <summary>
    /// Keeps the drop-down's items hovering while it is open. Every one of them
    /// is told, not only the one under the pointer: each decides from the point
    /// itself, which is how the one being left stops looking hovered.
    /// </summary>
    private bool HandlePointerMove(UiInputEvent input)
    {
        if (!IsOverflowOpen)
            return false;

        foreach (UiElement child in _overflowed)
            child.DispatchInput(input);

        return _overflowPopupBounds.Contains(input.Position);
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        bool down = input.MouseButtonTransition == MouseButtonTransition.Down;
        bool onChevron = _overflowed.Count > 0 && _overflowButtonBounds.Contains(input.Position);

        if (down && onChevron)
        {
            if (IsOverflowOpen)
                CloseManagedOverflow();
            else
                OpenManagedOverflow();
            return true;
        }

        if (IsOverflowOpen)
        {
            if (!_overflowPopupBounds.Contains(input.Position))
            {
                // A press anywhere else dismisses, and is spent doing it - which
                // is what every drop-down in the toolkit does. A release finishes
                // a press that started on an item and slid off it, so the item is
                // told and can drop its pressed look without running.
                if (down)
                {
                    CloseManagedOverflow();
                }
                else if (_pressedOverflowItem is UiElement dragged)
                {
                    _pressedOverflowItem = null;
                    dragged.DispatchInput(input);
                    CloseManagedOverflow();
                }

                return true;
            }

            if (down)
            {
                UiElement? item = OverflowItemAt(input.Position);
                _pressedOverflowItem = item;
                item?.DispatchInput(input);

                // The item takes the session's capture on a press, the way any
                // pressed control does. The bar takes it straight back, or the
                // release would go to the item directly and the drop-down would
                // still be open behind the command it just ran.
                Session?.CaptureInput(this);
                return true;
            }

            UiElement? pressed = _pressedOverflowItem;
            _pressedOverflowItem = null;
            pressed?.DispatchInput(input);

            // An item that answered the press by showing a list of its own is not
            // finished, and neither is the drop-down it is standing in.
            if (ShowingItem() is null)
                CloseManagedOverflow();

            return true;
        }

        if (down)
            Session?.SetFocus(this);

        return down;
    }

    /// <summary>
    /// The item in the drop-down that is showing something of its own, if any.
    /// The bar does not have to know what kind of control it is holding: an item
    /// that reaches beyond its own box says so through
    /// <see cref="UiElement.OverlayBounds"/>.
    /// </summary>
    private UiElement? ShowingItem()
    {
        foreach (UiElement child in _overflowed)
        {
            if (!child.OverlayBounds.IsEmpty)
                return child;
        }

        return null;
    }

    /// <summary>Whether an event belongs to the item that is showing its own list.</summary>
    private static bool IsForItem(UiElement item, UiInputEvent input) =>
        input.Kind switch
        {
            UiInputEventKind.KeyboardKey or UiInputEventKind.TextInput or UiInputEventKind.TextComposition => true,
            UiInputEventKind.PointerMove or UiInputEventKind.PointerButton =>
                item.OverlayBounds.Contains(input.Position) || item.Bounds.Contains(input.Position),
            _ => false,
        };

    /// <summary>
    /// Hands an event to the item working its own list, and shuts this drop-down
    /// once that list is gone - the click that chose a value, or the one that
    /// dismissed it without choosing.
    /// </summary>
    private bool ForwardToShowingItem(UiElement item, UiInputEvent input)
    {
        item.DispatchInput(input);

        // The item takes the session's capture while its list is down; the bar
        // takes it back, because the bar is what routes to the item and what a
        // press outside everything has to reach to dismiss the pair of them.
        Session?.CaptureInput(this);
        if (ShowingItem() is null)
        {
            _pressedOverflowItem = null;
            CloseManagedOverflow();
        }

        return true;
    }

    private UiElement? OverflowItemAt(BPoint point)
    {
        for (int index = _overflowed.Count - 1; index >= 0; index--)
        {
            UiElement child = _overflowed[index];
            if (child.Visibility == UiVisibility.Visible && child.Bounds.Contains(point))
                return child;
        }

        return null;
    }

    /// <summary>
    /// Shows the drop-down and takes the session's input with it, however it was
    /// opened, so a press anywhere else dismisses it and a drop-down opened from
    /// the keyboard answers a mouse exactly as one opened by a mouse does. The
    /// keys an item in there is owed are handed on rather than swallowed.
    /// </summary>
    private bool OpenManagedOverflow()
    {
        if (!OpenOverflow())
            return false;

        Session?.SetFocus(this);
        Session?.CaptureInput(this);
        return true;
    }

    private void CloseManagedOverflow()
    {
        _pressedOverflowItem = null;
        CloseOverflow();
        Session?.ReleaseInputCapture(this);
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return FocusIndexedChild(first: true);
        if (IsKey(input, BVirtualKey.End, "End"))
            return FocusIndexedChild(first: false);

        bool forward = Orientation == UiToolbarOrientation.Horizontal
            ? IsKey(input, BVirtualKey.Right, "Right")
            : IsKey(input, BVirtualKey.Down, "Down");
        bool backward = Orientation == UiToolbarOrientation.Horizontal
            ? IsKey(input, BVirtualKey.Left, "Left")
            : IsKey(input, BVirtualKey.Up, "Up");

        if (forward)
            return MoveFocus(1);
        if (backward)
            return MoveFocus(-1);

        return false;
    }

    private bool FocusIndexedChild(bool first)
    {
        List<UiElement> focusable = GetVisibleChildren();
        if (focusable.Count == 0 || Session is null)
            return false;

        return FocusChild(first ? focusable[0] : focusable[^1]);
    }

    private bool MoveFocus(int delta)
    {
        List<UiElement> focusable = GetVisibleChildren();
        if (focusable.Count == 0 || Session is null)
            return false;

        UiElement? focused = Session.FocusedElement;
        int index = -1;
        if (focused is not null)
        {
            for (int candidate = 0; candidate < focusable.Count; candidate++)
            {
                UiElement child = focusable[candidate];
                if (ReferenceEquals(focused, child) || focused.IsDescendantOf(child))
                {
                    index = candidate;
                    break;
                }
            }
        }

        int next = index < 0
            ? (delta >= 0 ? 0 : focusable.Count - 1)
            : (index + delta + focusable.Count) % focusable.Count;
        return FocusChild(focusable[next]);
    }

    /// <summary>
    /// Moves focus to a child, showing the drop-down when the child is inside it
    /// and shutting it again on the way out. Arrowing along the bar reaches every
    /// item it holds, which is what it did when the far end was merely clipped and
    /// has to keep doing now that the far end is behind a chevron.
    /// </summary>
    private bool FocusChild(UiElement child)
    {
        if (Session is null)
            return false;

        if (_isOverflowed.Contains(child))
        {
            // Laid out again straight away: an item in a shut drop-down has no
            // box, and focus that lands on a box-less control is focus the user
            // cannot see.
            if (OpenManagedOverflow())
                Arrange(Bounds);
        }
        else if (IsOverflowOpen)
        {
            CloseManagedOverflow();
            Arrange(Bounds);
        }

        Session.SetFocus(child);
        return true;
    }

    private List<UiElement> GetVisibleChildren()
    {
        var result = new List<UiElement>(Children.Count);
        foreach (UiElement child in Children)
        {
            if (child.Visibility == UiVisibility.Visible)
                result.Add(child);
        }

        return result;
    }

    /// <summary>
    /// The extra room the break in front of <paramref name="child"/> asks for, on top of the bar's
    /// normal spacing. Measure, arrange and the overflow calculation all have to agree on this, so
    /// there is one answer and they all read it.
    /// </summary>
    private double BreakExtent(UiElement child) =>
        GetBreakBefore(child) switch
        {
            UiToolbarBreak.Separator => SeparatorExtent,
            UiToolbarBreak.Gap => GroupExtent,
            _ => 0,
        };

    /// <summary>The room a run of items needs along the bar, separators and spacing included.</summary>
    private double RunExtent(List<UiElement> visible, bool horizontal)
    {
        double extent = 0;
        for (int index = 0; index < visible.Count; index++)
        {
            UiElement child = visible[index];
            if (index > 0)
                extent += Spacing + BreakExtent(child);

            extent += horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
        }

        return extent;
    }

    private void DrawSeparators(UiRenderContext context)
    {
        bool first = true;
        foreach (UiElement child in Children)
        {
            if (child.Visibility != UiVisibility.Visible || _isOverflowed.Contains(child))
                continue;

            bool leads = first;
            first = false;

            // The bar opens no group it did not also open a gap for: the first
            // item on it has nothing to be separated from.
            if (leads || GetBreakBefore(child) != UiToolbarBreak.Separator)
                continue;

            if (Orientation == UiToolbarOrientation.Horizontal)
            {
                double x = child.Bounds.Left - Math.Max(2, Spacing / 2);
                double top = Overflow == UiToolbarOverflow.Wrap ? child.Bounds.Top : Bounds.Top + Padding + 4;
                double height = Overflow == UiToolbarOverflow.Wrap ? child.Bounds.Height : Math.Max(0, Bounds.Height - (Padding + 4) * 2);
                context.RenderList.FillRect(new BRect(x, top, 1, height), SeparatorColor);
            }
            else
            {
                double y = child.Bounds.Top - Math.Max(2, Spacing / 2);
                double left = Overflow == UiToolbarOverflow.Wrap ? child.Bounds.Left : Bounds.Left + Padding + 4;
                double width = Overflow == UiToolbarOverflow.Wrap ? child.Bounds.Width : Math.Max(0, Bounds.Width - (Padding + 4) * 2);
                context.RenderList.FillRect(new BRect(left, y, width, 1), SeparatorColor);
            }
        }
    }

    private BRect GetContentBounds(BRect bounds) =>
        new(
            bounds.Left + Padding,
            bounds.Top + Padding,
            Math.Max(0, bounds.Width - Padding * 2),
            Math.Max(0, bounds.Height - Padding * 2));

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
