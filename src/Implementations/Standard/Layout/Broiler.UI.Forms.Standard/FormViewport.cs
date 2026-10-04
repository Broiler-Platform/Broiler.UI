using Broiler.Graphics.Geometry;
using Broiler.UI;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;

namespace Broiler.UI.Forms.Standard;

/// <summary>
/// Wraps scroll content to the viewport width using standard viewport-constrained scrolling. When the
/// viewport shrinks, for example because feedback appeared below a form's actions, a focused control
/// that was on screen is kept on screen.
/// </summary>
public sealed class FormViewport : UiElement
{
    public StandardScrollView Scroll { get; } = new() { Constraint = UiScrollConstraint.ConstrainWidth };

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

        // Scroll no more than it takes to show that part again, and never past its top: the same rule
        // FormSurface.Reveal uses for a field taller than the viewport. Focus stays where it is.
        BRect now = focused.Bounds;
        double top = now.Top + shownFrom;
        double bottom = now.Top + Math.Min(shownTo, now.Height);
        double delta = bottom > after.Bottom ? Math.Min(bottom - after.Bottom, top - after.Top)
            : top < after.Top ? top - after.Top
            : 0;

        // The offset change invalidates the scroll view's arrange, but this arrange is the one that
        // would act on it, so the content is placed at the new offset now.
        if (delta != 0 && Scroll.ScrollBy(0, delta))
            Scroll.Arrange(finalRect);
    }
}
