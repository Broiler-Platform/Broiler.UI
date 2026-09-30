using Broiler.Graphics.Geometry;

namespace Broiler.UI;

/// <summary>
/// Defines a scrollable container that can bring a child element or target bounds into view.
/// </summary>
public interface IUiScrollable
{
    /// <summary>Scrolls the container as necessary so the target bounds become visible.</summary>
    /// <returns>True if scrolling occurred; false if already visible or unable to scroll.</returns>
    bool MakeVisible(BRect targetRect);
}
