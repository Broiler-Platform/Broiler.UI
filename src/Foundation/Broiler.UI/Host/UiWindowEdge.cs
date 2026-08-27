namespace Broiler.UI;

/// <summary>
/// The edge or corner a resize drag starts from. Owner-drawn window chrome hit-tests its own
/// border and names the edge; the host hands the drag to the window manager.
/// </summary>
public enum UiWindowEdge
{
    None = 0,
    Left,
    Top,
    Right,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}
