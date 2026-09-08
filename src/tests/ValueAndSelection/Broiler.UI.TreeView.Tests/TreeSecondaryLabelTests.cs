using Broiler.Graphics;
using Broiler.UI.TreeView.Standard;

namespace Broiler.UI.TreeView.Tests;

/// <summary>
/// Where a row's secondary label lands.
///
/// A file tree's is a word beside a name; a tree reporting findings carries a
/// sentence there, and beside the name it runs off the end of the pane. The
/// placement is the host's to choose, and choosing the second one changes the
/// row height — which is what the hit test and the visible capacity divide by,
/// so the two have to move together.
/// </summary>
public sealed class TreeSecondaryLabelTests
{
    [Fact(Timeout = 600000)]
    public void Inline_Is_The_Default_And_Keeps_One_Line_Rows()
    {
        using TreeScene scene = TreeStandardHarness.Create(Source());

        Assert.Equal(TreeSecondaryLabelPlacement.Inline, scene.Tree.SecondaryLabelPlacement);

        // Both labels on the row's own line, the secondary to the right of the
        // label rather than under it.
        BRenderList list = scene.Render();
        BPoint label = Assert.Single(TextAt(list, "alpha"));
        BPoint secondary = Assert.Single(TextAt(list, "about alpha"));

        Assert.Equal(label.Y, secondary.Y, 3);
        Assert.True(secondary.X > label.X, "the secondary label sits after the label");
    }

    [Fact(Timeout = 600000)]
    public void Below_The_Label_Puts_It_On_Its_Own_Line()
    {
        using TreeScene scene = TreeStandardHarness.Create(Source());
        scene.Tree.SecondaryLabelPlacement = TreeSecondaryLabelPlacement.BelowLabel;

        BRenderList list = scene.Render();
        BPoint label = Assert.Single(TextAt(list, "alpha"));
        BPoint secondary = Assert.Single(TextAt(list, "about alpha"));

        // Under it, and at the same left edge, so a column of answers reads
        // down the pane instead of starting wherever each name happened to end.
        Assert.True(secondary.Y > label.Y, "the secondary label is on the next line");
        Assert.Equal(label.X, secondary.X, 3);
    }

    /// <summary>
    /// The row height follows the placement, and every consequence of the row
    /// height with it. A click that still divided by the one-line height would
    /// land on a row the user did not press.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Row_Height_And_The_Hit_Test_Follow_The_Placement()
    {
        using TreeScene scene = TreeStandardHarness.Create(Source());
        double single = scene.Tree.RowHeight;

        scene.Tree.SecondaryLabelPlacement = TreeSecondaryLabelPlacement.BelowLabel;
        scene.Render();

        Assert.True(scene.Tree.RowHeight > single * 1.5, "a two-line row is about twice as tall");
        Assert.Equal(0, scene.Tree.HitTestRow(scene.RowPoint(0)));
        Assert.Equal(1, scene.Tree.HitTestRow(scene.RowPoint(1)));
    }

    /// <summary>
    /// A tree that virtualizes has always known how much it was not showing and
    /// never said so. A pane that scrolls with no bar on it looks like a pane
    /// that ends where its last row does.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void The_Bar_Appears_Only_When_There_Are_More_Rows_Than_Fit()
    {
        var few = new LabelledTreeSource();
        few.Add("/", "/alpha", "/beta");
        using (TreeScene small = TreeStandardHarness.Create(few))
        {
            small.Render();
            Assert.False(small.Tree.HasVerticalScrollbar);
            Assert.Equal(small.Tree.Bounds.Width, small.Tree.ContentBounds.Width, 3);
        }

        using TreeScene scene = TreeStandardHarness.Create(Many());
        scene.Render();

        Assert.True(scene.Tree.HasVerticalScrollbar);
        Assert.True(
            scene.Tree.ContentBounds.Width < scene.Tree.Bounds.Width,
            "the bar takes its width from the rows");
    }

    /// <summary>
    /// Dragging the thumb scrolls, and lands on a whole row — the tree renders
    /// and hit-tests by row, so a bar that left it half way down one would draw
    /// every row half out of its own rectangle.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Dragging_The_Thumb_Scrolls_The_Rows()
    {
        using TreeScene scene = TreeStandardHarness.Create(Many());
        scene.Render();

        double right = scene.Tree.Bounds.Right - 2;
        scene.Route.Dispatch(TreeStandardHarness.MouseDown(right, scene.Tree.Bounds.Top + 4));
        scene.Route.Dispatch(TreeStandardHarness.MouseMove(right, scene.Tree.Bounds.Bottom));
        scene.Route.Dispatch(TreeStandardHarness.MouseUp(right, scene.Tree.Bounds.Bottom));

        Assert.True(scene.Tree.FirstVisibleRow > 0, "the thumb dragged the rows down");
        Assert.Equal(0, scene.Tree.FirstVisibleRow % 1);
    }

