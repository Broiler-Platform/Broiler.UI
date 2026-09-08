using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

/// <summary>
/// A vertical scrollbar for a control that scrolls its own content: where the
/// track goes, how big the thumb is, what a press or a drag means, and how to
/// paint it.
///
/// A helper the control owns rather than an element of its own, because that is
/// what the Standard controls that already scroll are shaped like — a list, a
/// rich edit and a formatted code view each keep their content in one rectangle
/// and their bar in another, and none of them has a child element for it. Three
/// of them had also written this arithmetic out separately, which is three
/// places for a thumb to end up a pixel from the bottom of its track.
///
/// It holds no session and no element, so what it does is arithmetic and
/// painting, and it can be tested by asserting rectangles. Capturing the pointer
/// stays with the control, which is the only thing that has a session to capture
/// with.
///
/// It shows itself only when the content does not fit. A bar over a pane with
/// four rows in it is chrome that reports nothing, and it costs the pane its
/// width for the privilege.
/// </summary>
public sealed class StandardVerticalScrollbar
{
    private double _thickness = 12;
    private double _minimumThumbLength = 18;
    private double _dragOffsetWithinThumb;

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

    /// <summary>How wide the bar is, and how much width it takes from the content.</summary>
    public double Thickness
    {
        get => _thickness;
        set => _thickness = NonNegative(value);
    }

    /// <summary>
    /// The shortest the thumb may be drawn.
    ///
    /// Without a floor the thumb of a long document is a line nobody can hit —
    /// its height is the viewport's share of the extent, and that share goes to
    /// nothing as the extent grows.
    /// </summary>
    public double MinimumThumbLength
    {
        get => _minimumThumbLength;
        set => _minimumThumbLength = NonNegative(value);
    }

    /// <summary>True when the last <see cref="Layout"/> found something to scroll.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>True while the thumb is being dragged.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>Where the control's own content goes: its bounds, less the bar.</summary>
    public BRect ContentBounds { get; private set; }

    /// <summary>Where the bar goes, or empty when it is not showing.</summary>
    public BRect TrackBounds { get; private set; }

    /// <summary>The height of the content the control has, in layout units.</summary>
    public double Extent { get; private set; }

    /// <summary>The height of the window onto it.</summary>
    public double Viewport => ContentBounds.Height;

    /// <summary>The largest offset that still shows content, and never negative.</summary>
    public double MaximumOffset => Math.Max(0, Extent - Viewport);

    /// <summary>
    /// Splits <paramref name="bounds"/> between the content and the bar, and
    /// returns the content's share. Call it from the control's arrange, before
    /// anything that depends on how much room the content has.
    /// </summary>
    /// <param name="bounds">The control's arranged bounds.</param>
    /// <param name="extent">The full height of the content being scrolled.</param>
    public BRect Layout(BRect bounds, double extent)
    {
        Extent = Math.Max(0, extent);

        // Measured against the whole width, not the content's, because the
        // content's width is what this is about to decide: asking whether the
        // bar is needed after taking its width away is a question that answers
        // itself differently every frame.
        IsVisible = IsEnabled && _thickness > 0 && bounds.Height > 0 && Extent > bounds.Height;

        double thickness = IsVisible ? Math.Min(_thickness, Math.Max(0, bounds.Width)) : 0;
        ContentBounds = new BRect(
            bounds.Left, bounds.Top, Math.Max(0, bounds.Width - thickness), bounds.Height);
        TrackBounds = IsVisible
            ? new BRect(ContentBounds.Right, bounds.Top, thickness, bounds.Height)
            : BRect.Empty;

        return ContentBounds;
    }

    /// <summary>The thumb for <paramref name="offset"/>, or empty when nothing is showing.</summary>
    public BRect ThumbBounds(double offset)
    {
        if (TrackBounds.IsEmpty || Extent <= 0)
            return BRect.Empty;

        double share = Viewport / Math.Max(Viewport, Extent);
        double floor = Math.Min(_minimumThumbLength, TrackBounds.Height);
        double height = Math.Clamp(TrackBounds.Height * share, floor, TrackBounds.Height);
        double travel = Math.Max(0, TrackBounds.Height - height);
        double top = TrackBounds.Top + (MaximumOffset <= 0
            ? 0
            : travel * Math.Clamp(offset / MaximumOffset, 0, 1));

        return new BRect(TrackBounds.Left, top, TrackBounds.Width, height);
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
    /// where it was; above or below it, it pages towards the press.
    /// </summary>
    /// <returns>False when the press was not on the bar, and the control should go on handling it.</returns>
    public bool TryPress(BPoint position, double offset, out double newOffset)
    {
        newOffset = offset;
        if (!IsVisible || !TrackBounds.Contains(position))
            return false;

        BRect thumb = ThumbBounds(offset);
        if (thumb.Contains(position))
        {
            IsDragging = true;

            // Where in the thumb the pointer took hold, so the thumb moves with
            // the pointer rather than jumping its own middle under it.
            _dragOffsetWithinThumb = position.Y - thumb.Top;
            return true;
        }

        double page = Viewport * 0.85;
        newOffset = Math.Clamp(
            offset + (position.Y < thumb.Top ? -page : page), 0, MaximumOffset);
        return true;
    }

    /// <summary>Continues a drag. False when none is in progress.</summary>
    public bool TryDrag(BPoint position, double offset, out double newOffset)
    {
        newOffset = offset;
        if (!IsDragging)
            return false;

        BRect thumb = ThumbBounds(offset);
        double travel = Math.Max(0, TrackBounds.Height - thumb.Height);
        if (travel <= 0)
            return true;

        double top = position.Y - _dragOffsetWithinThumb;
        newOffset = Math.Clamp((top - TrackBounds.Top) / travel, 0, 1) * MaximumOffset;
        return true;
    }

    /// <summary>Ends a drag. Safe when none was in progress.</summary>
    public void EndDrag()
    {
        IsDragging = false;
        _dragOffsetWithinThumb = 0;
    }

    private static double NonNegative(double value) =>
        double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "The value must be finite and non-negative.");
}
