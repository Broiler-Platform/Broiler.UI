using Broiler.Graphics.Geometry;

namespace Broiler.UI.ListView;

/// <summary>
/// Provides context for generating the accessibility semantic node of an individual list item cell.
/// </summary>
public sealed class UiListItemSemanticContext
{
    public required UiListItem Item { get; init; }
    public required UiListItemState State { get; init; }
    public required BRect Bounds { get; init; }
    public required int Index { get; init; }
}
