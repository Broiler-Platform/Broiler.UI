using System;

namespace Broiler.UI.ListView;

/// <summary>
/// Event arguments for list item actions, such as activation via Enter key or double-click.
/// </summary>
public sealed class UiListItemEventArgs(UiListItem item) : EventArgs
{
    public UiListItem Item { get; } = item;
}
