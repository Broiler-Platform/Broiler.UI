using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Toolbar.Standard;

namespace Broiler.UI.Toolbar.Tests;

/// <summary>
/// A control with a drop-down of its own inside the bar's drop-down. The bar
/// routes the pointer while its own list is showing, so a combo box in there
/// would otherwise never see the clicks that work its list — and the bar would
/// read them as clicks on itself and dismiss the list being chosen from.
/// </summary>
public sealed class ToolbarOverflowComboTests
{
    private sealed record Bar(
        UiSession Session,
        StandardToolbar Toolbar,
        StandardComboBox Combo,
        StandardButton Button,
        StandardInputRoute Route);

    /// <summary>
    /// A bar with room for one 80-wide button, so the button after it and the
    /// combo box after that both end up in the drop-down.
    /// </summary>
    private static Bar Create()
    {
        var host = new TestHost(new BSize(420, 500));
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);
        var toolbar = new StandardToolbar { Padding = 10, Spacing = 4, PreferredSize = new BSize(200, 44) };
        var first = new StandardButton { Text = "First", PreferredSize = new BSize(80, 30) };
        var second = new StandardButton { Text = "Second", PreferredSize = new BSize(80, 30) };
        var combo = new StandardComboBox { PreferredSize = new BSize(90, 30), ItemHeight = 24, MaxDropDownItems = 4 };
        combo.SetItems([
            new UiComboBoxItem("s", "Small"),
            new UiComboBoxItem("m", "Medium"),
            new UiComboBoxItem("l", "Large"),
        ]);
        combo.SelectIndex(0);
        toolbar.AddChild(first);
        toolbar.AddChild(second);
        toolbar.AddChild(combo);

        var box = new FixedBox(toolbar, new BRect(0, 0, 200, 44));
        session.AddRoot(box);
        session.RenderFrame();
        return new Bar(session, toolbar, combo, second, new StandardInputRoute(session));
    }

    [Fact(Timeout = 600000)]
    public void The_Combo_Box_Is_In_The_Drop_Down_To_Begin_With()
    {
        Bar bar = Create();

        Assert.Contains(bar.Combo, bar.Toolbar.OverflowItems);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Its_List_Opens_Without_The_Bar_Shutting_Its_Own()
    {
        Bar bar = Create();
        OpenOverflow(bar);

        Click(bar, Middle(bar.Combo.Bounds));

        Assert.True(bar.Combo.IsDropDownOpen, "the combo box's own list did not open");
        Assert.True(bar.Toolbar.IsOverflowOpen, "the bar shut the drop-down the combo box lives in");
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Choosing_From_Its_List_Selects_The_Value_And_Shuts_Both()
    {
        Bar bar = Create();
        OpenOverflow(bar);
        Click(bar, Middle(bar.Combo.Bounds));
        bar.Session.RenderFrame();
        int changes = 0;
        bar.Combo.SelectionChanged += (_, _) => changes++;

        BRect popup = bar.Combo.PopupBounds;
        Click(bar, new BPoint(popup.Left + (popup.Width / 2), popup.Top + (bar.Combo.ItemHeight * 2) + 2));

        Assert.Equal(1, changes);
        Assert.Equal(2, bar.Combo.SelectedIndex);
        Assert.Equal("Large", bar.Combo.SelectedItem?.Text);
        Assert.False(bar.Combo.IsDropDownOpen);
        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Its_List_Is_Drawn_Over_The_Drop_Down_It_Sits_In()
    {
        Bar bar = Create();
        OpenOverflow(bar);
        Click(bar, Middle(bar.Combo.Bounds));

        string[] drawn = bar.Session.RenderFrame().Commands
            .OfType<BRenderCommand.DrawText>()
            .Select(static command => command.Text.Text)
            .ToArray();

        Assert.Contains("Small", drawn);
        Assert.Contains("Medium", drawn);
        Assert.Contains("Large", drawn);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Its_Keyboard_Reaches_It_Through_The_Bar()
    {
        Bar bar = Create();
        OpenOverflow(bar);
        Click(bar, Middle(bar.Combo.Bounds));
        bar.Session.RenderFrame();

        bar.Route.Dispatch(Key("Down", BVirtualKey.Down));
        bar.Route.Dispatch(Key("Enter", BVirtualKey.Enter));

        Assert.Equal(1, bar.Combo.SelectedIndex);
        Assert.False(bar.Combo.IsDropDownOpen);
        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Dismissing_Its_List_Without_Choosing_Leaves_The_Value_Alone()
    {
        Bar bar = Create();
        OpenOverflow(bar);
        Click(bar, Middle(bar.Combo.Bounds));
        bar.Session.RenderFrame();

        bar.Route.Dispatch(Key("Escape", BVirtualKey.Escape));

        Assert.Equal(0, bar.Combo.SelectedIndex);
        Assert.False(bar.Combo.IsDropDownOpen);
        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Ordinary_Button_Beside_It_Still_Runs_And_Shuts_The_Drop_Down()
    {
        Bar bar = Create();
        OpenOverflow(bar);
        int clicks = 0;
        bar.Button.Clicked += (_, _) => clicks++;

        Click(bar, Middle(bar.Button.Bounds));

        Assert.Equal(1, clicks);
        Assert.False(bar.Toolbar.IsOverflowOpen);
        bar.Session.Dispose();
    }

    // --- Harness -----------------------------------------------------------

    private static void OpenOverflow(Bar bar)
    {
        Click(bar, Middle(bar.Toolbar.OverflowButtonBounds));
        bar.Session.RenderFrame();
        Assert.True(bar.Toolbar.IsOverflowOpen);
    }

    private static BPoint Middle(BRect rect) => new(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));

    private static void Click(Bar bar, BPoint point)
    {
        bar.Route.Dispatch(Mouse(point, MouseButtonTransition.Down));
        bar.Route.Dispatch(Mouse(point, MouseButtonTransition.Up));
    }

    private static MouseButtonEvent Mouse(BPoint point, MouseButtonTransition transition) =>
        new(
            new InputEventHeader(
                InputDeviceId.FromOpaqueValue("mouse:overflow-combo"),
                new InputTimestamp(1, TimeSpan.TicksPerSecond, "toolbar-overflow-combo"),
                1),
            InputPoint.ClientDeviceIndependentPixels(point.X, point.Y),
            transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None,
            MouseButton.Left,
            transition,
            InputEventSource.Synthetic);

    private static KeyboardKeyEvent Key(string name, int nativeKeyCode) =>
        new(
            new InputEventHeader(
                InputDeviceId.FromOpaqueValue("keyboard:overflow-combo"),
                new InputTimestamp(1, TimeSpan.TicksPerSecond, "toolbar-overflow-combo"),
                1),
            KeyboardKey.FromName(name),
            KeyboardKeyTransition.Down,
            KeyboardModifierState.None,
            nativeKeyCode,
            0,
            0,
            false,
            false,
            Source: InputEventSource.Synthetic);

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
            return availableSize;
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
