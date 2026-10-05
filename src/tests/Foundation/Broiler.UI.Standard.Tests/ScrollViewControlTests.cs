using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.Input.Touch;
using Broiler.UI.Button.Standard;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class ScrollViewControlTests
{
    [Fact]
    public void Standard_ScrollView_Reserves_Content_Bounds_For_Auto_Scrollbars()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
        };
        var content = new FixedElement(new BSize(140, 180));
        scrollView.AddChild(content);

        scrollView.Measure(new BSize(100, 100));
        scrollView.Arrange(new BRect(0, 0, 100, 100));

        Assert.True(scrollView.HasVerticalScrollbar);
        Assert.True(scrollView.HasHorizontalScrollbar);
        Assert.Equal(new BSize(90, 90), scrollView.ViewportSize);
        Assert.Equal(new BRect(0, 0, 90, 90), scrollView.ContentBounds);
        Assert.Equal(new BRect(0, 0, 140, 180), content.Bounds);

        scrollView.SetOffset(new BPoint(25, 40));
        scrollView.Arrange(new BRect(0, 0, 100, 100));

        Assert.Equal(new BRect(-25, -40, 140, 180), content.Bounds);
    }

    [Fact]
    public void Standard_ScrollView_Renders_Tracks_Thumbs_And_Corner()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
        };
        scrollView.AddChild(new FixedElement(new BSize(140, 180)));

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out BRenderList renderList);

        IEnumerable<BRenderCommand.FillRoundedRect> roundedFills = renderList.Commands.OfType<BRenderCommand.FillRoundedRect>();
        Assert.Contains(roundedFills, command => command.Rect == new BRect(90, 0, 10, 90) && command.Color == scrollView.ScrollbarTrack);
        Assert.Contains(roundedFills, command => command.Rect == new BRect(0, 90, 90, 10) && command.Color == scrollView.ScrollbarTrack);
        Assert.Contains(roundedFills, command => command.Color == scrollView.ScrollbarThumb);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.FillRect>(), command => command.Rect == new BRect(90, 90, 10, 10) && command.Color == scrollView.ScrollbarTrack);
    }

    [Fact]
    public void Standard_ScrollView_Dragging_Vertical_Thumb_Updates_Offset_And_Captures_Input()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            HorizontalScrollBarVisibility = UiScrollBarVisibility.Hidden,
        };
        scrollView.AddChild(new FixedElement(new BSize(80, 300)));

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        Assert.True(scrollView.HasVerticalScrollbar);
        Assert.False(scrollView.HasHorizontalScrollbar);
        Assert.True(scrollView.DispatchInput(MouseDown(95, 5, 1)));
        Assert.Same(scrollView, session.CapturedElement);

        Assert.True(scrollView.DispatchInput(MouseMove(95, 55, 2)));

        Assert.InRange(scrollView.VerticalOffset, 149.999, 150.001);
        Assert.True(scrollView.DispatchInput(MouseUp(95, 55, 3)));
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void Standard_ScrollView_Leaves_Room_Beside_Width_Constrained_Content_Inside_Its_Clip()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            Constraint = UiScrollConstraint.ConstrainWidth,
            HorizontalContentInset = 2,
        };
        var content = new WidthFillingElement(300);
        scrollView.AddChild(content);

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out BRenderList renderList);

        // The viewport, the bar and the clip stay where they are; the content is 2 DIP short of each side.
        Assert.True(scrollView.HasVerticalScrollbar);
        Assert.False(scrollView.HasHorizontalScrollbar);
        Assert.Equal(new BRect(0, 0, 90, 100), scrollView.ContentBounds);
        Assert.Equal(86, content.MeasuredWidth);
        Assert.Equal(new BRect(2, 0, 86, 300), content.Bounds);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.PushClip>(), clip => clip.Rect == scrollView.ContentBounds);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == new BRect(90, 0, 10, 100));
        Assert.Equal(90, scrollView.ExtentSize.Width);
    }

    [Fact]
    public void Standard_ScrollView_Keeps_The_Room_At_Both_Ends_Of_Content_That_Scrolls_Sideways()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            VerticalScrollBarVisibility = UiScrollBarVisibility.Hidden,
            HorizontalContentInset = 3,
        };
        var content = new FixedElement(new BSize(140, 80));
        scrollView.AddChild(content);

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        Assert.Equal(146, scrollView.ExtentSize.Width);
        Assert.Equal(new BRect(3, 0, 140, 90), content.Bounds);

        scrollView.SetOffset(new BPoint(scrollView.ExtentSize.Width - scrollView.ViewportSize.Width, 0));
        scrollView.Arrange(new BRect(0, 0, 100, 100));
        Assert.Equal(scrollView.ContentBounds.Right - 3, content.Bounds.Right, 6);

        // Brought into view, a rectangle at the content's left edge keeps the room beside it.
        Assert.True(scrollView.MakeVisible(new BRect(content.Bounds.Left, 10, 20, 20)));
        Assert.Equal(0, scrollView.HorizontalOffset);
    }

    [Fact]
    public void Standard_ScrollView_Keeps_The_Room_At_Both_Ends_Of_Content_That_Scrolls_Up_And_Down()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            Constraint = UiScrollConstraint.ConstrainWidth,
            VerticalContentInset = 2,
        };
        var content = new WidthFillingElement(300);
        scrollView.AddChild(content);

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out BRenderList renderList);

        // The viewport, the bar and the clip stay where they are; the content starts 2 DIP down, at its full
        // width, and the extent takes in the room at both ends.
        Assert.Equal(new BRect(0, 0, 90, 100), scrollView.ContentBounds);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.PushClip>(), clip => clip.Rect == scrollView.ContentBounds);
        Assert.Equal(90, content.MeasuredWidth);
        Assert.Equal(new BRect(0, 2, 90, 300), content.Bounds);
        Assert.Equal(304, scrollView.ExtentSize.Height);

        scrollView.SetOffset(new BPoint(0, scrollView.ExtentSize.Height - scrollView.ViewportSize.Height));
        scrollView.Arrange(new BRect(0, 0, 100, 100));
        Assert.Equal(scrollView.ContentBounds.Bottom - 2, content.Bounds.Bottom, 6);

        // Brought into view from above or below, a rectangle keeps the room between it and the edge it meets.
        scrollView.SetOffset(new BPoint(0, 100));
        scrollView.Arrange(new BRect(0, 0, 100, 100));
        Assert.True(scrollView.MakeVisible(new BRect(0, 150, 20, 20)));
        scrollView.Arrange(new BRect(0, 0, 100, 100));
        Assert.Equal(scrollView.ContentBounds.Bottom - 2, 150 - (scrollView.VerticalOffset - 100) + 20, 6);
        double offset = scrollView.VerticalOffset;
        Assert.True(scrollView.MakeVisible(new BRect(0, -10, 20, 20)));
        Assert.Equal(offset - 12, scrollView.VerticalOffset, 6);
    }

    [Fact]
    public void Standard_ScrollView_Leaves_Room_Above_And_Below_Height_Constrained_Content()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            Constraint = UiScrollConstraint.ConstrainHeight,
            VerticalScrollBarVisibility = UiScrollBarVisibility.Hidden,
            VerticalContentInset = 3,
        };
        var content = new HeightFillingElement(80);
        scrollView.AddChild(content);

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        // Measured and arranged 3 DIP short of each end of the viewport.
        Assert.Equal(new BRect(0, 0, 100, 100), scrollView.ContentBounds);
        Assert.Equal(94, content.MeasuredHeight);
        Assert.Equal(new BRect(0, 3, 100, 94), content.Bounds);
    }

    [Fact]
    public void Standard_ScrollView_Content_Inset_Is_Zero_Unless_Set_And_Never_Negative()
    {
        var scrollView = new StandardScrollView();

        Assert.Equal(0, scrollView.HorizontalContentInset);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.HorizontalContentInset = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.HorizontalContentInset = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.HorizontalContentInset = double.PositiveInfinity);
        Assert.Equal(0, scrollView.VerticalContentInset);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.VerticalContentInset = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.VerticalContentInset = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.VerticalContentInset = double.PositiveInfinity);
        Assert.Equal(0, scrollView.ScrollbarGap);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.ScrollbarGap = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.ScrollbarGap = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollView.ScrollbarGap = double.PositiveInfinity);
    }

    [Fact]
    public void Standard_ScrollView_Leaves_A_Gap_Between_Width_Constrained_Content_And_A_Shown_Bar()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            Constraint = UiScrollConstraint.ConstrainWidth,
            HorizontalContentInset = 1,
            ScrollbarGap = 2,
        };
        var content = new WidthFillingElement(300);
        scrollView.AddChild(content);

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out BRenderList renderList);

        // The bar keeps its place at the edge; the content area, and the clip with it, ends 2 DIP short of it, and
        // the content is measured and arranged inside that, with its own room.
        Assert.True(scrollView.HasVerticalScrollbar);
        Assert.Equal(new BRect(0, 0, 88, 100), scrollView.ContentBounds);
        Assert.Equal(86, content.MeasuredWidth);
        Assert.Equal(new BRect(1, 0, 86, 300), content.Bounds);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.PushClip>(), clip => clip.Rect == scrollView.ContentBounds);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == new BRect(90, 0, 10, 100) && fill.Color == scrollView.ScrollbarTrack);
        BRect thumb = renderList.Commands.OfType<BRenderCommand.FillRoundedRect>().Single(fill => fill.Color == scrollView.ScrollbarThumb).Rect;
        Assert.Equal(90, thumb.Left);
        Assert.Equal(88, scrollView.ExtentSize.Width);

        // Content that fits shows no bar and loses nothing to the gap.
        var fits = new StandardScrollView
        {
            ScrollbarThickness = 10,
            Constraint = UiScrollConstraint.ConstrainWidth,
            HorizontalContentInset = 1,
            ScrollbarGap = 2,
        };
        var fitting = new WidthFillingElement(60);
        fits.AddChild(fitting);
        using UiSession other = AttachAndRender(fits, new BSize(100, 100), out _);
        Assert.False(fits.HasVerticalScrollbar);
        Assert.Equal(new BRect(0, 0, 100, 100), fits.ContentBounds);
        Assert.Equal(98, fitting.MeasuredWidth);
    }

    [Fact]
    public void Standard_ScrollView_Leaves_The_Gap_Beside_Both_Bars_Which_Keep_Their_Places()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            ScrollbarGap = 2,
        };
        scrollView.AddChild(new FixedElement(new BSize(140, 180)));

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out BRenderList renderList);

        // The tracks and the corner are where they are without a gap; each bar runs past the gap beside the other.
        Assert.True(scrollView.HasVerticalScrollbar);
        Assert.True(scrollView.HasHorizontalScrollbar);
        Assert.Equal(new BRect(0, 0, 88, 88), scrollView.ContentBounds);
        Assert.Equal(new BSize(88, 88), scrollView.ViewportSize);
        IEnumerable<BRenderCommand.FillRoundedRect> roundedFills = renderList.Commands.OfType<BRenderCommand.FillRoundedRect>();
        Assert.Contains(roundedFills, command => command.Rect == new BRect(90, 0, 10, 90) && command.Color == scrollView.ScrollbarTrack);
        Assert.Contains(roundedFills, command => command.Rect == new BRect(0, 90, 90, 10) && command.Color == scrollView.ScrollbarTrack);
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.FillRect>(), command => command.Rect == new BRect(90, 90, 10, 10) && command.Color == scrollView.ScrollbarTrack);

        // The track is still where a press pages the content, and the gap is not part of it.
        Assert.True(scrollView.DispatchInput(MouseDown(95, 80, 1)));
        Assert.True(scrollView.VerticalOffset > 0);
        double offset = scrollView.VerticalOffset;
        Assert.False(scrollView.DispatchInput(MouseDown(89, 80, 2)));
        Assert.Equal(offset, scrollView.VerticalOffset);
    }

    [Theory]
    [InlineData(0.25, false)]
    [InlineData(0.5, false)]
    [InlineData(0.75, true)]
    [InlineData(40, true)]
    public void Standard_ScrollView_Shows_A_Bar_Only_Past_Half_A_DIP_Of_Overflow(double overflow, bool bar)
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            Constraint = UiScrollConstraint.ConstrainWidth,
        };
        var content = new WidthFillingElement(100 + overflow);
        scrollView.AddChild(content);

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        // A rounding error in the content's height is no reason for a bar: the content keeps the full width.
        Assert.Equal(bar, scrollView.HasVerticalScrollbar);
        Assert.Equal(bar ? 90 : 100, content.MeasuredWidth);

        // Nothing is lost to it either: the extent is the content's, and the end of it can still be brought into view.
        Assert.Equal(100 + overflow, scrollView.ExtentSize.Height);
        Assert.True(scrollView.ScrollToEnd());
        Assert.Equal(overflow, scrollView.VerticalOffset, 6);
    }

    [Fact]
    public void Standard_ScrollView_Clicking_Vertical_Track_Pages_Content()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
            HorizontalScrollBarVisibility = UiScrollBarVisibility.Hidden,
        };
        scrollView.AddChild(new FixedElement(new BSize(80, 300)));

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        Assert.True(scrollView.DispatchInput(MouseDown(95, 80, 1)));

        Assert.Equal(85, scrollView.VerticalOffset);
    }

    [Fact]
    public void Standard_ScrollView_HitTesting_Routes_Scrollbar_Track_Ahead_Of_Overflowing_Content()
    {
        var scrollView = new StandardScrollView
        {
            ScrollbarThickness = 10,
        };
        var content = new FixedElement(new BSize(140, 300)) { HandlesInput = true };
        scrollView.AddChild(content);

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        Assert.Same(scrollView, session.HitTest(new BPoint(95, 80)));
        Assert.True(session.DispatchInput(MouseDown(95, 80, 1)));

        Assert.Equal(0, content.InputCount);
        Assert.Equal(76.5, scrollView.VerticalOffset);
    }

    [Fact]
    public void Standard_ScrollView_Pans_Content_From_A_Touch_Drag()
    {
        var scrollView = new StandardScrollView
        {
            HorizontalScrollBarVisibility = UiScrollBarVisibility.Hidden,
        };
        scrollView.AddChild(new FixedElement(new BSize(80, 300)));

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        Assert.False(scrollView.DispatchInput(Touch(50, 70, 1, TouchContactState.Pressed)));
        Assert.True(scrollView.DispatchInput(Touch(50, 30, 2, TouchContactState.Moved)));
        Assert.Equal(40, scrollView.VerticalOffset);
        Assert.True(scrollView.DispatchInput(Touch(50, 30, 3, TouchContactState.Released)));
    }

    [Fact]
    public void Touch_Scroll_Cancels_The_Pointer_Fallback_Without_Clicking_A_Child_Button()
    {
        var scrollView = new StandardScrollView
        {
            HorizontalScrollBarVisibility = UiScrollBarVisibility.Hidden,
        };
        var content = new ButtonContent();
        int clicks = 0;
        content.Button.Clicked += (_, _) => clicks++;
        scrollView.AddChild(content);

        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        Assert.True(session.DispatchInput(Touch(40, 40, 1, TouchContactState.Pressed)));
        Assert.True(session.DispatchInput(Touch(40, 30, 2, TouchContactState.Moved)));
        Assert.True(session.DispatchInput(Touch(40, 30, 3, TouchContactState.Released)));
        Assert.Equal(0, clicks);
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void Standard_ScrollView_Shift_Wheel_Scrolls_Horizontally()
    {
        var scrollView = new StandardScrollView { ScrollbarThickness = 10 };
        scrollView.AddChild(new FixedElement(new BSize(300, 300)));
        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        Assert.True(scrollView.DispatchInput(Wheel(-1, InputModifiers.Shift, 1)));

        Assert.True(scrollView.HorizontalOffset > 0);
        Assert.Equal(0, scrollView.VerticalOffset);
    }

    [Fact]
    public void Standard_ScrollView_A_Wheel_Tilted_Right_Scrolls_Right()
    {
        var scrollView = new StandardScrollView { ScrollbarThickness = 10 };
        scrollView.AddChild(new FixedElement(new BSize(300, 300)));
        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        // Win32 reports a wheel tilted right (WM_MOUSEHWHEEL) as a positive notch, and so does
        // every Broiler input source.
        Assert.True(scrollView.DispatchInput(Wheel(1, InputModifiers.None, 1, MouseWheelAxis.Horizontal)));
        Assert.Equal(scrollView.LineScrollAmount, scrollView.HorizontalOffset);
        Assert.Equal(0, scrollView.VerticalOffset);

        Assert.True(scrollView.DispatchInput(Wheel(-1, InputModifiers.None, 2, MouseWheelAxis.Horizontal)));
        Assert.Equal(0, scrollView.HorizontalOffset);

        // Shift with the wheel turned towards the user scrolls right too; turned away, back left.
        Assert.True(scrollView.DispatchInput(Wheel(-1, InputModifiers.Shift, 3)));
        Assert.Equal(scrollView.LineScrollAmount, scrollView.HorizontalOffset);
        Assert.True(scrollView.DispatchInput(Wheel(1, InputModifiers.Shift, 4)));
        Assert.Equal(0, scrollView.HorizontalOffset);
        Assert.Equal(0, scrollView.VerticalOffset);
    }

    [Fact]
    public void Standard_ScrollView_Shift_Wheel_Scrolls_The_Same_Way_When_The_Host_Has_Already_Turned_It_Sideways()
    {
        var scrollView = new StandardScrollView { ScrollbarThickness = 10 };
        scrollView.AddChild(new FixedElement(new BSize(300, 300)));
        using UiSession session = AttachAndRender(scrollView, new BSize(100, 100), out _);

        // Broiler.Hosting.Windows reports Shift with the wheel as a horizontal notch that keeps Shift
        // and the vertical sign: turned towards the user, it arrives as Horizontal -1 with Shift.
        Assert.True(scrollView.DispatchInput(Wheel(-1, InputModifiers.Shift, 1, MouseWheelAxis.Horizontal)));
        Assert.Equal(scrollView.LineScrollAmount, scrollView.HorizontalOffset);
        Assert.Equal(0, scrollView.VerticalOffset);

        Assert.True(scrollView.DispatchInput(Wheel(1, InputModifiers.Shift, 2, MouseWheelAxis.Horizontal)));
        Assert.Equal(0, scrollView.HorizontalOffset);
        Assert.Equal(0, scrollView.VerticalOffset);
    }

    private static UiInputEvent Wheel(double notches, InputModifiers modifiers, long sequence, MouseWheelAxis axis = MouseWheelAxis.Vertical) =>
        UiInputEvent.FromMouseWheel(
            new MouseWheelEvent(
                Header(sequence),
                InputPoint.ClientDeviceIndependentPixels(50, 50),
                MouseButtons.None,
                axis,
                notches,
                InputEventSource.Synthetic,
                modifiers));

    private static UiSession AttachAndRender(StandardScrollView scrollView, BSize viewportSize, out BRenderList renderList)
    {
        var host = new TestHost(viewportSize);
        UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(scrollView);
        renderList = StandardRenderTraversal.Render(session);
        return session;
    }

    private static UiInputEvent MouseDown(double x, double y, long sequence) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header(sequence),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

    private static UiInputEvent MouseUp(double x, double y, long sequence) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header(sequence),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.None,
                MouseButton.Left,
                MouseButtonTransition.Up,
                InputEventSource.Synthetic));

    private static UiInputEvent MouseMove(double x, double y, long sequence) =>
        UiInputEvent.FromMouseMove(
            new MouseMoveEvent(
                Header(sequence),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                InputEventSource.Synthetic));

    private static UiInputEvent Touch(double x, double y, long sequence, TouchContactState state) =>
        UiInputEvent.FromTouchContact(
            new TouchContactEvent(
                Header(sequence),
                42,
                InputPoint.ClientDeviceIndependentPixels(x, y),
                state,
                1,
                InputEventSource.Synthetic));

    private static InputEventHeader Header(long sequence) =>
        new(
            InputDeviceId.FromOpaqueValue("mouse"),
            new InputTimestamp(sequence, TimeSpan.TicksPerSecond, "scrollview-test"),
            sequence);

    /// <summary>As tall as it is offered, and records the height.</summary>
    private sealed class HeightFillingElement(double width) : UiElement
    {
        public double MeasuredHeight { get; private set; }

        protected override BSize MeasureCore(BSize availableSize)
        {
            MeasuredHeight = availableSize.Height;
            return new BSize(width, availableSize.Height);
        }
    }

    /// <summary>As wide as it is offered, and records the width.</summary>
    private sealed class WidthFillingElement(double height) : UiElement
    {
        public double MeasuredWidth { get; private set; }

        protected override BSize MeasureCore(BSize availableSize)
        {
            MeasuredWidth = availableSize.Width;
            return new BSize(availableSize.Width, height);
        }
    }

    private sealed class FixedElement : UiElement
    {
        public FixedElement(BSize desiredSize)
        {
            Desired = desiredSize;
        }

        public BSize Desired { get; }

        public bool HandlesInput { get; init; }

        public int InputCount { get; private set; }

        protected override BSize MeasureCore(BSize availableSize) => Desired;

        protected override void RenderCore(UiRenderContext context) =>
            context.RenderList.FillRect(Bounds, BColor.Blue);

        protected override bool OnInput(UiInputEvent input)
        {
            InputCount++;
            return HandlesInput;
        }
    }

    private sealed class ButtonContent : UiElement
    {
        public ButtonContent()
        {
            Button = new StandardButton { Text = "Touch target" };
            AddChild(Button);
        }

        public StandardButton Button { get; }

        protected override BSize MeasureCore(BSize availableSize)
        {
            Button.Measure(new BSize(80, 80));
            return new BSize(80, 300);
        }

        protected override void ArrangeCore(BRect finalRect) =>
            Button.Arrange(new BRect(finalRect.Left, finalRect.Top, 80, 80));
    }

    private sealed class TestHost : IUiHost
    {
        public TestHost(BSize viewportSize)
        {
            ViewportSize = viewportSize;
        }

        public BSize ViewportSize { get; }

        public double Scale => 1.0;

        public List<UiInvalidation> Invalidations { get; } = [];

        public List<BRenderList> Presented { get; } = [];

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation) => Invalidations.Add(invalidation);

        public void Present(BRenderList renderList) => Presented.Add(renderList);
    }
}
