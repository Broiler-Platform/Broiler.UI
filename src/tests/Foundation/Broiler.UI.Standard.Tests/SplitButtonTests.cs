using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Touch;
using Broiler.UI.Button.Standard;
using Broiler.UI.Menu;

namespace Broiler.UI.Standard.Tests;

public sealed class SplitButtonTests
{
    [Fact]
    public void PrimaryAndMenuActionsAreIndependentAndDismissRestoresFocus()
    {
        using var session = Create(out var split);
        int launches = 0, actions = 0;
        split.Primary.Clicked += (_, _) => launches++;
        split.Menu.ItemInvoked += (_, _) => actions++;
        Click(session, 35, 35);
        Assert.Equal(1, launches);
        Click(session, 165, 35);
        Assert.True(split.IsExpanded);
        Assert.Same(split.Menu, session.CapturedElement);
        Assert.True(split.Trigger.GetSemanticNode().State.HasFlag(UiSemanticState.Expanded));
        Assert.Equal(new[] { "Install for user", "Install for all" }, split.Menu.GetSemanticNode().Children.Select(n => n.Name));
        session.RenderFrame();
        var popup = split.Menu.OverlayBounds;
        Assert.Equal(split.Bounds.Bottom, popup.Top);
        Click(session, popup.Left + 10, popup.Top + 10);
        Assert.Equal(1, launches);
        Assert.Equal(1, actions);
        Assert.False(split.IsExpanded);
        Assert.Null(session.CapturedElement);
        Assert.Same(split.Trigger, session.FocusedElement);
        Assert.True(split.Trigger.GetSemanticNode().State.HasFlag(UiSemanticState.Collapsed));
    }

    [Fact]
    public void MissingPrimaryStillAllowsMenuButDisabledDisclosureClosesIt()
    {
        using var session = Create(out var split);
        split.IsPrimaryEnabled = false;
        int calls = 0;
        split.Primary.Clicked += (_, _) => calls++;
        Click(session, 35, 35);
        Assert.Equal(0, calls);
        Click(session, 165, 35);
        Assert.True(split.IsExpanded);
        split.IsMenuEnabled = false;
        Assert.False(split.IsExpanded);
        Assert.Null(session.CapturedElement);
        Assert.False(split.Expand());
    }

    [Fact]
    public void KeyboardOpensNavigatesInvokesAndEscapesWithoutLaunching()
    {
        using var session = Create(out var split);
        int calls = 0;
        split.Primary.Clicked += (_, _) => calls++;
        UiMenuItem? invoked = null;
        split.Menu.ItemInvoked += (_, e) => invoked = e.Item;
        session.SetFocus(split.Primary);
        Assert.True(session.DispatchInput(Key("Down", 0x28, KeyboardModifierState.Alt)));
        Assert.True(split.IsExpanded);
        session.DispatchInput(Key("Down", 0x28));
        session.DispatchInput(Key("Enter", 0x0D));
        Assert.Equal("all", invoked?.Id);
        Assert.Equal(0, calls);
        session.DispatchInput(Key("F4", 0x73));
        Assert.True(split.IsExpanded);
        session.DispatchInput(Key("Escape", 0x1B));
        Assert.False(split.IsExpanded);
        Assert.Same(split.Trigger, session.FocusedElement);
        Assert.False(split.DispatchInput(Key("F4", 0x73, KeyboardModifierState.Alt)));
    }

