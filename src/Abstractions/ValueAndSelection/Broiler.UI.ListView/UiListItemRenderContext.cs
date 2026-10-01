using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;

namespace Broiler.UI.ListView;

/// <summary>
/// Provides context for rendering an individual list item cell.
/// </summary>
public sealed class UiListItemRenderContext
{
    public required BRenderList RenderList { get; init; }
    public required BRect Bounds { get; init; }
    public required UiListItem Item { get; init; }
    public required UiListItemState State { get; init; }
    public required BFontStyle Font { get; init; }
    public required BColor Foreground { get; init; }
    public required BColor SecondaryForeground { get; init; }
    public required BColor Background { get; init; }
    public required BColor SelectedBackground { get; init; }
    public required BColor FocusRing { get; init; }
    public required BColor Accent { get; init; }
    public bool IsHighContrast { get; init; }
}
