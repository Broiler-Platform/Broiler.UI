using Broiler.Graphics;

namespace Broiler.UI.TreeView.Tests;

/// <summary>
/// What the mouse does to a tree.
///
/// Selection had been the only thing a click could do: NodeActivated was raised
/// from the Enter key and nowhere else, so clicking a file in a solution
/// explorer selected it and the host, which opens documents on activation, was
/// never told. These pin the pointer's route to activation.
/// </summary>
public sealed class TreeViewMouseTests
{
    private static CountingTreeSource BuildTree()
    {
        var source = new CountingTreeSource();
        source.Add("/", "/Project0", "/Readme.md");
        source.Add("/Project0", "/Project0/File0.cs", "/Project0/File1.cs");
        return source;
    }

    [Fact(Timeout = 600000)]
    public void One_Click_Selects_And_Does_Not_Activate()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());

        scene.Click(scene.RowPoint(1));

        Assert.Equal("/Readme.md", Assert.Single(scene.Tree.Selection).Value);
        Assert.Empty(scene.Activated);
    }

    [Fact(Timeout = 600000)]
    public void A_Double_Click_Activates_The_Row_It_Landed_On()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());

        scene.Click(scene.RowPoint(1));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(120));
        scene.Click(scene.RowPoint(1));

        Assert.Equal("/Readme.md", Assert.Single(scene.Activated));
    }

    [Fact(Timeout = 600000)]
    public void A_Second_Click_After_The_Window_Is_Two_Single_Clicks()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());

        scene.Click(scene.RowPoint(1));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(900));
        scene.Click(scene.RowPoint(1));

        Assert.Empty(scene.Activated);
    }

    [Fact(Timeout = 600000)]
    public void A_Quick_Click_On_The_Next_Row_Is_Not_A_Double_Click()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());

        // Two presses inside the window, one row apart. A tree is clicked by
        // row, so this is two selections and not an activation of either.
        scene.Click(scene.RowPoint(0));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(50));
        scene.Click(scene.RowPoint(1));

        Assert.Empty(scene.Activated);
        Assert.Equal("/Readme.md", Assert.Single(scene.Tree.Selection).Value);
    }

    [Fact(Timeout = 600000)]
    public void A_Third_Click_Does_Not_Activate_Again()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());

        // The second click consumes the first. A triple click is one
        // activation, not two — a double click plus a fresh single one.
        scene.Click(scene.RowPoint(1));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(80));
        scene.Click(scene.RowPoint(1));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(80));
        scene.Click(scene.RowPoint(1));

        Assert.Equal("/Readme.md", Assert.Single(scene.Activated));
    }

    [Fact(Timeout = 600000)]
    public void A_Double_Click_On_A_Parent_Row_Expands_It()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());
        var project = new TreeNodeId("/Project0");

        scene.Click(scene.RowPoint(0));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(120));
        scene.Click(scene.RowPoint(0));

        Assert.True(scene.Tree.IsExpanded(project));
        Assert.Equal("/Project0", Assert.Single(scene.Activated));
        Assert.Equal(4, scene.Tree.Rows.Count);
    }

    [Fact(Timeout = 600000)]
    public void Clicking_The_Expander_Toggles_Without_Selecting_Or_Activating()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());
        BPoint expander = scene.ExpanderPoint(0, depth: 0);

        scene.Click(expander);
        Assert.True(scene.Tree.IsExpanded(new TreeNodeId("/Project0")));
        Assert.Empty(scene.Tree.Selection);

        // Twice in a row is a collapse, not an activation: the expander is its
        // own control and never arms a double click.
        scene.Clock.Advance(TimeSpan.FromMilliseconds(60));
        scene.Click(expander);
        Assert.False(scene.Tree.IsExpanded(new TreeNodeId("/Project0")));
        Assert.Empty(scene.Activated);
    }

    [Fact(Timeout = 600000)]
    public void Enter_Still_Activates_The_Focused_Row()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());

        scene.Click(scene.RowPoint(1));
        scene.Route.Dispatch(TreeStandardHarness.Key("Enter"));

        Assert.Equal("/Readme.md", Assert.Single(scene.Activated));
    }

    [Fact(Timeout = 600000)]
    public void A_Click_Below_The_Last_Row_Activates_Nothing()
    {
        using TreeScene scene = TreeStandardHarness.Create(BuildTree());

        // Two rows in a 400pt tree leaves a lot of empty space, and a double
        // click in it must not reach the row that happens to be selected.
        scene.Click(scene.RowPoint(1));
        scene.Clock.Advance(TimeSpan.FromMilliseconds(60));
        BPoint empty = scene.RowPoint(10);
        scene.Click(empty);
        scene.Clock.Advance(TimeSpan.FromMilliseconds(60));
        scene.Click(empty);

        Assert.Empty(scene.Activated);
    }
}
