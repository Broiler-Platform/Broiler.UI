using Broiler.Graphics;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The scrollbar three Standard controls had each written out separately, now
/// written once.
///
/// It holds no element and no session, so what it does is arithmetic and
/// painting — which is why these are assertions about rectangles rather than a
/// scene to click in.
/// </summary>
public sealed class VerticalScrollbarTests
{
    private static readonly BRect Bounds = new(10, 20, 200, 100);

    /// <summary>
    /// A bar over a pane whose content fits is chrome that reports nothing, and
    /// it costs the pane its width for the privilege.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void It_Shows_Only_When_The_Content_Does_Not_Fit()
    {
        var bar = new StandardVerticalScrollbar();

        Assert.Equal(Bounds, bar.Layout(Bounds, extent: 40));
        Assert.False(bar.IsVisible);
        Assert.True(bar.TrackBounds.IsEmpty);
        Assert.Equal(BRect.Empty, bar.ThumbBounds(0));

        // Exactly full is still not more than fits.
        bar.Layout(Bounds, extent: Bounds.Height);
        Assert.False(bar.IsVisible);

        bar.Layout(Bounds, extent: Bounds.Height + 1);
        Assert.True(bar.IsVisible);
    }

    [Fact(Timeout = 600000)]
    public void It_Takes_Its_Width_From_The_Content_And_Sits_Beside_It()
    {
        var bar = new StandardVerticalScrollbar { Thickness = 12 };

        BRect content = bar.Layout(Bounds, extent: 400);

        Assert.Equal(Bounds.Width - 12, content.Width, 3);
        Assert.Equal(Bounds.Height, content.Height, 3);
        Assert.Equal(content.Right, bar.TrackBounds.Left, 3);
        Assert.Equal(Bounds.Right, bar.TrackBounds.Right, 3);
        Assert.Equal(Bounds.Height, bar.TrackBounds.Height, 3);
    }

    /// <summary>
    /// The thumb is the viewport's share of the extent, and never shorter than
    /// its floor — that share goes to nothing as a document grows, and a thumb
    /// of no height is one nobody can take hold of.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Thumb_Is_The_Viewports_Share_With_A_Floor()
    {
        var bar = new StandardVerticalScrollbar { MinimumThumbLength = 18 };

        bar.Layout(Bounds, extent: 400);
        Assert.Equal(25, bar.ThumbBounds(0).Height, 3);

        bar.Layout(Bounds, extent: 100_000);
        Assert.Equal(18, bar.ThumbBounds(0).Height, 3);
    }

    [Fact(Timeout = 600000)]
    public void The_Thumb_Runs_The_Track_As_The_Offset_Runs_Its_Range()
    {
        var bar = new StandardVerticalScrollbar();
        bar.Layout(Bounds, extent: 400);

        Assert.Equal(bar.TrackBounds.Top, bar.ThumbBounds(0).Top, 3);
        Assert.Equal(bar.TrackBounds.Bottom, bar.ThumbBounds(bar.MaximumOffset).Bottom, 3);

        // And no further, whatever it is handed.
        Assert.Equal(bar.TrackBounds.Bottom, bar.ThumbBounds(bar.MaximumOffset * 4).Bottom, 3);
    }

    [Fact(Timeout = 600000)]
    public void A_Press_Off_The_Bar_Is_Not_The_Bars_To_Answer()
    {
        var bar = new StandardVerticalScrollbar();
        bar.Layout(Bounds, extent: 400);

        Assert.False(bar.TryPress(new BPoint(Bounds.Left + 4, Bounds.Top + 10), 0, out double offset));
        Assert.Equal(0, offset, 3);
        Assert.False(bar.IsDragging);
    }

    /// <summary>A press above or below the thumb pages towards it, and stays in range.</summary>
    [Fact(Timeout = 600000)]
    public void A_Press_On_The_Track_Pages_Towards_It()
    {
        var bar = new StandardVerticalScrollbar();
        bar.Layout(Bounds, extent: 400);

        Assert.True(bar.TryPress(TrackPoint(bar, 0.9), 0, out double down));
        Assert.Equal(Bounds.Height * 0.85, down, 3);
        Assert.False(bar.IsDragging);

        Assert.True(bar.TryPress(TrackPoint(bar, 0.05), bar.MaximumOffset, out double up));
        Assert.Equal(bar.MaximumOffset - (Bounds.Height * 0.85), up, 3);

        // Never past either end.
        Assert.True(bar.TryPress(TrackPoint(bar, 0.05), 0, out double floor));
        Assert.Equal(0, floor, 3);
    }

    /// <summary>
    /// A drag takes hold where the pointer landed on the thumb, so the thumb
    /// moves with the pointer rather than jumping its own middle under it.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Drag_Keeps_The_Grip_It_Started_With()
    {
        var bar = new StandardVerticalScrollbar();
        bar.Layout(Bounds, extent: 400);

        BRect thumb = bar.ThumbBounds(0);
        BPoint grip = new(bar.TrackBounds.Left + 1, thumb.Top + 4);

        Assert.True(bar.TryPress(grip, 0, out double pressed));
        Assert.True(bar.IsDragging);
        Assert.Equal(0, pressed, 3);

        // Dragged to the bottom of its travel: the offset is the maximum, and
        // the grip is still four units from the thumb's top.
        double travel = bar.TrackBounds.Height - thumb.Height;
        Assert.True(bar.TryDrag(new BPoint(grip.X, grip.Y + travel), 0, out double dragged));
        Assert.Equal(bar.MaximumOffset, dragged, 3);

        bar.EndDrag();
        Assert.False(bar.IsDragging);
        Assert.False(bar.TryDrag(grip, dragged, out _));
    }

    [Fact(Timeout = 600000)]
    public void A_Disabled_Bar_Never_Appears_However_Long_The_Content()
    {
        var bar = new StandardVerticalScrollbar { IsEnabled = false };

        Assert.Equal(Bounds, bar.Layout(Bounds, extent: 100_000));
        Assert.False(bar.IsVisible);
    }

    private static BPoint TrackPoint(StandardVerticalScrollbar bar, double fraction) =>
        new(bar.TrackBounds.Left + 1, bar.TrackBounds.Top + (bar.TrackBounds.Height * fraction));
}
