using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.Standard;
using Broiler.UI.TreeView.Standard;

namespace Broiler.UI.TreeView.Tests;

/// <summary>
/// A tree's semantic children are the rows in view, so whatever scrolls the tree changes them. Hosts
/// learn of a change through a semantic invalidation; one that heard only of a render kept exposing
/// the rows that had scrolled away until the tree's next state change.
/// </summary>
public sealed class TreeRowsInViewSemanticsTests
{
    [Fact]
    public void Scrolling_With_The_Wheel_Tells_Hosts_The_Rows_In_View_Changed()
    {
        using TreeScene scene = TreeStandardHarness.Create(Many());
        using var heard = new RowsHeard(scene);

        BPoint over = scene.RowPoint(0);
        Assert.True(scene.Route.Dispatch(TreeStandardHarness.MouseWheel(over.X, over.Y, -2)));

        Assert.True(scene.Tree.FirstVisibleRow > 0);
        heard.AssertLastShowsTheRowsInView();
    }

    [Fact]
    public void Scrolling_With_The_Bar_Tells_Hosts_The_Rows_In_View_Changed()
    {
        using TreeScene scene = TreeStandardHarness.Create(Many());
        Assert.True(scene.Tree.HasVerticalScrollbar);
        using var heard = new RowsHeard(scene);

        // A press on the track below the thumb pages down.
        BRect bounds = scene.Tree.Bounds;
        BPoint track = new(bounds.Right - 3, bounds.Bottom - 6);
        Assert.True(scene.Route.Dispatch(TreeStandardHarness.MouseDown(track.X, track.Y)));
        scene.Route.Dispatch(TreeStandardHarness.MouseUp(track.X, track.Y));

        Assert.True(scene.Tree.FirstVisibleRow > 0);
        heard.AssertLastShowsTheRowsInView();
    }

    [Fact]
    public void Bringing_The_Focused_Row_Into_View_Tells_Hosts_After_It_Scrolled()
    {
        // Moving the focus changes the selection first, while the old rows are still in view, and
        // scrolls after. The last thing a host hears has to be the scroll.
        using TreeScene scene = TreeStandardHarness.Create(Many());
        using var heard = new RowsHeard(scene);

        Assert.True(scene.Route.Dispatch(TreeStandardHarness.Key("End")));

        Assert.True(scene.Tree.FirstVisibleRow > 0);
        heard.AssertLastShowsTheRowsInView();
    }

    [Fact]
    public void Setting_The_First_Visible_Row_Tells_Hosts_Only_When_It_Moves()
    {
        using TreeScene scene = TreeStandardHarness.Create(Many());
        using var heard = new RowsHeard(scene);

        scene.Tree.FirstVisibleRow = 0;
        Assert.Empty(heard.FirstRows);

        scene.Tree.FirstVisibleRow = 40;
        heard.AssertLastShowsTheRowsInView();
        Assert.Contains(scene.Session.Invalidations, invalidation =>
            ReferenceEquals(invalidation.Element, scene.Tree) && invalidation.Kind.HasFlag(UiInvalidationKind.Semantic));
    }

    [Fact]
    public void Resizing_The_Tree_Tells_Hosts_The_Rows_In_View_Changed()
    {
        var host = new ResizableHost(new BSize(300, 200));
        using UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(new ImmediateUiDispatcher())
            .Build(host);
        var tree = new StandardTreeView { DataSource = Many() };
        session.AddRoot(tree);
        session.RenderFrame();
        int before = tree.VisibleRowCapacity;
        var rowsHeard = new List<int>();
        session.SemanticChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Element, tree) && e.Change == UiSemanticChangeKind.StateChanged)
                rowsHeard.Add(tree.GetSemanticNode().Children.Count);
        };

        host.ViewportSize = new BSize(300, 400);
        session.RenderFrame();

        Assert.True(tree.VisibleRowCapacity > before);
        Assert.Equal(tree.VisibleRowCapacity, Assert.Single(rowsHeard));

        // A frame at the same size changes nothing and says nothing.
        tree.InvalidateArrange();
        session.RenderFrame();
        Assert.Single(rowsHeard);
    }

    private static CountingTreeSource Many()
    {
        var source = new CountingTreeSource();
        source.Add("/", [.. Enumerable.Range(0, 200).Select(index => $"/row{index}")]);
        return source;
    }

    /// <summary>
    /// The first row a host would read from the tree's semantic children each time the tree reports a
    /// semantic change, read when the change is reported.
    /// </summary>
    private sealed class RowsHeard : IDisposable
    {
        private readonly TreeScene _scene;

        public RowsHeard(TreeScene scene)
        {
            _scene = scene;
            scene.Session.SemanticChanged += OnSemanticChanged;
        }

        public List<string> FirstRows { get; } = [];

        public void AssertLastShowsTheRowsInView()
        {
            string expected = Label(_scene.Tree.Rows[_scene.Tree.FirstVisibleRow].Id);
            Assert.NotEmpty(FirstRows);
            Assert.StartsWith(expected + ",", FirstRows[^1], StringComparison.Ordinal);
        }

        public void Dispose() => _scene.Session.SemanticChanged -= OnSemanticChanged;

        private void OnSemanticChanged(object? sender, UiSemanticChangedEventArgs e)
        {
            if (ReferenceEquals(e.Element, _scene.Tree) && e.Change == UiSemanticChangeKind.StateChanged)
                FirstRows.Add(_scene.Tree.GetSemanticNode().Children[0].Name);
        }

        private static string Label(TreeNodeId node) => node.Value[(node.Value.LastIndexOf('/') + 1)..];
    }

    private sealed class ResizableHost(BSize size) : IUiHost
    {
        public BSize ViewportSize { get; set; } = size;

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
