using Broiler.Graphics.Geometry;
using Broiler.UI;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;

namespace Broiler.UI.Forms.Standard;

/// <summary>Wraps scroll content to the viewport width using standard viewport-constrained scrolling.</summary>
public sealed class FormViewport : UiElement
{
    public StandardScrollView Scroll { get; } = new() { Constraint = UiScrollConstraint.ConstrainWidth };

    public FormViewport(UiElement content)
    {
        Scroll.AddChild(content);
        AddChild(Scroll);
    }

    protected override BSize MeasureCore(BSize availableSize) => Scroll.Measure(availableSize);

    protected override void ArrangeCore(BRect finalRect) => Scroll.Arrange(finalRect);
}
