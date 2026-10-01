namespace Broiler.UI.ListView;

/// <summary>
/// Defines the presenter and template boundary for measuring, rendering, and providing semantics for list items.
/// </summary>
public interface IUiListItemPresenter
{
    /// <summary>
    /// Computes the layout height of an item given density and available content width.
    /// </summary>
    double GetItemHeight(UiListItem? item, UiDensity density, double availableWidth);

    /// <summary>
    /// Renders the item into the provided cell bounds.
    /// </summary>
    void Render(UiListItemRenderContext context);

    /// <summary>
    /// Creates the semantic accessibility node for the item.
    /// </summary>
    UiSemanticNode CreateSemanticNode(UiListItemSemanticContext context);
}
