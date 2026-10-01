using System;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Windowing;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Splitter.Standard;
using Broiler.UI.Standard;
using Xunit;

namespace Broiler.UI.Splitter.Tests;

public sealed class StandardSplitContainerTests
{
    [Fact]
    public void Initial_Layout_And_Minima_Are_Enforced()
    {
        using var scene = CreateScene(800, 600);
        var container = scene.Container;
        container.FirstPaneMinimumSize = 200;
        container.SecondPaneMinimumSize = 300;
        container.SplitterFraction = 0.5;

        scene.Session.RenderFrame();

        double thickness = container.Splitter.PreferredSize.Width;
        double netExtent = 800 - thickness;
        double expectedFirst = Math.Round(netExtent * 0.5);
        double expectedSecond = netExtent - expectedFirst;

        Assert.Equal(expectedFirst, scene.Pane1.Bounds.Width);
        Assert.Equal(thickness, container.Splitter.Bounds.Width);
        Assert.Equal(expectedSecond, scene.Pane2.Bounds.Width);
        Assert.Equal(600, scene.Pane1.Bounds.Height);
        Assert.Equal(600, scene.Pane2.Bounds.Height);

        // Try setting a fraction that violates FirstPaneMinimumSize (200 / netExtent > 0.05)
        container.SplitterFraction = 0.05;
        scene.Session.RenderFrame();
        Assert.True(scene.Pane1.Bounds.Width >= container.FirstPaneMinimumSize - 1);

        // Try setting a fraction that violates SecondPaneMinimumSize
        container.SplitterFraction = 0.95;
        scene.Session.RenderFrame();
        Assert.True(scene.Pane2.Bounds.Width >= container.SecondPaneMinimumSize - 1);
    }

    [Fact]
    public void Keyboard_Resizing_Adjusts_SplitterFraction_And_Raises_Event()
    {
        using var scene = CreateScene(800, 600);
        var container = scene.Container;
        UiSplitterPositionChangedEventArgs? raised = null;
        container.SplitterPositionChanged += (_, args) => raised = args;

        scene.Session.SetFocus(container.Splitter);
        double initialFraction = container.SplitterFraction;

        // Press Right arrow to increase fraction
        scene.Route.Dispatch(Key("Right", BVirtualKey.Right));
        scene.Session.RenderFrame();

        Assert.True(container.SplitterFraction > initialFraction);
        Assert.NotNull(raised);
        Assert.Equal(initialFraction, raised!.OldFraction);
        Assert.Equal(container.SplitterFraction, raised!.NewFraction);
    }

    [Fact]
    public void Pointer_Drag_Resizing_Adjusts_Position()
    {
        using var scene = CreateScene(800, 600);
        var container = scene.Container;
        scene.Session.RenderFrame();

        double initialSplitterX = container.Splitter.Bounds.Left;

        // Drag 40 pixels to the right
        scene.Route.Dispatch(MouseDown(initialSplitterX + 4, 300));
        scene.Route.Dispatch(MouseMove(initialSplitterX + 44, 300));
        scene.Route.Dispatch(MouseUp(initialSplitterX + 44, 300));
        scene.Session.RenderFrame();

        Assert.True(container.Splitter.Bounds.Left > initialSplitterX);
    }

    [Fact]
    public void Collapsed_Pane_Behavior()
    {
        using var scene = CreateScene(800, 600);
        var container = scene.Container;
        scene.Session.RenderFrame();

        // Collapse FirstPane
        container.CollapseFirstPane();
        scene.Session.RenderFrame();

        Assert.True(container.IsFirstPaneCollapsed);
        Assert.Equal(BRect.Empty, scene.Pane1.Bounds);
        Assert.Equal(BRect.Empty, container.Splitter.Bounds);
        Assert.Equal(new BRect(0, 0, 800, 600), scene.Pane2.Bounds);

        // Collapse SecondPane (switches from first)
        container.CollapseSecondPane();
        scene.Session.RenderFrame();

        Assert.True(container.IsSecondPaneCollapsed);
        Assert.False(container.IsFirstPaneCollapsed);
        Assert.Equal(new BRect(0, 0, 800, 600), scene.Pane1.Bounds);
        Assert.Equal(BRect.Empty, container.Splitter.Bounds);
        Assert.Equal(BRect.Empty, scene.Pane2.Bounds);

        // Restore
        container.RestorePanes();
        scene.Session.RenderFrame();

        Assert.False(container.IsFirstPaneCollapsed);
        Assert.False(container.IsSecondPaneCollapsed);
        Assert.True(scene.Pane1.Bounds.Width > 0);
        Assert.True(scene.Pane2.Bounds.Width > 0);
        Assert.True(container.Splitter.Bounds.Width > 0);
    }