    /// <summary>A press on the bar scrolls; it does not select the row beside it.</summary>
    [Fact(Timeout = 600000)]
    public void A_Press_On_The_Bar_Selects_Nothing()
    {
        using TreeScene scene = TreeStandardHarness.Create(Many());
        scene.Render();

        scene.Click(new BPoint(scene.Tree.Bounds.Right - 2, scene.Tree.Bounds.Bottom - 10));

        Assert.Empty(scene.Tree.Selection);
        Assert.True(scene.Tree.FirstVisibleRow > 0, "the press paged towards itself");
    }

    /// <summary>
    /// A row wider than the pane is a row whose answer cannot be read. Shift
    /// and the wheel is the convention every editor shares, and the only way to
    /// reach it on a mouse with one wheel.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void A_Row_Wider_Than_The_Pane_Can_Be_Scrolled_To()
    {
        var wide = new LabelledTreeSource();
        wide.Add("/", "/" + new string('w', 200));

        using TreeScene scene = TreeStandardHarness.Create(wide);

        // Two frames: the first learns how wide the row is while drawing it,
        // and the second is laid out knowing.
        scene.Render();
        scene.Render();

        Assert.True(scene.Tree.HasHorizontalScrollbar);
        Assert.True(
            scene.Tree.ContentBounds.Height < scene.Tree.Bounds.Height,
            "the bar takes its height from the rows");

        BPoint over = scene.RowPoint(0);
        scene.Route.Dispatch(TreeStandardHarness.MouseWheel(over.X, over.Y, -2, shift: true));
        scene.Render();

        BRenderList list = scene.Render();
        double scrolled = Assert.Single(TextAt(list, new string('w', 200))).X;
        Assert.True(scrolled < scene.Tree.Bounds.Left, "the row moved left, off its own start");
    }

    /// <summary>Without Shift the same wheel scrolls rows, not columns.</summary>
    [Fact(Timeout = 600000)]
    public void The_Plain_Wheel_Still_Scrolls_Rows()
    {
        using TreeScene scene = TreeStandardHarness.Create(Many());
        scene.Render();

        BPoint over = scene.RowPoint(0);
        scene.Route.Dispatch(TreeStandardHarness.MouseWheel(over.X, over.Y, -2, shift: false));

        Assert.True(scene.Tree.FirstVisibleRow > 0);
    }

    private static LabelledTreeSource Many()
    {
        var source = new LabelledTreeSource();
        source.Add("/", [.. Enumerable.Range(0, 200).Select(index => $"/row{index}")]);
        return source;
    }

    private static LabelledTreeSource Source()
    {
        var source = new LabelledTreeSource();
        source.Add("/", "/alpha", "/beta");
        return source;
    }

    private static IReadOnlyList<BPoint> TextAt(BRenderList list, string text) =>
    [
        .. list.Commands
            .OfType<BRenderCommand.DrawText>()
            .Where(command => command.Text.Text == text)
            .Select(command => command.Origin),
    ];

    /// <summary>Rows that carry a secondary label, which the counting source does not.</summary>
    private sealed class LabelledTreeSource : ITreeDataSource
    {
        private readonly Dictionary<string, List<string>> _children = [];

        public TreeNodeId Root => new("/");

        public void Add(string parent, params string[] children)
        {
            if (!_children.TryGetValue(parent, out List<string>? list))
                _children[parent] = list = [];
            list.AddRange(children);
        }

        public int GetChildCount(TreeNodeId node) =>
            _children.TryGetValue(node.Value, out List<string>? list) ? list.Count : 0;

        public TreeNodeId GetChild(TreeNodeId node, int index) => new(_children[node.Value][index]);

        public bool CanExpand(TreeNodeId node) => GetChildCount(node) > 0;

        public TreeNodePresentation GetPresentation(TreeNodeId node) =>
            new(node, Name(node), $"about {Name(node)}");

        private static string Name(TreeNodeId node) =>
            node.Value[(node.Value.LastIndexOf('/') + 1)..];
    }
}
