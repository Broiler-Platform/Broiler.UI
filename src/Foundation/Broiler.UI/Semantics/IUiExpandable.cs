namespace Broiler.UI;

/// <summary>
/// Something that shows and hides content of its own: a disclosure section, a drop-down, a menu.
/// </summary>
/// <remarks>
/// This is the action half of expand and collapse. The state half is the
/// <see cref="UiSemanticState.Expanded"/> or <see cref="UiSemanticState.Collapsed"/> flag on a
/// semantic node, which is what a host reads to decide whether to offer an expand/collapse pattern at
/// all. The host then acts through this interface: on the element itself when it implements it, and
/// otherwise on the element's <see cref="UiElement.Discloses"/> target. See Broiler.UI ADR 0028.
/// </remarks>
public interface IUiExpandable
{
    /// <summary>Whether the content is currently shown.</summary>
    bool IsExpanded { get; }

    /// <summary>Shows the content.</summary>
    /// <returns>False when the content was already shown or cannot be shown now.</returns>
    bool Expand();

    /// <summary>Hides the content.</summary>
    /// <returns>False when the content was already hidden or cannot be hidden.</returns>
    bool Collapse();
}
