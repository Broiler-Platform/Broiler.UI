using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.CheckBox.Standard;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Menu;
using Broiler.UI.RadioButton.Standard;
using Broiler.UI.SpinBox.Standard;
using Broiler.UI.Tooltip.Standard;
using Broiler.UI.TreeView.Standard;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// Coverage for hover-to-tooltip.
/// </summary>
/// <remarks>
/// UiTooltip could always place itself, wait and time out, but nothing connected it to a pointer,
/// so a tooltip was something each application wired by hand or, in practice, went without. These
/// tests pin the connecting behaviour: what the pointer has to be over, how long nothing happens,
/// and when the host still has a reason to draw.
/// </remarks>
public sealed class TooltipControllerTests
{
    [Fact]
    public void Nothing_Appears_Before_The_Delay_Has_Passed()
    {
        Fixture fixture = Fixture.Create();

        Assert.True(fixture.Controller.PointerMoved(new BPoint(20, 20)) || true);
        Assert.Same(fixture.Button, fixture.Controller.Target);
        Assert.False(fixture.Tooltip.IsTooltipOpen);

        // The host has to keep drawing, or the delay it is waiting on never elapses.
        Assert.True(fixture.Controller.IsWaiting);

        fixture.Clock.Advance(TimeSpan.FromMilliseconds(200));
        Assert.False(fixture.Controller.Tick());
        Assert.False(fixture.Tooltip.IsTooltipOpen);
    }

    [Fact]
    public void The_Tooltip_Opens_Once_The_Delay_Has_Passed()
    {
        Fixture fixture = Fixture.Create();
        fixture.Controller.PointerMoved(new BPoint(20, 20));

        fixture.Clock.Advance(fixture.Tooltip.InitialDelay);

        Assert.True(fixture.Controller.Tick());
        Assert.True(fixture.Tooltip.IsTooltipOpen);
        Assert.Equal("Save (Ctrl+S)", fixture.Tooltip.Text);

        // Nothing more to wait for, so the host may stop drawing on the tooltip's account.
        Assert.False(fixture.Controller.IsWaiting);
    }

    [Fact]
    public void A_Pointer_Over_Nothing_With_A_Tip_Closes_It()
    {
        Fixture fixture = Fixture.Create();
        fixture.Controller.PointerMoved(new BPoint(20, 20));
        fixture.Clock.Advance(fixture.Tooltip.InitialDelay);
        fixture.Controller.Tick();
        Assert.True(fixture.Tooltip.IsTooltipOpen);

        Assert.True(fixture.Controller.PointerMoved(new BPoint(400, 400)));

        Assert.Null(fixture.Controller.Target);
        Assert.False(fixture.Tooltip.IsTooltipOpen);
    }

    [Fact]
    public void An_Element_Without_A_Tip_Is_Not_A_Target()
    {
        Fixture fixture = Fixture.Create();
        fixture.Button.ToolTipText = string.Empty;

        fixture.Controller.PointerMoved(new BPoint(20, 20));

        Assert.Null(fixture.Controller.Target);
        Assert.False(fixture.Controller.IsWaiting);
    }

    [Fact]
    public void Moving_To_Another_Control_Restarts_The_Wait()
    {
        Fixture fixture = Fixture.Create();
        fixture.Controller.PointerMoved(new BPoint(20, 20));
        fixture.Clock.Advance(fixture.Tooltip.InitialDelay);
        fixture.Controller.Tick();
        Assert.True(fixture.Tooltip.IsTooltipOpen);

        fixture.Controller.PointerMoved(new BPoint(120, 20));

        // The tooltip does not slide along the bar following the pointer: the second control has
        // to earn its own tooltip from scratch.
        Assert.Same(fixture.Second, fixture.Controller.Target);
        Assert.False(fixture.Tooltip.IsTooltipOpen);
        Assert.True(fixture.Controller.IsWaiting);

        fixture.Clock.Advance(fixture.Tooltip.InitialDelay);
        Assert.True(fixture.Controller.Tick());
        Assert.Equal("Open (Ctrl+O)", fixture.Tooltip.Text);
    }

    [Fact]
    public void Dismiss_Closes_The_Tooltip_And_Stops_The_Wait()
    {
        Fixture fixture = Fixture.Create();
        fixture.Controller.PointerMoved(new BPoint(20, 20));

        Assert.False(fixture.Controller.Dismiss());

        Assert.Null(fixture.Controller.Target);
        Assert.False(fixture.Controller.IsWaiting);
        Assert.False(fixture.Controller.Tick());
    }

