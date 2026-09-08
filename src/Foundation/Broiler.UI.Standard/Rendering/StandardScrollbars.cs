using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

/// <summary>
/// The pair of scrollbars over one control, and the rectangle they leave its
/// content.
///
/// The two are laid out together rather than one at a time, because whether
/// each is needed depends on the other: content one line too tall takes a
/// vertical bar, that bar takes width from the content, and the content may now
/// be too wide for what is left. Deciding them separately gives a control that
/// grows a bar, loses it on the next frame because the bar it grew made room,
/// and flickers between the two for as long as it is on screen.
///
/// They show themselves only when the content does not fit. A bar over a pane
/// with four rows in it is chrome that reports nothing, and it costs the pane
/// its width for the privilege.
/// </summary>
public sealed class StandardScrollbars
{
    public StandardScrollbar Vertical { get; } = new(StandardScrollbarOrientation.Vertical);

    public StandardScrollbar Horizontal { get; } = new(StandardScrollbarOrientation.Horizontal);

    /// <summary>Where the control's own content goes: its bounds, less whichever bars are showing.</summary>
    public BRect ContentBounds { get; private set; }

    /// <summary>True while either thumb is being dragged.</summary>
    public bool IsDragging => Vertical.IsDragging || Horizontal.IsDragging;

    /// <summary>The paint of both bars, for a control applying a theme.</summary>
    public void ApplyPaint(BColor track, BColor thumb)
    {
        Vertical.Track = track;
        Vertical.Thumb = thumb;
        Horizontal.Track = track;
        Horizontal.Thumb = thumb;
    }

    /// <summary>
    /// Splits <paramref name="bounds"/> between the content and the bars, and
    /// returns the content's share. Call it from the control's arrange, before
    /// anything that depends on how much room the content has.
    /// </summary>
    /// <param name="bounds">The control's arranged bounds.</param>
    /// <param name="extent">The full size of the content being scrolled.</param>
    public BRect Layout(BRect bounds, BSize extent)
    {
        double extentWidth = Math.Max(0, extent.Width);
        double extentHeight = Math.Max(0, extent.Height);

        bool vertical = Vertical.IsEnabled && Vertical.Thickness > 0 &&
            bounds.Height > 0 && extentHeight > bounds.Height;
        bool horizontal = Horizontal.IsEnabled && Horizontal.Thickness > 0 &&
            bounds.Width > 0 && extentWidth > bounds.Width;

        // One bar can be the reason the other is needed. Asked once each way,
        // which settles it: a bar that appears here cannot take away the room
        // that justified the bar it was asked about.
        if (vertical && !horizontal)
        {
            horizontal = Horizontal.IsEnabled && Horizontal.Thickness > 0 &&
                extentWidth > bounds.Width - Vertical.Thickness;
        }
        else if (horizontal && !vertical)
        {
            vertical = Vertical.IsEnabled && Vertical.Thickness > 0 &&
                extentHeight > bounds.Height - Horizontal.Thickness;
        }

        double down = vertical ? Math.Min(Vertical.Thickness, Math.Max(0, bounds.Width)) : 0;
        double across = horizontal ? Math.Min(Horizontal.Thickness, Math.Max(0, bounds.Height)) : 0;

        ContentBounds = new BRect(
            bounds.Left,
            bounds.Top,
            Math.Max(0, bounds.Width - down),
            Math.Max(0, bounds.Height - across));

        Vertical.IsVisible = vertical;
        Vertical.Extent = extentHeight;
        Vertical.Viewport = ContentBounds.Height;
        Vertical.TrackBounds = vertical
            ? new BRect(ContentBounds.Right, bounds.Top, down, ContentBounds.Height)
            : BRect.Empty;

        Horizontal.IsVisible = horizontal;
        Horizontal.Extent = extentWidth;
        Horizontal.Viewport = ContentBounds.Width;

        // Both tracks stop at the content's far edge rather than the control's,
        // so they meet at the corner instead of crossing in it.
        Horizontal.TrackBounds = horizontal
            ? new BRect(bounds.Left, ContentBounds.Bottom, ContentBounds.Width, across)
            : BRect.Empty;

        return ContentBounds;
    }

    public void Render(BRenderList list, BPoint offset)
    {
        Vertical.Render(list, offset.Y);
        Horizontal.Render(list, offset.X);
    }

    /// <summary>Offers a press to each bar. False when it landed on neither.</summary>
    public bool TryPress(BPoint position, BPoint offset, out BPoint newOffset)
    {
        if (Vertical.TryPress(position, offset.Y, out double y))
        {
            newOffset = new BPoint(offset.X, y);
            return true;
        }

        if (Horizontal.TryPress(position, offset.X, out double x))
        {
            newOffset = new BPoint(x, offset.Y);
            return true;
        }

        newOffset = offset;
        return false;
    }

    /// <summary>Continues whichever drag is in progress. False when neither is.</summary>
    public bool TryDrag(BPoint position, BPoint offset, out BPoint newOffset)
    {
        if (Vertical.TryDrag(position, offset.Y, out double y))
        {
            newOffset = new BPoint(offset.X, y);
            return true;
        }

        if (Horizontal.TryDrag(position, offset.X, out double x))
        {
            newOffset = new BPoint(x, offset.Y);
            return true;
        }

        newOffset = offset;
        return false;
    }

    public void EndDrag()
    {
        Vertical.EndDrag();
        Horizontal.EndDrag();
    }
}
