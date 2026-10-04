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
    /// were replaced. The event's element is the parent whose children changed.
    /// </summary>
    /// <remarks>
    /// A root has no parent, so when a root itself is shown, collapsed or hidden from assistive
    /// technology the event names the root: everything it exposes changed. Adding, removing and moving
    /// roots raises none, since hosts follow the session's roots themselves. Changes made while
    /// <see cref="UiSession.DispatchInput"/> or <see cref="UiSession.RenderFrame"/> runs are raised once
    /// per element when it returns; others are raised as they happen. See Broiler.UI ADR 0028.
    /// </remarks>
    StructureChanged,
    StatusAnnounced,
}

