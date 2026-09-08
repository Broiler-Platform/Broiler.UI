using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

/// <summary>Which way a <see cref="StandardScrollbar"/> runs.</summary>
public enum StandardScrollbarOrientation
{
    /// <summary>Down the right-hand edge, scrolling the content vertically.</summary>
    Vertical = 0,

    /// <summary>Along the bottom edge, scrolling the content horizontally.</summary>
    Horizontal,
}

/// <summary>
/// One scrollbar for a control that scrolls its own content: where the track
/// goes, how big the thumb is, what a press or a drag means, and how to paint
/// it.
///
/// A helper the control owns rather than an element of its own, because that is
/// what the Standard controls that already scroll are shaped like — a list, a
/// rich edit and a formatted code view each keep their content in one rectangle
/// and their bar in another, and none of them has a child element for it. Three
/// of them had also written this arithmetic out separately, which is three
/// places for a thumb to end up a pixel from the end of its track.
///
/// The two orientations are one class because the arithmetic is the same one
/// twice with the axes swapped. Writing it out again along X is how the vertical
/// and the horizontal bar of the same control come to disagree about what the
/// end of a track means.
///
/// It holds no session and no element, so what it does is arithmetic and
/// painting, and it can be tested by asserting rectangles. Capturing the pointer
/// stays with the control, which is the only thing that has a session to capture
/// with.
/// </summary>
public sealed class StandardScrollbar
{
    private double _thickness = 12;
    private double _minimumThumbLength = 18;
    private double _dragOffsetWithinThumb;

    public StandardScrollbar(StandardScrollbarOrientation orientation) => Orientation = orientation;

    public StandardScrollbarOrientation Orientation { get; }

    /// <summary>
    /// The paint of the track and of the thumb. Set from a theme by the
    /// control, like every other colour it owns.
    /// </summary>
    public BColor Track { get; set; } = StandardControlPaint.SurfaceDisabled;

    public BColor Thumb { get; set; } = StandardControlPaint.BorderStrong;

    /// <summary>
    /// Whether the bar may appear at all. False is a host saying this control
    /// never shows one; it is not the same as there being nothing to scroll,
    /// which <see cref="IsVisible"/> answers.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>How thick the bar is, and how much it takes from the content across its axis.</summary>
    public double Thickness
    {
        get => _thickness;
        set => _thickness = NonNegative(value);
    }

    /// <summary>
    /// The shortest the thumb may be drawn.
    ///
    /// Without a floor the thumb of a long document is a line nobody can hit —
    /// its length is the viewport's share of the extent, and that share goes to
    /// nothing as the extent grows.
    /// </summary>
    public double MinimumThumbLength
    {
        get => _minimumThumbLength;
        set => _minimumThumbLength = NonNegative(value);
    }

    /// <summary>True when the last layout found something to scroll along this axis.</summary>
    public bool IsVisible { get; internal set; }

    /// <summary>True while this bar's thumb is being dragged.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>Where the bar goes, or empty when it is not showing.</summary>
    public BRect TrackBounds { get; internal set; }

    /// <summary>The length of the content along this axis.</summary>
    public double Extent { get; internal set; }

    /// <summary>The length of the window onto it.</summary>
    public double Viewport { get; internal set; }

    /// <summary>The largest offset that still shows content, and never negative.</summary>
    public double MaximumOffset => Math.Max(0, Extent - Viewport);

    /// <summary>The thumb for <paramref name="offset"/>, or empty when nothing is showing.</summary>
    public BRect ThumbBounds(double offset)
    {
        if (TrackBounds.IsEmpty || Extent <= 0 || Viewport <= 0)
            return BRect.Empty;

        double track = Along(TrackBounds.Width, TrackBounds.Height);
        double share = Viewport / Math.Max(Viewport, Extent);
        double floor = Math.Min(_minimumThumbLength, track);
        double length = Math.Clamp(track * share, floor, track);
        double travel = Math.Max(0, track - length);
        double start = Along(TrackBounds.Left, TrackBounds.Top) + (MaximumOffset <= 0
            ? 0
            : travel * Math.Clamp(offset / MaximumOffset, 0, 1));

        return Orientation == StandardScrollbarOrientation.Vertical
            ? new BRect(TrackBounds.Left, start, TrackBounds.Width, length)
            : new BRect(start, TrackBounds.Top, length, TrackBounds.Height);
    }

    public void Render(BRenderList list, double offset)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (!IsVisible)
            return;

        StandardControlPaint.FillRounded(list, TrackBounds, Track, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(list, ThumbBounds(offset), Thumb, StandardControlPaint.PillRadius);
    }

    /// <summary>
    /// Answers a press. On the thumb it starts a drag and leaves the offset
    /// where it was; before or after it, it pages towards the press.
    /// </summary>
    /// <returns>False when the press was not on this bar, and the caller should go on handling it.</returns>
    public bool TryPress(BPoint position, double offset, out double newOffset)
    {
        newOffset = offset;
        if (!IsVisible || !TrackBounds.Contains(position))
            return false;

        BRect thumb = ThumbBounds(offset);
        if (thumb.Contains(position))
        {
            IsDragging = true;

            // Where along the thumb the pointer took hold, so the thumb moves
            // with the pointer rather than jumping its own middle under it.
            _dragOffsetWithinThumb = Along(position.X, position.Y) - Along(thumb.Left, thumb.Top);
            return true;
        }

        double page = Viewport * 0.85;
        bool before = Along(position.X, position.Y) < Along(thumb.Left, thumb.Top);
        newOffset = Math.Clamp(offset + (before ? -page : page), 0, MaximumOffset);
        return true;
    }

    /// <summary>Continues a drag. False when none is in progress on this bar.</summary>
    public bool TryDrag(BPoint position, double offset, out double newOffset)
    {
        newOffset = offset;
        if (!IsDragging)
            return false;

        BRect thumb = ThumbBounds(offset);
        double travel = Along(TrackBounds.Width, TrackBounds.Height) - Along(thumb.Width, thumb.Height);
        if (travel <= 0)
            return true;

        double start = Along(position.X, position.Y) - _dragOffsetWithinThumb;
        newOffset =
            Math.Clamp((start - Along(TrackBounds.Left, TrackBounds.Top)) / travel, 0, 1) * MaximumOffset;
        return true;
    }

    /// <summary>Ends a drag. Safe when none was in progress.</summary>
    public void EndDrag()
    {
        IsDragging = false;
        _dragOffsetWithinThumb = 0;
    }

    /// <summary>The component of a pair that runs along this bar's axis.</summary>
    private double Along(double x, double y) =>
        Orientation == StandardScrollbarOrientation.Vertical ? y : x;

    private static double NonNegative(double value) =>
        double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "The value must be finite and non-negative.");
}
