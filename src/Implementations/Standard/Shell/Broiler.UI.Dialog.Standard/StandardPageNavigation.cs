using System;
using Broiler.Graphics.Geometry;
using Broiler.UI.Button.Standard;
using Broiler.UI.Label.Standard;

namespace Broiler.UI.Dialog.Standard;

/// <summary>Arranges page navigation buttons and a page count for use in a preview or status bar.</summary>
public sealed class StandardPageNavigation : UiElement
{
    private readonly StandardButton _previous;
    private readonly StandardButton _next;
    private readonly StandardLabel _caption;

    public StandardPageNavigation(StandardButton previous, StandardButton next, StandardLabel caption)
    {
        _previous = previous;
        _next = next;
        _caption = caption;
        AddChild(previous);
        AddChild(next);
        AddChild(caption);
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double width = Math.Min(276, availableSize.Width);
        double buttonWidth = Math.Min(82, width / 3);
        _previous.Measure(new BSize(buttonWidth, availableSize.Height));
        _next.Measure(new BSize(buttonWidth, availableSize.Height));
        _caption.Measure(new BSize(width - 2 * buttonWidth, availableSize.Height));
        return new BSize(width, availableSize.Height);
    }

    protected override void ArrangeCore(BRect bounds)
    {
        double buttonWidth = Math.Min(82, bounds.Width / 3);
        double gap = Math.Min(4, Math.Max(0, bounds.Width - 2 * buttonWidth) / 2);
        _previous.Arrange(new BRect(bounds.Left, bounds.Top, buttonWidth - gap, bounds.Height));
        _next.Arrange(new BRect(bounds.Left + buttonWidth, bounds.Top, buttonWidth - gap, bounds.Height));
        double captionLeft = bounds.Left + 2 * buttonWidth + gap;
        _caption.Arrange(new BRect(captionLeft, bounds.Top, bounds.Right - captionLeft, bounds.Height));
    }
}
