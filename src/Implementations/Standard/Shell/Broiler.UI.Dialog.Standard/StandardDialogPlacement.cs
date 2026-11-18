using System;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.Dialog.Standard;

/// <summary>Positions a dialog in its owner's viewport, leaving space around its edges.</summary>
public static class StandardDialogPlacement
{
    public static BRect Center(BSize viewport, BSize preferredSize, double margin = 12)
    {
        double width = Math.Min(preferredSize.Width, Math.Max(0, viewport.Width - 2 * margin));
        double height = Math.Min(preferredSize.Height, Math.Max(0, viewport.Height - 2 * margin));
        return new BRect((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }
}
