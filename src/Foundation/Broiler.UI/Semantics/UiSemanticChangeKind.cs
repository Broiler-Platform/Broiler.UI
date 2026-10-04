namespace Broiler.UI;

public enum UiSemanticChangeKind
{
    SubtreeChanged = 0,
    FocusChanged,
    ValueChanged,
    StateChanged,

    /// <summary>
    /// The element's children changed: one was added, removed or moved, shown or collapsed, hidden
    /// from or returned to assistive technology, or a container's virtual children (list items, tabs)
    /// were replaced. The event's element is the parent, or the element itself for a root.
    /// </summary>
    StructureChanged,
    StatusAnnounced,
}

