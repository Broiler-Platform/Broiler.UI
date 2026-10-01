using System;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input.Touch;
using Broiler.UI.Standard;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// What vertical scrolling is measured against at one moment: the policy, the
/// window onto the content, how tall the content is, and how the bar is drawn.
/// </summary>
/// <remarks>
/// The bar overlays the right edge of the window rather than taking width from
/// the text column, so showing or hiding it never rewraps the document. That is
/// the difference from <see cref="StandardScrollbar"/>, which carves its bar out
/// of the content; the rich edit's text would reflow every time a document grew
/// past the window if it used it.
/// </remarks>
internal readonly record struct RichEditScrollMetrics(
    RichEditScrollPolicy Policy,
    BRect Inner,
    double Extent,
    double Thickness,
    double MinimumThumbLength)
{
    /// <summary>The height of the window onto the content.</summary>
    public double Viewport => Inner.Height;

    /// <summary>
    /// The largest offset that still shows content. Zero when the policy is
    /// <see cref="RichEditScrollPolicy.Never"/>: that editor does not scroll at
    /// all, by any means, rather than only losing its bar.
    /// </summary>
    public double MaxOffset => Policy == RichEditScrollPolicy.Never
        ? 0
        : Math.Max(0, Extent - Viewport);

    public bool HasScrollbar => Policy == RichEditScrollPolicy.Always ||
                                (Policy == RichEditScrollPolicy.Auto && MaxOffset > 0);

    public BRect Track
    {
        get
        {
            double thickness = Math.Clamp(Thickness, 0, Inner.Width);
            return new BRect(Inner.Right - thickness, Inner.Top, thickness, Inner.Height);
        }
    }

    public double Clamp(double offset) => Math.Clamp(offset, 0, MaxOffset);

    /// <summary>The thumb for <paramref name="offset"/>, or empty when the track has no height.</summary>
    public BRect Thumb(double offset)
    {
        BRect track = Track;
        if (track.Height <= 0)
            return BRect.Empty;

        double maxOffset = MaxOffset;
        double thumbHeight = maxOffset <= 0
            ? track.Height
            : Math.Clamp(track.Height * (Viewport / Math.Max(Viewport, Extent)),
                Math.Min(MinimumThumbLength, track.Height), track.Height);
        double top = track.Top;
        if (maxOffset > 0)
            top += (track.Height - thumbHeight) * (offset / maxOffset);
        return new BRect(track.Left, top, track.Width, thumbHeight);
    }
}

internal readonly record struct RichEditHorizontalScrollMetrics(
    RichEditScrollPolicy Policy,
    BRect Inner,
    double Extent,
    double Thickness,
    double MinimumThumbLength)
{
    public double Viewport => Inner.Width;

    public double MaxOffset => Policy == RichEditScrollPolicy.Never
        ? 0
        : Math.Max(0, Extent - Viewport);

    public bool HasScrollbar => Policy == RichEditScrollPolicy.Always ||
                                (Policy == RichEditScrollPolicy.Auto && MaxOffset > 0);

    public BRect Track
    {
        get
        {
            double thickness = Math.Clamp(Thickness, 0, Inner.Height);
            return new BRect(Inner.Left, Inner.Bottom - thickness, Inner.Width, thickness);
        }
    }

    public double Clamp(double offset) => Math.Clamp(offset, 0, MaxOffset);

    public BRect Thumb(double offset)
    {
        BRect track = Track;
        if (track.Width <= 0)
            return BRect.Empty;

        double maxOffset = MaxOffset;
        double thumbWidth = maxOffset <= 0
            ? track.Width
            : Math.Clamp(track.Width * (Viewport / Math.Max(Viewport, Extent)),
                Math.Min(MinimumThumbLength, track.Width), track.Width);
        double left = track.Left;
        if (maxOffset > 0)
            left += (track.Width - thumbWidth) * (offset / maxOffset);
        return new BRect(left, track.Top, thumbWidth, track.Height);
    }
}

/// <summary>
/// A rich edit's vertical and horizontal scroll position and the interactions that move it: the
/// wheel and the caret move it by an amount, the scrollbar by a press or a drag
/// of its thumb, and touch by a drag of the content once it has travelled far
/// enough to be a drag rather than a tap.
/// </summary>
/// <remarks>
/// It holds no element and no session. Each operation is given the
/// <see cref="RichEditScrollMetrics"/> of the moment, reports whether the
/// interaction was its to handle, and leaves the offset where it belongs; the
/// control invalidates when the offset moved.
/// </remarks>
internal sealed class RichEditScroller
{
    /// <summary>How far a contact travels before it scrolls instead of being left to be a tap.</summary>
    public const double TouchScrollThreshold = 6;

    private double _thumbGrabOffset;
    private double _horizontalThumbGrabOffset;
    private long? _touchContactId;
    private BPoint _touchStart;
    private BPoint _touchLast;
    private bool _isTouchScrolling;

    /// <summary>How far the content is scrolled up.</summary>
    public double Offset { get; private set; }

    /// <summary>How far the content is scrolled horizontally.</summary>
    public double OffsetX { get; private set; }

    /// <summary>True while the vertical scrollbar thumb is being dragged.</summary>
    public bool IsDraggingThumb { get; private set; }

    /// <summary>True while the horizontal scrollbar thumb is being dragged.</summary>
    public bool IsDraggingHorizontalThumb { get; private set; }

    /// <summary>
    /// Moves to <paramref name="offset"/>, clamped to what there is to scroll.
    /// </summary>
    /// <returns>True when the offset changed.</returns>
    public bool ScrollTo(double offset, in RichEditScrollMetrics metrics)
    {
        double clamped = metrics.Clamp(offset);
        if (clamped == Offset)
            return false;

        Offset = clamped;
        return true;
    }

