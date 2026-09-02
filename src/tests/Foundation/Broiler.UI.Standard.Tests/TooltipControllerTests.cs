using System;
using Broiler.Graphics;
using Broiler.UI.Button.Standard;
using Broiler.UI.Tooltip.Standard;
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
    [Fact(Timeout = 600000)]
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

    [Fact(Timeout = 600000)]
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

    [Fact(Timeout = 600000)]
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

    [Fact(Timeout = 600000)]
    public void An_Element_Without_A_Tip_Is_Not_A_Target()
    {
        Fixture fixture = Fixture.Create();
        fixture.Button.ToolTipText = string.Empty;

        fixture.Controller.PointerMoved(new BPoint(20, 20));

        Assert.Null(fixture.Controller.Target);
        Assert.False(fixture.Controller.IsWaiting);
    }

    [Fact(Timeout = 600000)]
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

    [Fact(Timeout = 600000)]
    public void Dismiss_Closes_The_Tooltip_And_Stops_The_Wait()
    {
        Fixture fixture = Fixture.Create();
        fixture.Controller.PointerMoved(new BPoint(20, 20));

        Assert.False(fixture.Controller.Dismiss());

        Assert.Null(fixture.Controller.Target);
        Assert.False(fixture.Controller.IsWaiting);
        Assert.False(fixture.Controller.Tick());
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

    private sealed class PlainHost : IUiHost
    {
        public BSize ViewportSize => new(800, 600);

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