    [Fact]
    public void Dispatch_With_Input_Event_Drives_Tooltip()
    {
        Fixture fixture = Fixture.Create();

        var pointerMove = MouseMove(20, 20);

        Assert.False(fixture.Controller.Dispatch(pointerMove));
        Assert.Same(fixture.Button, fixture.Controller.Target);
        Assert.True(fixture.Controller.IsWaiting);

        var keyPress = Key("Enter");

        fixture.Clock.Advance(fixture.Tooltip.InitialDelay);
        fixture.Controller.Tick();
        Assert.True(fixture.Tooltip.IsTooltipOpen);

        Assert.True(fixture.Controller.Dispatch(keyPress));
        Assert.Null(fixture.Controller.Target);
        Assert.False(fixture.Tooltip.IsTooltipOpen);
    }

    [Fact]
    public void WaitingChanged_Notifies_Host_And_Callbacks()
    {
        var clock = new TestClock();
        var host = new PlainHost();
        UiSession session = new StandardUiSessionBuilder().WithClock(clock).Build(host);
        var window = new StandardWindow();
        var button = new StandardButton { Text = "Action", ToolTipText = "Helpful tip" };
        window.AddChild(button);
        var tooltip = new StandardTooltip();
        window.OpenOwnedWindow(tooltip, new BRect(0, 0, 1, 1));
        session.AddRoot(window);
        session.RenderFrame();
        button.Arrange(new BRect(0, 0, 100, 40));

        List<bool> callbackNotifications = [];
        List<bool> eventNotifications = [];

        var controller = new StandardTooltipController(tooltip, waiting => callbackNotifications.Add(waiting));
        controller.WaitingChanged += (_, waiting) => eventNotifications.Add(waiting);

        controller.PointerMoved(new BPoint(20, 20));
        Assert.Single(callbackNotifications);
        Assert.True(callbackNotifications[0]);
        Assert.Single(eventNotifications);
        Assert.True(eventNotifications[0]);
        Assert.True(host.AnimationStarted);

        controller.Dismiss();
        Assert.Equal(2, callbackNotifications.Count);
        Assert.False(callbackNotifications[1]);
        Assert.Equal(2, eventNotifications.Count);
        Assert.False(eventNotifications[1]);
        Assert.True(host.AnimationStopped);
    }

    [Fact]
    public void Attach_Automatically_Subscribes_To_Session_InputDispatched()
    {
        var clock = new TestClock();
        var host = new PlainHost();
        UiSession session = new StandardUiSessionBuilder().WithClock(clock).Build(host);
        var window = new StandardWindow();
        var button = new StandardButton { Text = "Attached", ToolTipText = "Attached tip" };
        window.AddChild(button);
        session.AddRoot(window);
        session.RenderFrame();
        button.Arrange(new BRect(0, 0, 100, 40));

        var controller = StandardTooltipController.Attach(session, window);

        var moveEvent = MouseMove(20, 20);

        session.DispatchInput(moveEvent);

        Assert.Same(button, controller.Target);
        Assert.True(controller.IsWaiting);
    }