    /// <summary>Pulls the offset back inside what there is to scroll, after the content changed height.</summary>
    public void Clamp(in RichEditScrollMetrics metrics) => Offset = metrics.Clamp(Offset);

    /// <summary>
    /// Scales the offset with the content, unclamped, when the zoom changes: the
    /// content height scales with the zoom, so the reader stays on the passage
    /// they were reading instead of being thrown back towards the top of a
    /// document that just grew underneath them. The next layout clamps it.
    /// </summary>
    public void ScaleOffset(double factor) => Offset *= factor;

    /// <summary>
    /// Answers a press on the scrollbar: on the thumb it starts a drag, before or
    /// after it it pages towards the press.
    /// </summary>
    /// <returns>False when the press was not on a showing scrollbar.</returns>
    public bool TryPressScrollbar(BPoint position, in RichEditScrollMetrics metrics)
    {
        if (!metrics.HasScrollbar || !metrics.Track.Contains(position))
            return false;

        BRect thumb = metrics.Thumb(Offset);
        if (thumb.Contains(position))
        {
            IsDraggingThumb = true;
            _thumbGrabOffset = position.Y - thumb.Top;
        }
        else
        {
            double page = metrics.Viewport * 0.85;
            ScrollTo(Offset + (position.Y < thumb.Top ? -page : page), metrics);
        }

        return true;
    }

    /// <summary>Continues a thumb drag to <paramref name="pointerY"/>.</summary>
    public void DragThumb(double pointerY, in RichEditScrollMetrics metrics)
    {
        BRect track = metrics.Track;
        BRect thumb = metrics.Thumb(Offset);
        double travel = track.Height - thumb.Height;
        if (travel <= 0)
            return;

        double normalized = (pointerY - _thumbGrabOffset - track.Top) / travel;
        ScrollTo(Math.Clamp(normalized, 0, 1) * metrics.MaxOffset, metrics);
    }

    public void EndThumbDrag()
    {
        IsDraggingThumb = false;
        IsDraggingHorizontalThumb = false;
    }

    public bool ScrollToX(double offset, in RichEditHorizontalScrollMetrics metrics)
    {
        double clamped = metrics.Clamp(offset);
        if (clamped == OffsetX)
            return false;

        OffsetX = clamped;
        return true;
    }

    public void ClampX(in RichEditHorizontalScrollMetrics metrics) => OffsetX = metrics.Clamp(OffsetX);

    public bool TryPressHorizontalScrollbar(BPoint position, in RichEditHorizontalScrollMetrics metrics)
    {
        if (!metrics.HasScrollbar || !metrics.Track.Contains(position))
            return false;

        BRect thumb = metrics.Thumb(OffsetX);
        if (thumb.Contains(position))
        {
            IsDraggingHorizontalThumb = true;
            _horizontalThumbGrabOffset = position.X - thumb.Left;
        }
        else
        {
            double page = metrics.Viewport * 0.85;
            ScrollToX(OffsetX + (position.X < thumb.Left ? -page : page), metrics);
        }

        return true;
    }

    public void DragHorizontalThumb(double pointerX, in RichEditHorizontalScrollMetrics metrics)
    {
        BRect track = metrics.Track;
        BRect thumb = metrics.Thumb(OffsetX);
        double travel = track.Width - thumb.Width;
        if (travel <= 0)
            return;

        double normalized = (pointerX - _horizontalThumbGrabOffset - track.Left) / travel;
        ScrollToX(Math.Clamp(normalized, 0, 1) * metrics.MaxOffset, metrics);
    }

    /// <summary>
    /// Follows one touch contact. A press is never handled, so the session can
    /// still treat the contact as a tap; the contact is handled once it has moved
    /// far enough to scroll, and its release is handled only if it did.
    /// </summary>
    /// <returns>True when the contact was a scroll.</returns>
    public bool Touch(long contactId, TouchContactState state, BPoint position, in RichEditScrollMetrics metrics)
    {
        if (state == TouchContactState.Pressed)
        {
            if (_touchContactId is not null)
                return false;

            _touchContactId = contactId;
            _touchStart = position;
            _touchLast = position;
            _isTouchScrolling = false;
            return false;
        }

        if (_touchContactId != contactId)
            return false;

        if (state == TouchContactState.Moved)
        {
            double totalX = position.X - _touchStart.X;
            double totalY = position.Y - _touchStart.Y;
            if (!_isTouchScrolling && Math.Sqrt((totalX * totalX) + (totalY * totalY)) >= TouchScrollThreshold)
                _isTouchScrolling = true;

            if (!_isTouchScrolling)
            {
                _touchLast = position;
                return false;
            }

            ScrollTo(Offset + (_touchLast.Y - position.Y), metrics);
            _touchLast = position;
            return true;
        }

        if (state is TouchContactState.Released or TouchContactState.Cancelled)
        {
            bool handled = _isTouchScrolling;
            _touchContactId = null;
            _isTouchScrolling = false;
            return handled;
        }

        return false;
    }

    public void PaintScrollbar(BRenderList renderList, in RichEditScrollMetrics metrics, BColor track, BColor thumb)
    {
        if (!metrics.HasScrollbar)
            return;

        StandardControlPaint.FillRounded(renderList, metrics.Track, track, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(renderList, metrics.Thumb(Offset), thumb, StandardControlPaint.PillRadius);
    }

    public void PaintHorizontalScrollbar(BRenderList renderList, in RichEditHorizontalScrollMetrics metrics, BColor track, BColor thumb)
    {
        if (!metrics.HasScrollbar)
            return;

        StandardControlPaint.FillRounded(renderList, metrics.Track, track, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(renderList, metrics.Thumb(OffsetX), thumb, StandardControlPaint.PillRadius);
    }
}

