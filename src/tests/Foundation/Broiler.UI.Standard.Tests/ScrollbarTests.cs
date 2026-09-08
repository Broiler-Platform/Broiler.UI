using Broiler.Graphics;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The scrollbar three Standard controls had each written out separately, now
/// written once and along both axes.
///
/// It holds no element and no session, so what it does is arithmetic and
/// painting — which is why these are assertions about rectangles rather than a
/// scene to click in.
/// </summary>
public sealed class ScrollbarTests
{
    private static readonly BRect Bounds = new(10, 20, 200, 100);

    /// <summary>
    /// A bar over a pane whose content fits is chrome that reports nothing, and
    /// it costs the pane its width for the privilege.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Bar_Shows_Only_When_Its_Axis_Does_Not_Fit()
    {
        var bars = new StandardScrollbars();

        Assert.Equal(Bounds, bars.Layout(Bounds, new BSize(40, 40)));
        Assert.False(bars.Vertical.IsVisible);
        Assert.False(bars.Horizontal.IsVisible);
        Assert.Equal(BRect.Empty, bars.Vertical.ThumbBounds(0));

        // Exactly full is still not more than fits.
        bars.Layout(Bounds, new BSize(Bounds.Width, Bounds.Height));
        Assert.False(bars.Vertical.IsVisible);
        Assert.False(bars.Horizontal.IsVisible);

        // Narrow content, so only the height is in question.
        bars.Layout(Bounds, new BSize(40, Bounds.Height + 1));
        Assert.True(bars.Vertical.IsVisible);
        Assert.False(bars.Horizontal.IsVisible);

        bars.Layout(Bounds, new BSize(Bounds.Width + 1, 40));
        Assert.False(bars.Vertical.IsVisible);
        Assert.True(bars.Horizontal.IsVisible);
    }

    [Fact(Timeout = 600000)]
    public void Each_Bar_Takes_Its_Thickness_And_Sits_Beside_The_Content()
    {
        var bars = new StandardScrollbars();
        bars.Vertical.Thickness = 12;
        bars.Horizontal.Thickness = 12;

        BRect content = bars.Layout(Bounds, new BSize(400, 400));

        Assert.Equal(Bounds.Width - 12, content.Width, 3);
        Assert.Equal(Bounds.Height - 12, content.Height, 3);

        Assert.Equal(content.Right, bars.Vertical.TrackBounds.Left, 3);
        Assert.Equal(content.Height, bars.Vertical.TrackBounds.Height, 3);

        Assert.Equal(content.Bottom, bars.Horizontal.TrackBounds.Top, 3);
        Assert.Equal(content.Width, bars.Horizontal.TrackBounds.Width, 3);

        // The tracks stop at the content's far edge, so they meet at the corner
        // rather than crossing in it.
        Assert.Equal(bars.Horizontal.TrackBounds.Right, bars.Vertical.TrackBounds.Left, 3);
        Assert.Equal(bars.Vertical.TrackBounds.Bottom, bars.Horizontal.TrackBounds.Top, 3);
    }

    /// <summary>
    /// Content one unit too wide takes a horizontal bar; that bar takes height,
    /// and the content is now too tall for what is left. Deciding the two
    /// separately gives a pane that grows a bar, loses it because the bar it
    /// grew made room, and flickers between the two forever.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void One_Bar_Can_Be_The_Reason_The_Other_Is_Needed()
    {
        var bars = new StandardScrollbars();

        bars.Layout(Bounds, new BSize(Bounds.Width + 1, Bounds.Height));

        Assert.True(bars.Horizontal.IsVisible);
        Assert.True(bars.Vertical.IsVisible, "the horizontal bar took the height the content needed");

        var other = new StandardScrollbars();
        other.Layout(Bounds, new BSize(Bounds.Width, Bounds.Height + 1));

        Assert.True(other.Vertical.IsVisible);
        Assert.True(other.Horizontal.IsVisible, "the vertical bar took the width the content needed");
    }

    /// <summary>
    /// The thumb is the viewport's share of the extent, and never shorter than
    /// its floor — that share goes to nothing as a document grows, and a thumb
    /// of no length is one nobody can take hold of.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Thumb_Is_The_Viewports_Share_With_A_Floor()
    {
        var bars = new StandardScrollbars();
        bars.Vertical.MinimumThumbLength = 18;

        bars.Layout(Bounds, new BSize(0, 400));
        Assert.Equal(25, bars.Vertical.ThumbBounds(0).Height, 3);

        bars.Layout(Bounds, new BSize(0, 100_000));
        Assert.Equal(18, bars.Vertical.ThumbBounds(0).Height, 3);
    }

