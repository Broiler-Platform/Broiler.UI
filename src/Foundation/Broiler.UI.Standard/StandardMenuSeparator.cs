using System;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.Standard;

/// <summary>Shared separator metrics and painting for menus and editing context menus.</summary>
public static class StandardMenuSeparator
{
    public const double Height = 7;
    public const double Inset = 10;

    public static void Draw(UiRenderContext context, BRect bounds, BColor color, bool vertical = false)
    {
        BRect line = vertical
            ? new BRect(bounds.Left + Math.Floor(bounds.Width / 2), bounds.Top + 4, 1, Math.Max(0, bounds.Height - 8))
            : new BRect(bounds.Left + Inset, bounds.Top + Math.Floor(bounds.Height / 2), Math.Max(0, bounds.Width - Inset * 2), 1);
        context.RenderList.FillRect(line, color);
    }
}
