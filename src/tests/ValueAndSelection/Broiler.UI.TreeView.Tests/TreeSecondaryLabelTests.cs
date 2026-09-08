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
