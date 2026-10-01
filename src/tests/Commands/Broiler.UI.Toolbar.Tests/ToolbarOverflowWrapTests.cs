using System;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Windowing;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Toolbar.Standard;
using Xunit;

namespace Broiler.UI.Toolbar.Tests;

public sealed class ToolbarOverflowWrapTests
{
    private sealed record Bar(UiSession Session, StandardToolbar Toolbar, StandardButton[] Buttons, StandardInputRoute Route) : IDisposable
    {
        public void Dispose() => Session.Dispose();
    }

    private static Bar Create(double barWidth = 200, double viewportHeight = 400, int buttons = 4, UiToolbarOrientation orientation = UiToolbarOrientation.Horizontal)
    {
        var host = new TestHost(new BSize(barWidth, viewportHeight));
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);
        var toolbar = new StandardToolbar
        {
            Padding = 10,
            Spacing = 4,
            Orientation = orientation,
            Overflow = UiToolbarOverflow.Wrap,
            PreferredSize = orientation == UiToolbarOrientation.Horizontal ? new BSize(barWidth, 44) : new BSize(44, barWidth),
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

        session.AddRoot(toolbar);
        return new Bar(session, toolbar, created, new StandardInputRoute(session));
    }

    [Fact]
    public void Wrap_Mode_Wraps_Buttons_Onto_Multiple_Lines_When_Width_Is_Constrained()
    {
        // 4 buttons of width 80 + spacing 4. In 200px width (content width = 180px),
        // exactly 2 buttons fit per line (80 + 4 + 80 = 164 <= 180).
        using var bar = Create(barWidth: 200, buttons: 4);
        bar.Session.RenderFrame();

        // No items moved to overflow dropdown
        Assert.Empty(bar.Toolbar.OverflowItems);
        Assert.False(bar.Toolbar.IsOverflowOpen);

        // Buttons 0 and 1 are on the first row
        Assert.Equal(bar.Buttons[0].Bounds.Top, bar.Buttons[1].Bounds.Top);
        // Buttons 2 and 3 are on the second row
        Assert.Equal(bar.Buttons[2].Bounds.Top, bar.Buttons[3].Bounds.Top);
        Assert.True(bar.Buttons[2].Bounds.Top > bar.Buttons[0].Bounds.Top);

        // All 4 buttons have positive bounds
        foreach (var button in bar.Buttons)
        {
            Assert.True(button.Bounds.Width > 0);
            Assert.True(button.Bounds.Height > 0);
        }

        // Toolbar height has grown to accommodate 2 rows
        Assert.True(bar.Toolbar.Bounds.Height > 50);
    }

    [Fact]
    public void Wrap_Mode_Vertical_Wraps_Into_Columns()
    {
        // Vertical toolbar with constrained height (content height = 60).
        // 4 buttons of height 30. If height is 80 (content 60), 1 button fits per column.
        using var bar = Create(barWidth: 300, viewportHeight: 80, buttons: 4, orientation: UiToolbarOrientation.Vertical);
        bar.Session.RenderFrame();

        Assert.Empty(bar.Toolbar.OverflowItems);
        // Buttons 0 and 1 are in different columns
        Assert.True(bar.Buttons[1].Bounds.Left > bar.Buttons[0].Bounds.Left);
    }

    [Fact]
    public void Pointer_Click_Works_On_Wrapped_Buttons()
    {
        using var bar = Create(barWidth: 200, buttons: 4);
        bar.Session.RenderFrame();

        bool clicked = false;
        bar.Buttons[3].Clicked += (_, _) => clicked = true;

        // Click button 3 on the second row
        var bounds = bar.Buttons[3].Bounds;
        bar.Route.Dispatch(MouseDown(bounds.Left + 5, bounds.Top + 5));
        bar.Route.Dispatch(MouseUp(bounds.Left + 5, bounds.Top + 5));

        Assert.True(clicked);
    }

    private static InputEventHeader Header() =>
        new(InputDeviceId.FromOpaqueValue("test"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"), 1);

    private static MouseButtonEvent MouseDown(double x, double y) =>
        new(Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left,
            MouseButton.Left, MouseButtonTransition.Down, InputEventSource.Synthetic);

    private static MouseButtonEvent MouseUp(double x, double y) =>
        new(Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None,
            MouseButton.Left, MouseButtonTransition.Up, InputEventSource.Synthetic);

    private sealed class TestHost(BSize viewport) : IUiHost
    {
        public BSize ViewportSize => viewport;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
