using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Toolbar.Standard;

namespace Broiler.UI.Toolbar.Tests;

/// <summary>
/// A bar narrower than the items on it. Everything from the first item that does
/// not fit onward moves into a drop-down behind a chevron, so a command is never
/// merely drawn past the edge and clipped away — which is what the bar used to do
/// with it, leaving a control that was on screen, in the layout, and unreachable.
/// </summary>
public sealed class ToolbarOverflowTests
{
    private sealed record Bar(UiSession Session, StandardToolbar Toolbar, StandardButton[] Buttons, StandardInputRoute Route);

    /// <summary>
    /// Four 80-wide buttons in a bar with room for two of them: 200 of content,
    /// less the chevron and its gap, leaves 168 for items that need 344.
    /// </summary>
    private static Bar Create(double barWidth = 220, double viewportHeight = 400, int buttons = 4, double barTop = 0)
    {
        var host = new TestHost(new BSize(Math.Max(barWidth, 320), viewportHeight));
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);
        var toolbar = new StandardToolbar
        {
            Padding = 10,
            Spacing = 4,
            PreferredSize = new BSize(barWidth, 44),
        };

        var created = new StandardButton[buttons];
        for (int index = 0; index < buttons; index++)
        {
            created[index] = new StandardButton
            {
                Text = "Item" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PreferredSize = new BSize(80, 30),
            };
            toolbar.AddChild(created[index]);
        }

