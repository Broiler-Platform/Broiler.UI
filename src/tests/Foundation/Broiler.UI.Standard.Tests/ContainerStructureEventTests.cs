using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI;
using Broiler.UI.Forms.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;
using Broiler.UI.TabView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// Containers whose children assistive technology sees through virtual nodes (list items, tabs) or
/// through visibility (a disclosure) raise StructureChanged when those change. ADR 0028.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class ContainerStructureEventTests
{
    [Fact]
    public void ReplacingListItemsRaisesStructureChangedForTheList()
    {
        var list = new StandardListView();
        using UiSession session = Attach(list, out List<UiSemanticChangedEventArgs> events);

        list.SetItems([new UiListItem("1", "First"), new UiListItem("2", "Second")]);

        Assert.Contains(events, e => e.Change == UiSemanticChangeKind.StructureChanged && ReferenceEquals(e.Element, list));
    }

    [Fact]
    public void AddingRemovingMovingAndSwitchingTabsRaiseStructureChangedForTheTabView()
    {
        var tabs = new StandardTabView();
        using UiSession session = Attach(tabs, out List<UiSemanticChangedEventArgs> events);

        tabs.AddTab("one", "One");
        Assert.True(RaisedFor(events, tabs));

        events.Clear();
        tabs.AddTab("two", "Two", new StandardPanel());
        Assert.True(RaisedFor(events, tabs));

        events.Clear();
        tabs.SelectTab("two");
        Assert.True(RaisedFor(events, tabs));

        events.Clear();
        Assert.True(tabs.MoveTab("two", 0));
        Assert.True(RaisedFor(events, tabs));

        events.Clear();
        Assert.True(tabs.RemoveTab("one"));
        Assert.True(RaisedFor(events, tabs));
    }

    [Fact]
    public void ExpandingADisclosureRaisesStructureChangedWhereItsContentAppears()
    {
        var section = new FormSection("Cc and Bcc", collapsible: true, expanded: false);
        using UiSession session = Attach(section, out List<UiSemanticChangedEventArgs> events);

        section.Expand();

        Assert.True(RaisedFor(events, section.Content.Parent!));
    }

    private static bool RaisedFor(List<UiSemanticChangedEventArgs> events, UiElement element) =>
        events.Any(e => e.Change == UiSemanticChangeKind.StructureChanged && ReferenceEquals(e.Element, element));

    private static UiSession Attach(UiElement root, out List<UiSemanticChangedEventArgs> events)
    {
        UiSession session = new StandardUiSessionBuilder().Build(new Host());
        session.AddRoot(root);
        session.RenderFrame();
        var recorded = new List<UiSemanticChangedEventArgs>();
        session.SemanticChanged += (_, e) => recorded.Add(e);
        events = recorded;
        return session;
    }

    private sealed class Host : IUiHost
    {
        public BSize ViewportSize => new(640, 480);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
