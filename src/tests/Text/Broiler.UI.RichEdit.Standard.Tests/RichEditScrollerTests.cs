using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Input.Touch;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Vertical scrolling as arithmetic: where the bar goes, what a press or a drag
/// means, and when a touch stops being a tap.
/// </summary>
public sealed class RichEditScrollerTests
{
    /// <summary>A 200x100 window at (10, 20).</summary>
    private static readonly BRect Window = new(10, 20, 200, 100);

    private static RichEditScrollMetrics Metrics(
        double extent,
        RichEditScrollPolicy policy = RichEditScrollPolicy.Auto,
        double thickness = 12,
        double minimumThumb = 18) =>
        new(policy, Window, extent, thickness, minimumThumb);

    [Fact]
    public void An_Editor_That_Never_Scrolls_Does_Not_Scroll_At_All()
    {
        RichEditScrollMetrics metrics = Metrics(1000, RichEditScrollPolicy.Never);
        var scroller = new RichEditScroller();

        Assert.Equal(0, metrics.MaxOffset);
        Assert.False(metrics.HasScrollbar);
        Assert.False(scroller.ScrollTo(50, metrics));
        Assert.Equal(0, scroller.Offset);
    }

    [Fact]
    public void The_Bar_Shows_When_There_Is_Something_To_Scroll_Or_When_Asked_To()
    {
        Assert.False(Metrics(80).HasScrollbar);
        Assert.True(Metrics(300).HasScrollbar);
        Assert.True(Metrics(80, RichEditScrollPolicy.Always).HasScrollbar);
    }

    [Fact]
    public void Every_Offset_Is_Clamped_To_What_Can_Be_Scrolled()
    {
        RichEditScrollMetrics metrics = Metrics(300);
        var scroller = new RichEditScroller();

        Assert.False(scroller.ScrollTo(-5, metrics));
        Assert.True(scroller.ScrollTo(1000, metrics));
        Assert.Equal(200, scroller.Offset);
        Assert.False(scroller.ScrollTo(200, metrics));

        // Content that shrinks takes the offset back with it.
        scroller.Clamp(Metrics(150));
        Assert.Equal(50, scroller.Offset);
    }

    [Fact]
    public void The_Bar_Lies_Over_The_Right_Edge_Of_The_Window()
    {
        Assert.Equal(new BRect(Window.Right - 12, Window.Top, 12, Window.Height), Metrics(300).Track);

        // Out-of-range thicknesses are taken as the nearest that makes sense
        // rather than refused: the property is a plain setter on the control.
        Assert.Equal(0, Metrics(300, thickness: -3).Track.Width);
        Assert.Equal(Window.Width, Metrics(300, thickness: 500).Track.Width);
    }

    [Fact]
    public void The_Thumb_Keeps_Its_Minimum_Length_And_Reaches_The_End_Of_The_Track()
    {
        RichEditScrollMetrics metrics = Metrics(100_000);

        BRect top = metrics.Thumb(0);
        BRect bottom = metrics.Thumb(metrics.MaxOffset);

        Assert.Equal(18, top.Height, 6);
        Assert.Equal(metrics.Track.Top, top.Top, 6);
        Assert.Equal(metrics.Track.Bottom, bottom.Bottom, 6);

        // With nothing to scroll, the thumb is the whole track.
        Assert.Equal(metrics.Track.Height, Metrics(50, RichEditScrollPolicy.Always).Thumb(0).Height);
    }

    [Fact]
    public void A_Press_Beside_The_Thumb_Pages_And_A_Press_On_It_Takes_Hold()
    {
        RichEditScrollMetrics metrics = Metrics(1000);
        var scroller = new RichEditScroller();
        double x = metrics.Track.Left + 2;

        Assert.True(scroller.TryPressScrollbar(new BPoint(x, Window.Bottom - 2), metrics));
        Assert.Equal(85, scroller.Offset, 6);
        Assert.False(scroller.IsDraggingThumb);

        BRect thumb = metrics.Thumb(scroller.Offset);
        Assert.True(scroller.TryPressScrollbar(new BPoint(x, thumb.Top + 1), metrics));
        Assert.True(scroller.IsDraggingThumb);
        Assert.Equal(85, scroller.Offset, 6);

        scroller.DragThumb(Window.Bottom + 500, metrics);
        Assert.Equal(metrics.MaxOffset, scroller.Offset, 6);

        scroller.DragThumb(Window.Top - 500, metrics);
        Assert.Equal(0, scroller.Offset, 6);

        scroller.EndThumbDrag();
        Assert.False(scroller.IsDraggingThumb);
    }

    [Fact]
    public void A_Press_Off_The_Bar_Is_Left_To_The_Text()
    {
        var scroller = new RichEditScroller();

        Assert.False(scroller.TryPressScrollbar(new BPoint(Window.Left + 5, Window.Top + 5), Metrics(1000)));
        Assert.False(scroller.TryPressScrollbar(new BPoint(Window.Right - 2, Window.Top + 5), Metrics(50)));
    }

    [Fact]
    public void A_Contact_Scrolls_Once_It_Has_Travelled_Far_Enough_To_Be_A_Drag()
    {
        RichEditScrollMetrics metrics = Metrics(1000);
        var scroller = new RichEditScroller();

        Assert.False(scroller.Touch(1, TouchContactState.Pressed, new BPoint(50, 80), metrics));
        Assert.False(scroller.Touch(1, TouchContactState.Moved, new BPoint(50, 77), metrics));
        Assert.Equal(0, scroller.Offset);

        // Another finger does not steer this gesture.
        Assert.False(scroller.Touch(2, TouchContactState.Moved, new BPoint(50, 10), metrics));

        Assert.True(scroller.Touch(1, TouchContactState.Moved, new BPoint(50, 60), metrics));
        Assert.Equal(17, scroller.Offset, 6);
        Assert.True(scroller.Touch(1, TouchContactState.Released, new BPoint(50, 60), metrics));

        // The gesture is over, so the next contact starts a new one.
        Assert.False(scroller.Touch(3, TouchContactState.Pressed, new BPoint(50, 60), metrics));
        Assert.False(scroller.Touch(3, TouchContactState.Cancelled, new BPoint(50, 60), metrics));
    }

    [Fact]
    public void A_Tap_Is_Not_A_Scroll()
    {
        var scroller = new RichEditScroller();

        Assert.False(scroller.Touch(1, TouchContactState.Pressed, new BPoint(50, 80), Metrics(1000)));
        Assert.False(scroller.Touch(1, TouchContactState.Released, new BPoint(52, 81), Metrics(1000)));
    }

    [Fact]
    public void Zooming_Scales_The_Offset_Without_Clamping_It()
    {
        var scroller = new RichEditScroller();
        scroller.ScrollTo(150, Metrics(300));

        scroller.ScaleOffset(2);

        // Past the old maximum: the content grew with the zoom, and the next
        // layout is what knows by how much.
        Assert.Equal(300, scroller.Offset);
    }
}