        var content = new FixedBox(toolbar, new BRect(0, barTop, barWidth, 44));
        session.AddRoot(content);
        return new Bar(session, toolbar, created, new StandardInputRoute(session));
    }

    private static BRect ContentBounds(StandardToolbar toolbar) =>
        new(
            toolbar.Bounds.Left + toolbar.Padding,
            toolbar.Bounds.Top + toolbar.Padding,
            Math.Max(0, toolbar.Bounds.Width - (toolbar.Padding * 2)),
            Math.Max(0, toolbar.Bounds.Height - (toolbar.Padding * 2)));

    private static string[] DrawnText(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().Select(static command => command.Text.Text).ToArray();

    // --- What overflows ----------------------------------------------------

    [Fact(Timeout = 600000)]
    public void A_Bar_With_Room_For_Everything_Overflows_Nothing()
    {
        Bar bar = Create(900);

        BRenderList list = bar.Session.RenderFrame();

        Assert.Empty(bar.Toolbar.OverflowItems);
        Assert.Equal(BRect.Empty, bar.Toolbar.OverflowButtonBounds);
        Assert.DoesNotContain("»", DrawnText(list));
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Items_That_Do_Not_Fit_Move_Into_The_Drop_Down()
    {
        Bar bar = Create();

        BRenderList list = bar.Session.RenderFrame();

        Assert.NotEmpty(bar.Toolbar.OverflowItems);
        Assert.Contains("»", DrawnText(list));
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Nothing_Left_On_The_Bar_Is_Drawn_Past_Its_Edge()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();

        BRect content = ContentBounds(bar.Toolbar);
        foreach (StandardButton button in bar.Buttons)
        {
            if (bar.Toolbar.OverflowItems.Contains(button))
                continue;

            Assert.True(
                button.Bounds.Right <= bar.Toolbar.OverflowButtonBounds.Left + 0.01,
                $"{button.Text} ends at {button.Bounds.Right}, past the chevron at {bar.Toolbar.OverflowButtonBounds.Left}");
            Assert.True(button.Bounds.Left >= content.Left - 0.01, $"{button.Text} starts left of the bar");
        }

        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Drop_Down_Holds_A_Tail_Of_The_Bar_In_Bar_Order()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();

        int count = bar.Toolbar.OverflowItems.Count;
        Assert.True(count is > 0 and < 4, $"the sample bar must overflow some of its items, not {count} of four");

        Assert.Equal<UiElement>(bar.Buttons[^count..], bar.Toolbar.OverflowItems);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Shut_Drop_Down_Leaves_Its_Items_Without_A_Box_Or_A_Glyph()
    {
        Bar bar = Create();

        string[] drawn = DrawnText(bar.Session.RenderFrame());

        foreach (UiElement item in bar.Toolbar.OverflowItems)
        {
            Assert.Equal(BRect.Empty, item.Bounds);
            Assert.DoesNotContain(((StandardButton)item).Text, drawn);
        }

        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Clipping_Is_Still_Available_For_A_Host_That_Guarantees_Its_Width()
    {
        Bar bar = Create();
        bar.Toolbar.Overflow = UiToolbarOverflow.Clip;

        BRenderList list = bar.Session.RenderFrame();

        Assert.Empty(bar.Toolbar.OverflowItems);
        Assert.DoesNotContain("»", DrawnText(list));
        Assert.True(bar.Buttons[^1].Bounds.Right > bar.Toolbar.Bounds.Right, "the last item should run past the bar again");
        bar.Session.Dispose();
    }

    // --- Reaching them -----------------------------------------------------

    [Fact(Timeout = 600000)]
    public void The_Chevron_Opens_The_Drop_Down_And_Its_Items_Are_Drawn()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        string[] drawn = DrawnText(bar.Session.RenderFrame());

        Assert.True(bar.Toolbar.IsOverflowOpen);
        foreach (UiElement item in bar.Toolbar.OverflowItems)
        {
            Assert.NotEqual(BRect.Empty, item.Bounds);
            Assert.Contains(((StandardButton)item).Text, drawn);
            Assert.True(
                bar.Toolbar.OverflowPopupBounds.Contains(Middle(item.Bounds)),
                "an item was placed outside the drop-down it belongs to");
        }

        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Item_In_The_Drop_Down_Runs_When_It_Is_Clicked()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();
        var button = (StandardButton)bar.Toolbar.OverflowItems[^1];
        int clicks = 0;
        button.Clicked += (_, _) => clicks++;

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Session.RenderFrame();
        Click(bar, Middle(button.Bounds));

        Assert.Equal(1, clicks);
        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Press_That_Slides_Off_An_Item_Runs_Nothing()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();
        var button = (StandardButton)bar.Toolbar.OverflowItems[0];
        int clicks = 0;
        button.Clicked += (_, _) => clicks++;

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Session.RenderFrame();
        BPoint start = Middle(button.Bounds);
        bar.Route.Dispatch(Mouse(start, MouseButtonTransition.Down));
        bar.Route.Dispatch(Mouse(new BPoint(start.X, bar.Toolbar.OverflowPopupBounds.Bottom + 40), MouseButtonTransition.Up));

        Assert.Equal(0, clicks);
        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Press_Somewhere_Else_Dismisses_The_Drop_Down()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Session.RenderFrame();
        bar.Route.Dispatch(Mouse(new BPoint(4, bar.Toolbar.OverflowPopupBounds.Bottom + 60), MouseButtonTransition.Down));

        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Escape_Shuts_The_Drop_Down()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Route.Dispatch(Key("Escape", BVirtualKey.Escape));

        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Arrowing_Along_The_Bar_Still_Reaches_Every_Item_On_It()
    {
        // The far end used to be clipped but focusable. It is behind a chevron
        // now, so arrowing into it has to bring the chevron down with it.
        Bar bar = Create();
        bar.Session.RenderFrame();
        bar.Session.SetFocus(bar.Toolbar);

        foreach (StandardButton button in bar.Buttons)
        {
            bar.Route.Dispatch(Key("Right", BVirtualKey.Right));
            Assert.Same(button, bar.Session.FocusedElement);
            Assert.Equal(bar.Toolbar.OverflowItems.Contains(button), bar.Toolbar.IsOverflowOpen);
            if (bar.Toolbar.OverflowItems.Contains(button))
                Assert.NotEqual(BRect.Empty, button.Bounds);
        }

        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Arrowing_Back_Out_Of_The_Drop_Down_Shuts_It()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();
        bar.Session.SetFocus(bar.Toolbar);
        bar.Route.Dispatch(Key("End", BVirtualKey.End));
        Assert.True(bar.Toolbar.IsOverflowOpen);

        bar.Route.Dispatch(Key("Home", BVirtualKey.Home));

        Assert.Same(bar.Buttons[0], bar.Session.FocusedElement);
        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Item_Focused_In_The_Drop_Down_Hears_Its_Own_Keys()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();
        bar.Session.SetFocus(bar.Toolbar);
        bar.Route.Dispatch(Key("End", BVirtualKey.End));
        var focused = (StandardButton)bar.Session.FocusedElement!;
        Assert.Contains(focused, bar.Toolbar.OverflowItems);
        int clicks = 0;
        focused.Clicked += (_, _) => clicks++;

        bar.Route.Dispatch(Key("Enter", BVirtualKey.Enter));
        bar.Route.Dispatch(Key("Enter", BVirtualKey.Enter, KeyboardKeyTransition.Up));

        Assert.Equal(1, clicks);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Drop_Down_Opened_From_The_Keyboard_Answers_A_Mouse_The_Same_Way()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();
        bar.Session.SetFocus(bar.Toolbar);
        bar.Route.Dispatch(Key("End", BVirtualKey.End));
        Assert.True(bar.Toolbar.IsOverflowOpen);
        var item = (StandardButton)bar.Toolbar.OverflowItems[0];
        int clicks = 0;
        item.Clicked += (_, _) => clicks++;

        Click(bar, Middle(item.Bounds));

        Assert.Equal(1, clicks);
        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    // --- Where it is drawn -------------------------------------------------

    [Fact(Timeout = 600000)]
    public void The_Drop_Down_Stays_Inside_The_Window()
    {
        Bar bar = Create();
        bar.Session.RenderFrame();

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Session.RenderFrame();

        BRect popup = bar.Toolbar.OverflowPopupBounds;
        Assert.True(popup.Left >= 0, "the drop-down started left of the window");
        Assert.True(popup.Right <= 320.01, "the drop-down ran off the right of the window");
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Drop_Down_With_No_Room_Below_It_Opens_Upwards()
    {
        Bar bar = Create(viewportHeight: 200, barTop: 140);
        bar.Session.RenderFrame();

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Session.RenderFrame();

        BRect popup = bar.Toolbar.OverflowPopupBounds;
        Assert.True(
            popup.Top < bar.Toolbar.Bounds.Top,
            "the drop-down hung off the bottom of a window that had no room for it");
        Assert.True(popup.Bottom <= 200.01, "the drop-down ran off the bottom of the window");
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Drop_Down_Too_Tall_For_The_Window_Flows_Into_Columns()
    {
        // A stack taller than the screen has a foot exactly as unreachable as the
        // clipped end this replaced, so it does not get to be one.
        Bar bar = Create(viewportHeight: 220, buttons: 12);
        bar.Session.RenderFrame();

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Session.RenderFrame();

        BRect popup = bar.Toolbar.OverflowPopupBounds;
        Assert.True(popup.Bottom <= 220.01, $"the drop-down ran to {popup.Bottom} in a 220 window");
        Assert.True(popup.Left >= 0 && popup.Right <= 320.01, "the drop-down ran off the side of the window");

        int columns = bar.Toolbar.OverflowItems
            .Select(static item => Math.Round(item.Bounds.Left, 2))
            .Distinct()
            .Count();
        Assert.True(columns > 1, $"twelve items in a 220-tall window were laid out in {columns} column(s)");
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Every_Item_In_A_Columned_Drop_Down_Is_Inside_It()
    {
        Bar bar = Create(viewportHeight: 220, buttons: 12);
        bar.Session.RenderFrame();

        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Session.RenderFrame();

        foreach (UiElement item in bar.Toolbar.OverflowItems)
        {
            Assert.True(
                bar.Toolbar.OverflowPopupBounds.Contains(Middle(item.Bounds)),
                "an item was laid out outside the drop-down that holds it");
        }

        bar.Session.Dispose();
    }

    // --- Harness -----------------------------------------------------------

    private static BPoint Middle(BRect rect) => new(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));

    private static void Click(Bar bar, BPoint point)
    {
        bar.Route.Dispatch(Mouse(point, MouseButtonTransition.Down));
        bar.Route.Dispatch(Mouse(point, MouseButtonTransition.Up));
    }

    private static MouseButtonEvent Mouse(BPoint point, MouseButtonTransition transition) =>
        new(
            new InputEventHeader(
                InputDeviceId.FromOpaqueValue("mouse:overflow"),
                new InputTimestamp(1, TimeSpan.TicksPerSecond, "toolbar-overflow"),
                1),
            InputPoint.ClientDeviceIndependentPixels(point.X, point.Y),
            transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None,
            MouseButton.Left,
            transition,
            InputEventSource.Synthetic);

    private static KeyboardKeyEvent Key(
        string name,
        int nativeKeyCode,
        KeyboardKeyTransition transition = KeyboardKeyTransition.Down) =>
        new(
            new InputEventHeader(
                InputDeviceId.FromOpaqueValue("keyboard:overflow"),
                new InputTimestamp(1, TimeSpan.TicksPerSecond, "toolbar-overflow"),
                1),
            KeyboardKey.FromName(name),
            transition,
            KeyboardModifierState.None,
            nativeKeyCode,
            0,
            0,
            false,
            false,
            Source: InputEventSource.Synthetic);

    /// <summary>
    /// A root that gives the bar a box narrower than the window, so the drop-down
    /// has somewhere to hang and the bar still has to overflow.
    /// </summary>
    private sealed class FixedBox : UiElement
    {
        private readonly UiElement _child;
        private readonly BRect _box;

        public FixedBox(UiElement child, BRect box)
        {
            _child = child;
            _box = box;
            AddChild(child);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _child.Measure(new BSize(_box.Width, _box.Height));
            return new BSize(availableSize.Width, availableSize.Height);
        }

        protected override void ArrangeCore(BRect finalRect) => _child.Arrange(_box);
    }

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; } = viewportSize;

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
