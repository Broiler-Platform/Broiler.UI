using Broiler.UI.TreeView.Standard;

namespace Broiler.UI.TreeView.Tests;

/// <summary>
/// A data source that counts what the view asked it, so the virtualization
/// claims can be asserted rather than assumed. A tree that renders correctly
/// while enumerating everything is the failure this suite exists to catch.
/// </summary>
internal sealed class CountingTreeSource : ITreeDataSource
{
    private readonly Dictionary<string, List<string>> _children = [];

    public int ChildCountQueries { get; private set; }

    public int ChildQueries { get; private set; }

    public int PresentationQueries { get; private set; }

    public TreeNodeId Root => new("/");

    public void Add(string parent, params string[] children)
    {
        if (!_children.TryGetValue(parent, out List<string>? list))
            _children[parent] = list = [];
        list.AddRange(children);
    }

    public void ResetCounts()
    {
        ChildCountQueries = 0;
        ChildQueries = 0;
        PresentationQueries = 0;
    }

    public int GetChildCount(TreeNodeId node)
    {
        ChildCountQueries++;
        return _children.TryGetValue(node.Value, out List<string>? list) ? list.Count : 0;
    }

    public TreeNodeId GetChild(TreeNodeId node, int index)
    {
        ChildQueries++;
        return new TreeNodeId(_children[node.Value][index]);
    }

    public bool CanExpand(TreeNodeId node) =>
        _children.TryGetValue(node.Value, out List<string>? list) && list.Count > 0;

    public TreeNodePresentation GetPresentation(TreeNodeId node)
    {
        PresentationQueries++;
        string label = node.Value[(node.Value.LastIndexOf('/') + 1)..];
        return new TreeNodePresentation(node, label);
    }
}

public sealed class TreeViewTests
{
    private static CountingTreeSource BuildSolution(int projects, int filesPerProject)
    {
        var source = new CountingTreeSource();
        var projectIds = new List<string>();
        for (int p = 0; p < projects; p++)
        {
            string project = $"/Project{p}";
            projectIds.Add(project);
            source.Add(project, [.. Enumerable.Range(0, filesPerProject).Select(f => $"{project}/File{f}.cs")]);
        }

        source.Add("/", [.. projectIds]);
        return source;
    }

    [Fact]
    public void A_Collapsed_Tree_Never_Enumerates_What_It_Does_Not_Show()
    {
        // A thousand files across ten projects, all collapsed.
        CountingTreeSource source = BuildSolution(projects: 10, filesPerProject: 100);
        using var tree = new StandardTreeView { DataSource = source };
        source.ResetCounts();

        IReadOnlyList<TreeRow> rows = tree.Rows;

        // Ten rows, and the file lists were never walked.
        Assert.Equal(10, rows.Count);
        Assert.Equal(0, source.ChildQueries - 10);
        Assert.All(rows, row => Assert.True(row.HasChildren));
        Assert.All(rows, row => Assert.False(row.IsExpanded));
    }

    [Fact]
    public void Expanding_One_Node_Adds_Only_Its_Children()
    {
        CountingTreeSource source = BuildSolution(projects: 10, filesPerProject: 100);
        using var tree = new StandardTreeView { DataSource = source };

        Assert.True(tree.Expand(new TreeNodeId("/Project3")));

        // Ten projects plus one project's hundred files. The other nine
        // hundred files were never materialized.
        Assert.Equal(110, tree.Rows.Count);
        Assert.Contains(tree.Rows, row => row.Id.Value == "/Project3/File0.cs" && row.Depth == 1);
        Assert.DoesNotContain(tree.Rows, row => row.Id.Value.StartsWith("/Project4/", StringComparison.Ordinal));
    }

    [Fact]
    public void Semantics_Describe_Only_The_Visible_Window()
    {
        CountingTreeSource source = BuildSolution(projects: 1, filesPerProject: 1_000);
        using var tree = new StandardTreeView { DataSource = source, VisibleRowCapacity = 20 };
        tree.Expand(new TreeNodeId("/Project0"));

        UiSemanticNode node = tree.GetSemanticNode();

        // 1,001 rows exist; a screen reader is handed the twenty on screen.
        Assert.Equal(1_001, tree.Rows.Count);
        Assert.Equal(20, node.Children.Count);
        Assert.Contains("1001 items", node.Name);
    }

