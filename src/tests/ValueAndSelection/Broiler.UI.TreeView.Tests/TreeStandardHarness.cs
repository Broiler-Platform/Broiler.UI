using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;
using Broiler.UI.TreeView.Standard;

namespace Broiler.UI.TreeView.Tests;

internal sealed class TreeTestHost : IUiHost
{
    public TreeTestHost(BSize viewportSize) => ViewportSize = viewportSize;

    public BSize ViewportSize { get; }

    public double Scale => 1;

    public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

    public void Invalidate(UiInvalidation invalidation)
    {
    }

    public void Present(BRenderList renderList)
    {
    }
}

/// <summary>A clock the test drives, so the double-click window is deterministic.</summary>
internal sealed class ManualClock : IUiClock
{
    public UiTimestamp Now { get; set; }

    public void Advance(TimeSpan delta) => Now = new UiTimestamp(Now.Elapsed + delta);
}

internal sealed class TreeScene : IDisposable
{
    public required UiSession Session { get; init; }

    public required StandardTreeView Tree { get; init; }

    public required ManualClock Clock { get; init; }

    public required StandardInputRoute Route { get; init; }

    /// <summary>The nodes the tree announced as activated, in order.</summary>
    public List<string> Activated { get; } = [];

    /// <summary>A point inside the label of the row at <paramref name="index"/>.</summary>
    public BPoint RowPoint(int index) => new(
        Tree.Bounds.Left + 120,
        Tree.Bounds.Top + ((index - Tree.FirstVisibleRow + 0.5) * Tree.RowHeight));

    /// <summary>A point on the expander triangle of the row at <paramref name="index"/>.</summary>
    public BPoint ExpanderPoint(int index, int depth) => new(
        Tree.Bounds.Left + 4 + (depth * 16) + 4,
        Tree.Bounds.Top + ((index - Tree.FirstVisibleRow + 0.5) * Tree.RowHeight));

    /// <summary>A full press-and-release on a point, as a mouse delivers one.</summary>
    public void Click(BPoint point)
    {
        Route.Dispatch(TreeStandardHarness.MouseDown(point.X, point.Y));
        Route.Dispatch(TreeStandardHarness.MouseUp(point.X, point.Y));
    }

    public BRenderList Render() => Session.RenderFrame();

    public void Dispose() => Session.Dispose();
}

internal static class TreeStandardHarness
{
    /// <summary>The rect the tree is arranged into, so pointer maths is unambiguous.</summary>
    public static readonly BRect TreeBounds = new(20, 20, 280, 400);

    public static TreeScene Create(ITreeDataSource source)
    {
        var host = new TreeTestHost(new BSize(400, 500));
        var clock = new ManualClock();
        UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .WithClock(clock)
            .Build(host);

        var tree = new StandardTreeView { DataSource = source };
        session.AddRoot(new TreeRoot(tree));

        var scene = new TreeScene
        {
            Session = session,
            Tree = tree,
            Clock = clock,
            Route = new StandardInputRoute(session),
        };

        tree.NodeActivated += (_, e) => scene.Activated.Add(e.Node.Value);

        // One frame so bounds, and with them every hit test, are real.
        scene.Render();
        session.SetFocus(tree);
        return scene;
    }

    public static InputEventHeader Header(string id) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(1, TimeSpan.TicksPerSecond, "tree"), 1);

    public static MouseButtonEvent MouseDown(
        double x,
        double y,
        MouseButton button = MouseButton.Left,
        InputModifiers modifiers = InputModifiers.None) =>
        new(
            Header("mouse"),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            button == MouseButton.Right ? MouseButtons.Right : MouseButtons.Left,
            button,
            MouseButtonTransition.Down,
            InputEventSource.Synthetic,
            modifiers);

    public static MouseButtonEvent MouseUp(double x, double y, MouseButton button = MouseButton.Left) =>
        new(
            Header("mouse"),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.None,
            button,
            MouseButtonTransition.Up,
            InputEventSource.Synthetic);

    public static KeyboardKeyEvent Key(string name) =>
        new(
            Header("keyboard"),
            KeyboardKey.FromName(name),
            KeyboardKeyTransition.Down,
            KeyboardModifierState.None,
            0,
            0,
            0,
            false,
            false,
            Source: InputEventSource.Synthetic);

    /// <summary>Places the tree at a fixed offset so pointer coordinates are unambiguous.</summary>
    private sealed class TreeRoot : UiElement
    {
        private readonly UiElement _tree;

        public TreeRoot(UiElement tree)
        {
            _tree = tree;
            AddChild(tree);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _tree.Measure(availableSize);
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect) => _tree.Arrange(TreeBounds);
    }
}