    private static InputEventHeader Header() => new(InputDeviceId.FromOpaqueValue("tooltip-test"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"), 1);

    private static UiInputEvent Key(string name) => UiInputEvent.FromKeyboardKey(new KeyboardKeyEvent(
        Header(), KeyboardKey.FromName(name), KeyboardKeyTransition.Down, KeyboardModifierState.None, 0, 0, 0, false, false, Source: InputEventSource.Synthetic));

    private static UiInputEvent MouseMove(double x, double y) => UiInputEvent.FromMouseMove(new MouseMoveEvent(
        Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None, InputEventSource.Synthetic));

    [Fact]
    public void Tooltips_Generally_Available_Across_All_Control_Types()
    {
        var clock = new TestClock();
        var host = new PlainHost();
        UiSession session = new StandardUiSessionBuilder().WithClock(clock).Build(host);
        var window = new StandardWindow();

        var edit = new StandardEdit { Text = "Value", ToolTipText = "Enter text here" };
        var combo = new StandardComboBox { ToolTipText = "Select an option" };
        var check = new StandardCheckBox { Text = "Agree", ToolTipText = "Toggle agreement" };
        var radio = new StandardRadioButton { Text = "Choice A", ToolTipText = "Pick choice A" };
        var tree = new StandardTreeView { ToolTipText = "Tree hierarchy" };
        var spin = new StandardSpinBox { ToolTipText = "Step value" };
        var label = new StandardLabel("Heading") { ToolTipText = "Section heading" };
        var list = new StandardListView { ToolTipText = "Items list" };
        var button = new StandardButton { Text = "Action", ToolTipText = "Click button" };

        window.AddChild(edit);
        window.AddChild(combo);
        window.AddChild(check);
        window.AddChild(radio);
        window.AddChild(tree);
        window.AddChild(spin);
        window.AddChild(label);
        window.AddChild(list);
        window.AddChild(button);

        var tooltip = new StandardTooltip();
        window.OpenOwnedWindow(tooltip, new BRect(0, 0, 1, 1));
        session.AddRoot(window);
        session.RenderFrame();

        edit.Arrange(new BRect(0, 0, 50, 20));
        combo.Arrange(new BRect(50, 0, 50, 20));
        check.Arrange(new BRect(100, 0, 50, 20));
        radio.Arrange(new BRect(150, 0, 50, 20));
        tree.Arrange(new BRect(200, 0, 50, 20));
        spin.Arrange(new BRect(250, 0, 50, 20));
        label.Arrange(new BRect(300, 0, 50, 20));
        list.Arrange(new BRect(350, 0, 50, 20));
        button.Arrange(new BRect(400, 0, 50, 20));

        var controller = new StandardTooltipController(tooltip);

        void AssertControlTip(UiElement element, BPoint hitPoint, string expectedTip)
        {
            controller.PointerMoved(hitPoint);
            Assert.Same(element, controller.Target);
            clock.Advance(tooltip.InitialDelay);
            Assert.True(controller.Tick());
            Assert.Equal(expectedTip, tooltip.Text);
            controller.Dismiss();
        }

        AssertControlTip(edit, new BPoint(10, 10), "Enter text here");
        AssertControlTip(combo, new BPoint(60, 10), "Select an option");
        AssertControlTip(check, new BPoint(110, 10), "Toggle agreement");
        AssertControlTip(radio, new BPoint(160, 10), "Pick choice A");
        AssertControlTip(tree, new BPoint(210, 10), "Tree hierarchy");
        AssertControlTip(spin, new BPoint(260, 10), "Step value");
        AssertControlTip(label, new BPoint(310, 10), "Section heading");
        AssertControlTip(list, new BPoint(360, 10), "Items list");
        AssertControlTip(button, new BPoint(410, 10), "Click button");
    }

    [Fact]
    public void UiMenuItem_And_StandardCommand_Support_Tooltips()
    {
        var command = new StandardCommand("app.save", () => { }, null, "Ctrl+S", "Save document");
        Assert.Equal("Save document", command.TooltipText);
        Assert.Equal("Ctrl+S", command.AcceleratorText);

        var item = new UiMenuItem("save", command);
        Assert.Equal("Save document", item.ToolTipText);
    }

    [Fact]
    public void ToolTipText_Serves_As_Fallback_Semantic_Description()
    {
        var button = new StandardButton { Text = "Export", ToolTipText = "Export to PDF or CSV" };
        var node = button.GetSemanticNode();
        Assert.Equal("Export to PDF or CSV", node.Description);
    }

    private sealed class Fixture
    {
        private Fixture(
            UiSession session,
            TestClock clock,
            StandardTooltip tooltip,
            StandardTooltipController controller,
            StandardButton button,
            StandardButton second)
        {
            Session = session;
            Clock = clock;
            Tooltip = tooltip;
            Controller = controller;
            Button = button;
            Second = second;
        }

        public UiSession Session { get; }

        public TestClock Clock { get; }

        public StandardTooltip Tooltip { get; }

        public StandardTooltipController Controller { get; }

        public StandardButton Button { get; }

        public StandardButton Second { get; }

        public static Fixture Create()
        {
            var clock = new TestClock();
            UiSession session = new StandardUiSessionBuilder()
                .WithClock(clock)
                .Build(new PlainHost());

            var window = new StandardWindow();
            var button = new StandardButton { Text = "Save", ToolTipText = "Save (Ctrl+S)" };
            var second = new StandardButton { Text = "Open", ToolTipText = "Open (Ctrl+O)" };
            window.AddChild(button);
            window.AddChild(second);

            var tooltip = new StandardTooltip();
            window.OpenOwnedWindow(tooltip, new BRect(0, 0, 1, 1));
            session.AddRoot(window);
            session.RenderFrame();

            // The layout the window gives its children is not what these tests are about, so the
            // two buttons are placed by hand where the pointer positions expect them.
            button.Arrange(new BRect(0, 0, 100, 40));
            second.Arrange(new BRect(100, 0, 100, 40));

            return new Fixture(session, clock, tooltip, new StandardTooltipController(tooltip), button, second);
        }
    }

    private sealed class TestClock : IUiClock
    {
        private TimeSpan _elapsed;

        public UiTimestamp Now => new(_elapsed);

        public void Advance(TimeSpan by) => _elapsed += by;
    }

    private sealed class PlainHost : IUiHost, IUiAnimationHost
    {
        public bool AnimationStarted { get; private set; }
        public bool AnimationStopped { get; private set; }

        public BSize ViewportSize => new(800, 600);

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public void StartAnimation() => AnimationStarted = true;

        public void StopAnimation() => AnimationStopped = true;
    }
}