    [Fact]
    public void OutsideClickDismissesWithoutInvokingPrimary()
    {
        using var session = Create(out var split);
        int calls = 0;
        split.Primary.Clicked += (_, _) => calls++;
        split.Expand();
        Click(session, 300, 250);
        Assert.False(split.IsExpanded);
        Assert.Null(session.CapturedElement);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void DisabledMenuItemCannotDispatchACommand()
    {
        using var session = Create(out var split);
        int calls = 0;
        var dispatcher = new StandardCommandDispatcher();
        dispatcher.Add(new StandardCommand("install", () => calls++));
        split.PopupMenu.CommandDispatcher = dispatcher;
        split.Menu.SetItems([new UiMenuItem("install", "Install") { IsEnabled = false, CommandName = "install" }]);
        split.Expand();
        session.DispatchInput(Key("Enter", 0x0D));
        Assert.Equal(0, calls);
        Assert.True(split.IsExpanded);
    }

    [Fact]
    public void PopupFitsViewportAndEmptyMenusDoNotCapture()
    {
        using var session = Create(out var split, new BRect(230, 250, 160, 32));
        split.Expand();
        session.RenderFrame();
        Assert.InRange(split.Menu.OverlayBounds.Right, 0, 400);
        Assert.InRange(split.Menu.OverlayBounds.Bottom, 0, 300);
        Assert.True(split.Menu.OverlayBounds.Bottom <= split.Bounds.Top);
        split.Collapse();
        split.Menu.SetItems([]);
        Assert.False(split.Expand());
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void TouchDisclosureUsesSessionFallbackWithoutInvokingPrimary()
    {
        using var session = Create(out var split);
        int calls = 0;
        split.Primary.Clicked += (_, _) => calls++;
        foreach (var state in new[] { TouchContactState.Pressed, TouchContactState.Released })
            session.DispatchInput(UiInputEvent.FromTouchContact(new TouchContactEvent(Header(), 42,
                InputPoint.ClientDeviceIndependentPixels(165, 35), state, 1, InputEventSource.Synthetic)));
        Assert.True(split.IsExpanded);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void TabTraversalVisitsBothSegmentsAndReleasesPopupCapture()
    {
        using var session = Create(out var split);
        var scope = new StandardFocusScope(session);
        Assert.True(scope.MoveFocus(1));
        Assert.Same(split.Primary, session.FocusedElement);
        Assert.True(scope.MoveFocus(1));
        Assert.Same(split.Trigger, session.FocusedElement);
        split.Expand();
        Assert.False(session.DispatchInput(Key("Tab", 0x09)));
        Assert.False(split.IsExpanded);
        Assert.Null(session.CapturedElement);
        Assert.True(scope.MoveFocus(1));
        Assert.Same(split.Primary, session.FocusedElement);
    }

    private static UiSession Create(out StandardSplitButton split, BRect? bounds = null)
    {
        split = new StandardSplitButton { Text = "Launch Writer" };
        split.Menu.SetItems([new UiMenuItem("user", "Install for user"), new UiMenuItem("all", "Install for all")]);
        var session = new StandardUiSessionBuilder().Build(new Host());
        session.AddRoot(new Container(split, bounds ?? new BRect(20, 20, 160, 32)));
        session.RenderFrame();
        return session;
    }

    private static InputEventHeader Header() => new(InputDeviceId.FromOpaqueValue("split-test"), new InputTimestamp(1, 1000, "test"), 1);
    private static UiInputEvent Key(string name, int code, KeyboardModifierState modifiers = KeyboardModifierState.None) =>
        UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(Header(), KeyboardKey.FromName(name), KeyboardKeyTransition.Down,
            modifiers, code, 0, 0, false, false, KeyboardKeyLocation.Standard, InputEventSource.Synthetic));
    private static void Click(UiSession session, double x, double y)
    {
        foreach (var transition in new[] { MouseButtonTransition.Down, MouseButtonTransition.Up })
            session.DispatchInput(UiInputEvent.FromMouseButton(new MouseButtonEvent(Header(), InputPoint.ClientDeviceIndependentPixels(x, y),
                transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None, MouseButton.Left, transition, InputEventSource.Synthetic)));
    }

    private sealed class Container : UiElement
    {
        private readonly StandardSplitButton split;
        private readonly BRect bounds;
        public Container(StandardSplitButton split, BRect bounds)
        {
            this.split = split;
            this.bounds = bounds;
            AddChild(split);
        }
        protected override BSize MeasureCore(BSize size) { split.Measure(bounds.Size); return size; }
        protected override void ArrangeCore(BRect rect) => split.Arrange(bounds);
    }

    private sealed class Host : IUiHost
    {
        public BSize ViewportSize => new(400, 300);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList list) { }
    }
}
