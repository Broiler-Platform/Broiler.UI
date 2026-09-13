using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI;

public sealed record UiDragStartRequest(
    UiElement Source,
    BPoint Origin,
    UiDragDataPackage Data,
    UiDragDropEffect AllowedEffects);
