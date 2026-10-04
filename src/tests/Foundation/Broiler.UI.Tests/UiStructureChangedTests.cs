using Broiler.Graphics.Geometry;

namespace Broiler.UI.Tests;

/// <summary>
/// Every change to the children assistive technology sees raises StructureChanged for the parent, so
/// a host can refresh that subtree and drop peers for what left it. ADR 0028.
/// </summary>
public sealed class UiStructureChangedTests
{
    [Fact]
    public void Inserting_Removing_And_Moving_A_Child_Raise_StructureChanged_For_The_Parent()
    {
        using UiSession session = CreateSession(out List<UiSemanticChangedEventArgs> events, out AccessibilityHost host);
        var root = new TestElement("root");
        var first = new TestElement("first");
        var second = new TestElement("second");
        session.AddRoot(root);

        root.AddChild(first);
        Assert.Equal<UiElement>([root], Structure(events));
        Assert.Contains((root, UiSemanticChangeKind.StructureChanged), host.Notifications);

        events.Clear();
        root.InsertChild(0, second);
        Assert.Equal<UiElement>([root], Structure(events));

        events.Clear();
        Assert.True(root.MoveChild(second, 1));
        Assert.Equal<UiElement>([root], Structure(events));

        events.Clear();
        Assert.False(root.MoveChild(second, 1));
        Assert.Empty(Structure(events));

        events.Clear();
        Assert.True(root.RemoveChild(first));
        Assert.Equal<UiElement>([root], Structure(events));
    }

    [Fact]
    public void A_Visibility_Change_Raises_StructureChanged_For_The_Parent_Or_The_Root_Itself()
    {
        using UiSession session = CreateSession(out List<UiSemanticChangedEventArgs> events, out _);
        var root = new TestElement("root");
        var child = new TestElement("child");
        root.AddChild(child);
        session.AddRoot(root);
        events.Clear();

        child.Visibility = UiVisibility.Collapsed;
        Assert.Equal<UiElement>([root], Structure(events));

        events.Clear();
        child.Visibility = UiVisibility.Collapsed;
        Assert.Empty(Structure(events));

        events.Clear();
        root.Visibility = UiVisibility.Hidden;
        Assert.Equal<UiElement>([root], Structure(events));
    }

    [Fact]
    public void Hiding_From_Accessibility_Raises_StructureChanged_For_The_Parent()
    {
        using UiSession session = CreateSession(out List<UiSemanticChangedEventArgs> events, out _);
        var container = new HidingContainer();
        var content = new TestElement("content");
        container.AddChild(content);
        session.AddRoot(container);
        events.Clear();

        container.Hide(content, true);
        Assert.Equal<UiElement>([container], Structure(events));

        events.Clear();
        container.Hide(content, true);
        Assert.Empty(Structure(events));

        events.Clear();
        container.Hide(content, false);
        Assert.Equal<UiElement>([container], Structure(events));
    }

    [Fact]
    public void State_Only_Changes_Do_Not_Raise_StructureChanged()
    {
        using UiSession session = CreateSession(out List<UiSemanticChangedEventArgs> events, out _);
        var root = new TestElement("root");
        var child = new TestElement("child");
        root.AddChild(child);
        session.AddRoot(root);
        events.Clear();

        child.AccessibleName = "Renamed";
        child.IsRequired = true;
        child.InvalidateRender();

        Assert.NotEmpty(events);
        Assert.Empty(Structure(events));
    }

    [Fact]
    public void Disposing_A_Subtree_Raises_One_Event_For_Its_Parent()
    {
        using UiSession session = CreateSession(out List<UiSemanticChangedEventArgs> events, out _);
        var root = new TestElement("root");
        var panel = new TestElement("panel");
        panel.AddChild(new TestElement("a"));
        panel.AddChild(new TestElement("b"));
        root.AddChild(panel);
        session.AddRoot(root);
        events.Clear();

        panel.Dispose();

        Assert.Equal<UiElement>([root], Structure(events));
    }

    [Fact]
    public void Containers_With_Virtual_Children_Raise_It_Themselves_And_Detached_Trees_Raise_Nothing()
    {
        using UiSession session = CreateSession(out List<UiSemanticChangedEventArgs> events, out _);
        var container = new HidingContainer();
        session.AddRoot(container);
        events.Clear();

        container.ReplaceVirtualChildren();
        Assert.Equal<UiElement>([container], Structure(events));

        // Without a session there is nobody to tell, and nothing throws.
        var detached = new HidingContainer();
        detached.AddChild(new TestElement("child"));
        detached.ReplaceVirtualChildren();
        detached.Children[0].Visibility = UiVisibility.Collapsed;
    }

    private static UiElement[] Structure(List<UiSemanticChangedEventArgs> events) =>
        [.. events.Where(e => e.Change == UiSemanticChangeKind.StructureChanged).Select(e => e.Element)];

    private static UiSession CreateSession(out List<UiSemanticChangedEventArgs> events, out AccessibilityHost host)
    {
        host = new AccessibilityHost();
        var session = new UiSession(host, new InlineUiDispatcher(), new ManualUiClock());
        var recorded = new List<UiSemanticChangedEventArgs>();
        session.SemanticChanged += (_, e) => recorded.Add(e);
        events = recorded;
        return session;
    }

    private sealed class HidingContainer : UiElement
    {
        public void Hide(UiElement child, bool hidden) => SetHiddenFromAccessibility(child, hidden);

        public void ReplaceVirtualChildren() => NotifyStructureChanged();
    }

    private sealed class AccessibilityHost : IUiHost, IUiAccessibilityHost
    {
        public BSize ViewportSize => new(100, 50);
        public double Scale => 1;
        public List<(UiElement, UiSemanticChangeKind)> Notifications { get; } = [];
        public Broiler.Graphics.RenderList.BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(Broiler.Graphics.RenderList.BRenderList renderList) { }
        public void PublishSemanticSnapshot(IReadOnlyList<UiSemanticNode> roots) { }
        public void NotifySemanticChanged(UiElement element, UiSemanticChangeKind change) => Notifications.Add((element, change));
    }
}
