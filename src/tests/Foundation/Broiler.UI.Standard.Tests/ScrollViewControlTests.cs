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
