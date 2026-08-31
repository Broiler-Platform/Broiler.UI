using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Mouse;

namespace Broiler.UI.Tests;

/// <summary>
/// Hit-testing an element that reaches beyond its own box. An overlay is drawn
/// after everything else in the frame, so a point over one belongs to it whatever
/// the boxes underneath say — without that, a control could show a list nothing
/// could click, which is the state a deferred-drawn popup is in by default.
/// </summary>
public sealed class UiOverlayHitTestTests
{
    /// <summary>
    /// A box that also answers for a strip below itself while it is showing one,
    /// which is what a drop-down is.
    /// </summary>
    private sealed class ShowingElement : UiElement
    {
        private readonly BRect _box;
        private BRect _overlay = BRect.Empty;

        public ShowingElement(BRect box, BRect overlay)
        {
            _box = box;
            Overlay = overlay;
        }

        public BRect Overlay
        {
            get => _overlay;
            set => _overlay = value;
        }

        public bool IsShowing { get; set; } = true;

        public override BRect OverlayBounds => IsShowing ? _overlay : BRect.Empty;

        protected override BSize MeasureCore(BSize availableSize)
        {
            base.MeasureCore(availableSize);
            return new BSize(_box.Width, _box.Height);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            foreach (UiElement child in Children)
                child.Arrange(child.Bounds);
        }
    }

    /// <summary>A root that puts each child in the box it was given.</summary>
    private sealed class Board : UiElement
    {
        private readonly List<(UiElement Child, BRect Box)> _placed = [];

        public void Place(UiElement child, BRect box)
        {
            AddChild(child);
            _placed.Add((child, box));
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            foreach ((UiElement child, BRect box) in _placed)
                child.Measure(new BSize(box.Width, box.Height));
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            foreach ((UiElement child, BRect box) in _placed)
                child.Arrange(box);
        }
    }

    [Fact(Timeout = 600000)]
    public void An_Element_Answers_For_The_Overlay_It_Is_Showing()
    {
        using UiSession session = CreateSession();
        var board = new Board();
        var showing = new ShowingElement(new BRect(0, 0, 40, 20), new BRect(0, 20, 40, 60));
        board.Place(showing, new BRect(0, 0, 40, 20));
        session.AddRoot(board);
        session.RenderFrame();

        Assert.Same(showing, session.HitTest(new BPoint(20, 50)));
    }

    [Fact(Timeout = 600000)]
    public void It_Stops_Answering_Once_It_Is_Not_Showing()
    {
        using UiSession session = CreateSession();
        var board = new Board();
        var showing = new ShowingElement(new BRect(0, 0, 40, 20), new BRect(0, 20, 40, 60));
        board.Place(showing, new BRect(0, 0, 40, 20));
        session.AddRoot(board);
        session.RenderFrame();

        showing.IsShowing = false;

        Assert.Same(board, session.HitTest(new BPoint(20, 50)));
    }

    [Fact(Timeout = 600000)]
    public void An_Overlay_Beats_The_Box_It_Is_Drawn_Over()
    {
        // The sibling is later in the tree, so it is drawn after the element and
        // hit before it. The overlay is drawn after them both.
        using UiSession session = CreateSession();
        var board = new Board();
        var showing = new ShowingElement(new BRect(0, 0, 40, 20), new BRect(0, 20, 40, 60));
        var under = new ShowingElement(new BRect(0, 20, 100, 60), BRect.Empty);
        board.Place(showing, new BRect(0, 0, 40, 20));
        board.Place(under, new BRect(0, 20, 100, 60));
        session.AddRoot(board);
        session.RenderFrame();

        Assert.Same(under, session.HitTest(new BPoint(70, 50)));
        Assert.Same(showing, session.HitTest(new BPoint(20, 50)));
    }

    [Fact(Timeout = 600000)]
    public void A_Point_On_An_Overlays_Own_Child_Reaches_The_Child()
    {
        using UiSession session = CreateSession();
        var board = new Board();
        var showing = new ShowingElement(new BRect(0, 0, 40, 20), new BRect(0, 20, 40, 60));
        var item = new TestElement("item");
        showing.AddChild(item);
        board.Place(showing, new BRect(0, 0, 40, 20));
        session.AddRoot(board);
        session.RenderFrame();
        item.Arrange(new BRect(4, 30, 32, 16));

        Assert.Same(item, session.HitTest(new BPoint(20, 38)));
        Assert.Same(showing, session.HitTest(new BPoint(20, 55)));
    }

    [Fact(Timeout = 600000)]
    public void An_Overlay_Opened_From_Inside_Another_One_Answers_First()
    {
        // A list opened from a control that is itself inside a drop-down is drawn
        // over that drop-down, so the deeper of the two owns the point.
        using UiSession session = CreateSession();
        var board = new Board();
        var outer = new ShowingElement(new BRect(0, 0, 40, 20), new BRect(0, 20, 80, 80));
        var inner = new ShowingElement(new BRect(4, 30, 32, 16), new BRect(4, 46, 60, 40));
        outer.AddChild(inner);
        board.Place(outer, new BRect(0, 0, 40, 20));
        session.AddRoot(board);
        session.RenderFrame();
        inner.Arrange(new BRect(4, 30, 32, 16));

        Assert.Same(inner, session.HitTest(new BPoint(50, 60)));
        Assert.Same(outer, session.HitTest(new BPoint(70, 92)));
    }

    [Fact(Timeout = 600000)]
    public void Input_Is_Dispatched_To_The_Element_Showing_The_Overlay()
    {
        using UiSession session = CreateSession();
        var board = new Board();
        var showing = new ShowingElement(new BRect(0, 0, 40, 20), new BRect(0, 20, 40, 60));
        var item = new TestElement("item") { HandlesInput = true };
        showing.AddChild(item);
        board.Place(showing, new BRect(0, 0, 40, 20));
        session.AddRoot(board);
        session.RenderFrame();
        item.Arrange(new BRect(4, 30, 32, 16));

        Assert.True(session.DispatchInput(MouseDown(20, 38)));
        Assert.Equal(1, item.InputCount);
    }

    [Fact(Timeout = 600000)]
    public void An_Element_Showing_Nothing_Is_Hit_Exactly_As_It_Was()
    {
        using UiSession session = CreateSession();
        var board = new Board();
        var plain = new TestElement("plain");
        board.Place(plain, new BRect(10, 10, 20, 20));
        session.AddRoot(board);
        session.RenderFrame();

        Assert.Same(plain, session.HitTest(new BPoint(15, 15)));
        Assert.Same(board, session.HitTest(new BPoint(60, 15)));
        Assert.Null(session.HitTest(new BPoint(-1, -1)));
    }

    private static UiSession CreateSession() =>
        new(new RecordingUiHost(new BSize(200, 120)), new InlineUiDispatcher(), new ManualUiClock());

    private static UiInputEvent MouseDown(double x, double y) =>
        UiInputEvent.FromMouseButton(new MouseButtonEvent(
            new InputEventHeader(
                InputDeviceId.FromOpaqueValue("mouse:overlay"),
                new InputTimestamp(1, TimeSpan.TicksPerSecond, "overlay"),
                1),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.Left,
            MouseButton.Left,
            MouseButtonTransition.Down,
            InputEventSource.Synthetic));
}