    [Fact]
    public void A_Row_Announces_Its_Level_And_Position_Within_That_Level()
    {
        var source = new CountingTreeSource();
        source.Add("/", "/A", "/B");
        source.Add("/A", "/A/one", "/A/two", "/A/three");
        using var tree = new StandardTreeView { DataSource = source, VisibleRowCapacity = 10 };
        tree.Expand(new TreeNodeId("/A"));

        IReadOnlyList<UiSemanticNode> rows = tree.GetSemanticNode().Children;

        Assert.Contains(rows, row => row.Name.Contains("A, expanded, level 1, 1 of 2", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Name.Contains("two, level 2, 2 of 3", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Name.Contains("B, level 1, 2 of 2", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Row_Reports_Exactly_One_Expansion_State_Only_When_It_Can_Expand()
    {
        var source = new CountingTreeSource();
        source.Add("/", "/A", "/B", "/C");
        source.Add("/A", "/A/one");
        source.Add("/C", "/C/one");
        using var tree = new StandardTreeView { DataSource = source, VisibleRowCapacity = 10 };
        tree.Expand(new TreeNodeId("/A"));

        IReadOnlyList<UiSemanticNode> rows = tree.GetSemanticNode().Children;
        const UiSemanticState expansion = UiSemanticState.Expanded | UiSemanticState.Collapsed;

        Assert.Equal(UiSemanticState.Expanded, rows[0].State & expansion);
        Assert.Equal(UiSemanticState.None, rows[1].State & expansion);
        Assert.Equal(UiSemanticState.None, rows[2].State & expansion);
        Assert.Equal(UiSemanticState.Collapsed, rows[3].State & expansion);
    }

    [Fact]
    public void Positions_Within_A_Level_Follow_Every_Change_To_The_Rows()
    {
        // The positions are worked out once for each set of rows, so every change that rebuilds the
        // rows (expanding, collapsing, a refresh) and every scroll is checked against a count made
        // from Rows directly.
        var source = new CountingTreeSource();
        source.Add("/", "/A", "/B", "/C");
        source.Add("/A", "/A/one", "/A/two", "/A/three");
        source.Add("/A/two", "/A/two/x", "/A/two/y");
        source.Add("/B", "/B/one", "/B/two");
        using var tree = new StandardTreeView { DataSource = source, VisibleRowCapacity = 100 };

        AssertPositionsMatchRows(tree, expectedRows: 3);
        tree.Expand(new TreeNodeId("/A"));
        AssertPositionsMatchRows(tree, expectedRows: 6);
        tree.Expand(new TreeNodeId("/A/two"));
        AssertPositionsMatchRows(tree, expectedRows: 8);
        tree.Expand(new TreeNodeId("/B"));
        AssertPositionsMatchRows(tree, expectedRows: 10);
        Assert.Contains(tree.GetSemanticNode().Children, row => row.Name.EndsWith("y, level 3, 2 of 2", StringComparison.Ordinal));
        Assert.Contains(tree.GetSemanticNode().Children, row => row.Name.EndsWith("C, level 1, 3 of 3", StringComparison.Ordinal));

        tree.Collapse(new TreeNodeId("/A"));
        AssertPositionsMatchRows(tree, expectedRows: 5);
        source.Add("/", "/D");
        tree.Refresh();
        AssertPositionsMatchRows(tree, expectedRows: 6);
        Assert.Contains(tree.GetSemanticNode().Children, row => row.Name.EndsWith("C, level 1, 3 of 4", StringComparison.Ordinal));

        tree.Expand(new TreeNodeId("/A"));
        tree.VisibleRowCapacity = 3;
        for (int first = 0; first < tree.Rows.Count; first++)
        {
            tree.FirstVisibleRow = first;
            AssertPositionsMatchRows(tree, expectedRows: Math.Min(3, tree.Rows.Count - first));
        }
    }

    /// <summary>
    /// Every described row's level and position against a count made by walking <see cref="UiTreeView.Rows"/>:
    /// the rows at the same depth since the last shallower row, and up to the next one.
    /// </summary>
    private static void AssertPositionsMatchRows(UiTreeView tree, int expectedRows)
    {
        IReadOnlyList<TreeRow> rows = tree.Rows;
        IReadOnlyList<UiSemanticNode> described = tree.GetSemanticNode().Children;
        Assert.Equal(expectedRows, described.Count);
        for (int slot = 0; slot < described.Count; slot++)
        {
            int index = tree.FirstVisibleRow + slot;
            int depth = rows[index].Depth;
            int start = index;
            while (start > 0 && rows[start - 1].Depth >= depth)
                start--;
            int end = index;
            while (end < rows.Count - 1 && rows[end + 1].Depth >= depth)
                end++;

            int position = Enumerable.Range(start, index - start + 1).Count(i => rows[i].Depth == depth);
            int count = Enumerable.Range(start, end - start + 1).Count(i => rows[i].Depth == depth);
            Assert.EndsWith($", level {depth + 1}, {position} of {count}", described[slot].Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Expansion_And_Selection_Survive_A_Data_Source_Refresh()
    {
        CountingTreeSource source = BuildSolution(projects: 3, filesPerProject: 5);
        using var tree = new StandardTreeView { DataSource = source };
        tree.Expand(new TreeNodeId("/Project1"));
        tree.SetSelection([new TreeNodeId("/Project1/File2.cs")]);

        // The workspace rebuilt its nodes; the IDs are the same.
        tree.Refresh();

        Assert.True(tree.IsExpanded(new TreeNodeId("/Project1")));
        Assert.Equal("/Project1/File2.cs", Assert.Single(tree.Selection).Value);
    }

    [Fact]
    public void Collapsing_A_Subtree_Moves_Focus_Out_Of_It()
    {
        var source = new CountingTreeSource();
        source.Add("/", "/A");
        source.Add("/A", "/A/child");
        using var tree = new StandardTreeView { DataSource = source };
        tree.Expand(new TreeNodeId("/A"));
        tree.SetSelection([new TreeNodeId("/A/child")]);
        Assert.Equal("/A/child", tree.FocusedNode.Value);

        tree.Collapse(new TreeNodeId("/A"));

        // Focus never stays on a row that no longer exists.
        Assert.Equal("/A", tree.FocusedNode.Value);
    }

    [Fact]
    public void Keyboard_Navigation_Walks_The_Expanded_Set()
    {
        CountingTreeSource source = BuildSolution(projects: 3, filesPerProject: 2);
        using var tree = new StandardTreeView { DataSource = source, VisibleRowCapacity = 10 };

        Assert.True(tree.MoveFocusToFirst());
        Assert.Equal("/Project0", tree.FocusedNode.Value);

        Assert.True(tree.MoveFocus(1, extendSelection: false));
        Assert.Equal("/Project1", tree.FocusedNode.Value);

        Assert.True(tree.MoveFocusToLast());
        Assert.Equal("/Project2", tree.FocusedNode.Value);

        // Past the end is a no-op rather than an error.
        Assert.False(tree.MoveFocus(1, extendSelection: false));
    }

    [Fact]
    public void Type_Ahead_Finds_The_Next_Match_And_Wraps()
    {
        var source = new CountingTreeSource();
        source.Add("/", "/Alpha", "/Beta", "/Alpine");
        using var tree = new StandardTreeView { DataSource = source, VisibleRowCapacity = 10 };

        Assert.True(tree.TypeAhead("Al"));
        Assert.Equal("/Alpha", tree.FocusedNode.Value);

        Assert.True(tree.TypeAhead("Al"));
        Assert.Equal("/Alpine", tree.FocusedNode.Value);

        // Wraps back rather than stopping at the end.
        Assert.True(tree.TypeAhead("Al"));
        Assert.Equal("/Alpha", tree.FocusedNode.Value);
    }

    [Fact]
    public void Reveal_Expands_Every_Ancestor_And_Scrolls_The_Node_Into_View()
    {
        CountingTreeSource source = BuildSolution(projects: 5, filesPerProject: 40);
        using var tree = new StandardTreeView { DataSource = source, VisibleRowCapacity = 10 };

        Assert.True(tree.RevealNode(
            new TreeNodeId("/Project4/File39.cs"),
            [new TreeNodeId("/Project4")]));

        Assert.Equal("/Project4/File39.cs", tree.FocusedNode.Value);
        int index = tree.Rows.ToList().FindIndex(row => row.Id.Value == "/Project4/File39.cs");
        Assert.InRange(index, tree.FirstVisibleRow, tree.FirstVisibleRow + tree.VisibleRowCapacity - 1);
    }

    [Fact]
    public void Single_Selection_Mode_Keeps_One_Node_Selected()
    {
        CountingTreeSource source = BuildSolution(projects: 3, filesPerProject: 1);
        using var tree = new StandardTreeView { DataSource = source, SelectionMode = TreeSelectionMode.Single };

        tree.SetSelection([new TreeNodeId("/Project0"), new TreeNodeId("/Project1")]);

        Assert.Equal("/Project1", Assert.Single(tree.Selection).Value);
    }
}
