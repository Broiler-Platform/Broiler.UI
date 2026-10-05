using Broiler.Graphics.Geometry;
using Broiler.UI;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;

namespace Broiler.UI.Forms.Standard;

/// <summary>
/// Wraps scroll content to the viewport width using standard viewport-constrained scrolling. When the
/// viewport shrinks, for example because feedback appeared below a form's actions, a focused control
/// that was on screen is kept on screen. The content is laid out 1 DIP short of each edge of the
/// viewport (<see cref="StandardScrollView.HorizontalContentInset"/> and
/// <see cref="StandardScrollView.VerticalContentInset"/>), so a field as wide as the form, or one the
/// keyboard brings into view at the top or the bottom, shows its whole focus ring rather than losing
/// the outer half to the clip. While a scrollbar shows, a further 2 DIP lie between that room and the
/// bar (<see cref="StandardScrollView.ScrollbarGap"/>), so a ring beside the bar does not meet its thumb.
/// </summary>
public sealed class FormViewport : UiElement
{
    /// <summary>
    /// The outer half of the 2 DIP frame an edit, spin box or rich edit strokes on its edge while it has
    /// focus, the widest stroke a standard field draws outside itself.
    /// </summary>
    private const double FocusRingRoom = 1;

    /// <summary>
    /// The space between the ring's room and a shown bar, as a list keeps between its selection and its
    /// thumb. A contrast palette draws the thumb in its text color, which a ring that met it would merge with.
    /// </summary>
    private const double GapBesideBar = 2;

    public StandardScrollView Scroll { get; } = new()
    {
        Constraint = UiScrollConstraint.ConstrainWidth,
        HorizontalContentInset = FocusRingRoom,
        VerticalContentInset = FocusRingRoom,
        ScrollbarGap = GapBesideBar,
    };

    public FormViewport(UiElement content)
    {
        Scroll.AddChild(content);
        AddChild(Scroll);
    }

    protected override BSize MeasureCore(BSize availableSize) => Scroll.Measure(availableSize);

    protected override void ArrangeCore(BRect finalRect)
    {
        // The part of the focused control that was on screen, from its own top, before this layout.
        BRect before = Scroll.ContentBounds;
        UiElement? focused = Session?.FocusedElement is { } element && element.IsDescendantOf(Scroll) ? element : null;
        double shownFrom = 0;
        double shownTo = 0;
        if (focused is not null && !before.IsEmpty)
        {
            BRect bounds = focused.Bounds;
            shownFrom = Math.Max(bounds.Top, before.Top) - bounds.Top;
            shownTo = Math.Min(bounds.Bottom, before.Bottom) - bounds.Top;
        }

        Scroll.Arrange(finalRect);

        BRect after = Scroll.ContentBounds;
        if (focused is null || shownTo <= shownFrom || (after.Height >= before.Height && after.Width >= before.Width))
            return;

        // Scroll no more than it takes to show that part again, with the room for its ring, and never past
        // its top: the same rule FormSurface.Reveal uses for a field taller than the viewport. Focus stays
        // where it is.
        double room = Scroll.VerticalContentInset;
        double showTop = after.Top + room;
        double showBottom = after.Bottom - room;
        BRect now = focused.Bounds;
        double top = now.Top + shownFrom;
        double bottom = now.Top + Math.Min(shownTo, now.Height);
        double delta = bottom > showBottom ? Math.Min(bottom - showBottom, top - showTop)
            : top < showTop ? top - showTop
            : 0;

        // The offset change invalidates the scroll view's arrange, but this arrange is the one that
        // would act on it, so the content is placed at the new offset now.
        if (delta != 0 && Scroll.ScrollBy(0, delta))
            Scroll.Arrange(finalRect);
    }
}
