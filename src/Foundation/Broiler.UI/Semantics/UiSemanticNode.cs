using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI;

public sealed record UiSemanticNode(
    UiSemanticRole Role,
    string Name,
    BRect Bounds,
    UiSemanticState State,
    IReadOnlyList<UiSemanticNode> Children,
    UiSemanticTextInfo? TextInfo = null,
    long Id = 0)
{
    /// <summary>
    /// Text that describes the element beyond its name, or null. <see cref="UiElement.GetSemanticNode"/>
    /// fills it from the element's relations: the text of a shown <see cref="UiElement.ErrorMessage"/>
    /// first, then that of <see cref="UiElement.DescribedBy"/>, then any description the control gives
    /// itself. A host maps it to its full-description property, so a client that never follows the
    /// described-by relation still reads a field's error. See Broiler.UI ADR 0028.
    /// </summary>
    public string? Description { get; init; }
}
