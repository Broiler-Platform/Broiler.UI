namespace Broiler.UI.ListView;

/// <summary>
/// Represents the visual and interactive state of a list item within a list view.
/// </summary>
public readonly record struct UiListItemState(
    bool IsSelected,
    bool IsFocused,
    bool IsRead,
    int Index,
    UiDensity Density = UiDensity.Comfortable);