    [Fact]
    public void Horizontal_Orientation_Splits_Vertically()
    {
        using var scene = CreateScene(800, 600);
        var container = scene.Container;
        container.Orientation = UiSplitterOrientation.Horizontal;
        container.SplitterFraction = 0.4;
        scene.Session.RenderFrame();

        double thickness = container.Splitter.PreferredSize.Height;
        double netExtent = 600 - thickness;
        double expectedFirstHeight = Math.Round(netExtent * 0.4);
        double expectedSecondHeight = netExtent - expectedFirstHeight;

        Assert.Equal(800, scene.Pane1.Bounds.Width);
        Assert.Equal(800, scene.Pane2.Bounds.Width);
        Assert.Equal(expectedFirstHeight, scene.Pane1.Bounds.Height);
        Assert.Equal(thickness, container.Splitter.Bounds.Height);
        Assert.Equal(expectedSecondHeight, scene.Pane2.Bounds.Height);
    }

    [Fact]
    public void Responsive_Layout_With_Tight_Space_Does_Not_Fail_Or_Invert()
    {
        // When container is narrower than the sum of minima (e.g. 150 < 100 + 100)
        using var scene = CreateScene(150, 400);
        var container = scene.Container;
        container.FirstPaneMinimumSize = 100;
        container.SecondPaneMinimumSize = 100;

        scene.Session.RenderFrame();

        Assert.True(scene.Pane1.Bounds.Width > 0);
        Assert.True(scene.Pane2.Bounds.Width > 0);
        Assert.False(double.IsNaN(scene.Pane1.Bounds.Width));
        Assert.False(double.IsNaN(scene.Pane2.Bounds.Width));
        Assert.True(scene.Pane1.Bounds.Width + container.Splitter.Bounds.Width + scene.Pane2.Bounds.Width <= 151);
    }

    [Fact]
    public void Theming_And_Semantics()
    {
        using var scene = CreateScene(800, 600);
        var container = scene.Container;
        container.ApplyTheme(StandardThemeTokens.HighContrastDark);

        scene.Session.RenderFrame();

        UiSemanticNode node = container.GetSemanticNode();
        Assert.Equal(UiSemanticRole.Panel, node.Role);
        Assert.Contains("vertical", node.Name);
        Assert.Contains("50", node.Name);
        Assert.Equal(3, node.Children.Count); // Pane1, Splitter, Pane2
    }

    private static ContainerScene CreateScene(double width, double height)
    {
        var host = new Host(new BSize(width, height));
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);

        var pane1 = new TestPane();
        var pane2 = new TestPane();
        var container = new StandardSplitContainer
        {
            FirstPane = pane1,
            SecondPane = pane2,
            PreferredSize = new BSize(width, height),
        };

        session.AddRoot(container);
        return new ContainerScene(session, container, pane1, pane2, new StandardInputRoute(session));
    }

    private static InputEventHeader Header() =>
        new(InputDeviceId.FromOpaqueValue("test"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"), 1);

    private static KeyboardKeyEvent Key(string name, int native) =>
        new(Header(), KeyboardKey.FromName(name), KeyboardKeyTransition.Down,
            KeyboardModifierState.None, native, 0, 0, false, false, Source: InputEventSource.Synthetic);

    private static MouseButtonEvent MouseDown(double x, double y) =>
        new(Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left,
            MouseButton.Left, MouseButtonTransition.Down, InputEventSource.Synthetic);

    private static MouseMoveEvent MouseMove(double x, double y) =>
        new(Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.Left, InputEventSource.Synthetic);

    private static MouseButtonEvent MouseUp(double x, double y) =>
        new(Header(), InputPoint.ClientDeviceIndependentPixels(x, y), MouseButtons.None,
            MouseButton.Left, MouseButtonTransition.Up, InputEventSource.Synthetic);

    private sealed record ContainerScene(
        UiSession Session,
        StandardSplitContainer Container,
        TestPane Pane1,
        TestPane Pane2,
        StandardInputRoute Route) : IDisposable
    {
        public void Dispose() => Session.Dispose();
    }

    private sealed class TestPane : UiElement
    {
        protected override BSize MeasureCore(BSize availableSize) =>
            new(double.IsInfinity(availableSize.Width) ? 100 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 100 : availableSize.Height);

        protected override UiSemanticNode GetSemanticNodeCore() =>
            new(UiSemanticRole.Group, "TestPane", Bounds, UiSemanticState.Visible, []);
    }

    private sealed class Host(BSize viewport) : IUiHost
    {
        public BSize ViewportSize => viewport;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