    [Theory(Timeout = 600000)]
    [InlineData(StandardScrollbarOrientation.Vertical)]
    [InlineData(StandardScrollbarOrientation.Horizontal)]
    public void The_Thumb_Runs_The_Track_As_The_Offset_Runs_Its_Range(StandardScrollbarOrientation orientation)
    {
        StandardScrollbar bar = Only(orientation, 400);
        bool vertical = orientation == StandardScrollbarOrientation.Vertical;

        BRect atStart = bar.ThumbBounds(0);
        BRect atEnd = bar.ThumbBounds(bar.MaximumOffset);

        Assert.Equal(
            vertical ? bar.TrackBounds.Top : bar.TrackBounds.Left,
            vertical ? atStart.Top : atStart.Left,
            3);
        Assert.Equal(
            vertical ? bar.TrackBounds.Bottom : bar.TrackBounds.Right,
            vertical ? atEnd.Bottom : atEnd.Right,
            3);

        // And no further, whatever it is handed.
        BRect past = bar.ThumbBounds(bar.MaximumOffset * 4);
        Assert.Equal(vertical ? atEnd.Top : atEnd.Left, vertical ? past.Top : past.Left, 3);
    }

    [Fact(Timeout = 600000)]
    public void A_Press_Off_Every_Bar_Is_Not_Theirs_To_Answer()
    {
        var bars = new StandardScrollbars();
        bars.Layout(Bounds, new BSize(400, 400));

        Assert.False(
            bars.TryPress(new BPoint(Bounds.Left + 4, Bounds.Top + 10), default, out BPoint offset));
        Assert.Equal(0, offset.X, 3);
        Assert.Equal(0, offset.Y, 3);
        Assert.False(bars.IsDragging);
    }

    /// <summary>A press before or after the thumb pages towards it, and stays in range.</summary>
    [Theory(Timeout = 600000)]
    [InlineData(StandardScrollbarOrientation.Vertical)]
    [InlineData(StandardScrollbarOrientation.Horizontal)]
    public void A_Press_On_The_Track_Pages_Towards_It(StandardScrollbarOrientation orientation)
    {
        StandardScrollbar bar = Only(orientation, 400);

        Assert.True(bar.TryPress(TrackPoint(bar, 0.9), 0, out double forward));
        Assert.Equal(bar.Viewport * 0.85, forward, 3);
        Assert.False(bar.IsDragging);

        Assert.True(bar.TryPress(TrackPoint(bar, 0.05), bar.MaximumOffset, out double back));
        Assert.Equal(bar.MaximumOffset - (bar.Viewport * 0.85), back, 3);

        // Never past either end.
        Assert.True(bar.TryPress(TrackPoint(bar, 0.05), 0, out double floor));
        Assert.Equal(0, floor, 3);
    }

    /// <summary>
    /// A drag takes hold where the pointer landed on the thumb, so the thumb
    /// moves with the pointer rather than jumping its own middle under it.
    /// </summary>
    [Theory(Timeout = 600000)]
    [InlineData(StandardScrollbarOrientation.Vertical)]
    [InlineData(StandardScrollbarOrientation.Horizontal)]
    public void A_Drag_Keeps_The_Grip_It_Started_With(StandardScrollbarOrientation orientation)
    {
        StandardScrollbar bar = Only(orientation, 400);
        bool vertical = orientation == StandardScrollbarOrientation.Vertical;

        BRect thumb = bar.ThumbBounds(0);
        BPoint grip = vertical
            ? new BPoint(bar.TrackBounds.Left + 1, thumb.Top + 4)
            : new BPoint(thumb.Left + 4, bar.TrackBounds.Top + 1);

        Assert.True(bar.TryPress(grip, 0, out double pressed));
        Assert.True(bar.IsDragging);
        Assert.Equal(0, pressed, 3);

        double travel = vertical
            ? bar.TrackBounds.Height - thumb.Height
            : bar.TrackBounds.Width - thumb.Width;
        BPoint dragged = vertical
            ? new BPoint(grip.X, grip.Y + travel)
            : new BPoint(grip.X + travel, grip.Y);

        Assert.True(bar.TryDrag(dragged, 0, out double moved));
        Assert.Equal(bar.MaximumOffset, moved, 3);

        bar.EndDrag();
        Assert.False(bar.IsDragging);
        Assert.False(bar.TryDrag(grip, moved, out _));
    }

    [Fact(Timeout = 600000)]
    public void A_Disabled_Bar_Never_Appears_However_Long_The_Content()
    {
        var bars = new StandardScrollbars();
        bars.Vertical.IsEnabled = false;
        bars.Horizontal.IsEnabled = false;

        Assert.Equal(Bounds, bars.Layout(Bounds, new BSize(100_000, 100_000)));
        Assert.False(bars.Vertical.IsVisible);
        Assert.False(bars.Horizontal.IsVisible);
    }

    /// <summary>One axis overflowing, so the bar under test is the only one showing.</summary>
    private static StandardScrollbar Only(StandardScrollbarOrientation orientation, double extent)
    {
        var bars = new StandardScrollbars();
        bool vertical = orientation == StandardScrollbarOrientation.Vertical;
        bars.Layout(Bounds, vertical ? new BSize(0, extent) : new BSize(extent, 0));
        return vertical ? bars.Vertical : bars.Horizontal;
    }

    private static BPoint TrackPoint(StandardScrollbar bar, double fraction) =>
        bar.Orientation == StandardScrollbarOrientation.Vertical
            ? new BPoint(bar.TrackBounds.Left + 1, bar.TrackBounds.Top + (bar.TrackBounds.Height * fraction))
            : new BPoint(bar.TrackBounds.Left + (bar.TrackBounds.Width * fraction), bar.TrackBounds.Top + 1);
}
