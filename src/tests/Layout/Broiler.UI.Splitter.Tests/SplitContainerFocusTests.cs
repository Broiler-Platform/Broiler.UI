using System.Linq;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.Splitter.Standard;
using Broiler.UI.Standard;
using Xunit;

namespace Broiler.UI.Splitter.Tests;

/// <summary>Keyboard reachability of the splitter, visual child order, and collapsed panes.</summary>
public sealed class SplitContainerFocusTests
{
    [Fact]
    public void SplitterIsATabStopWhileEnabled()
    {
        using var scene = CreateScene();
        var splitter = scene.Container.Splitter;

        Assert.True(splitter.Focusable);
        Assert.True(splitter.IsTabStop);
        Assert.True(splitter.CanFocus);
        Assert.True(splitter.Focus());

        splitter.IsEnabled = false;
        Assert.False(splitter.CanFocus);
    }

    [Fact]
    public void ChildrenStayInVisualOrderWhateverTheAssignmentOrder()
    {
        var container = new StandardSplitContainer();
        var first = new FocusablePane("first");
        var second = new FocusablePane("second");
        container.SecondPane = second;
        container.FirstPane = first;
        Assert.Equal(new UiElement[] { first, container.Splitter, second }, container.Children);

        var replacement = new StandardSplitter();
        container.Splitter = replacement;
        Assert.Equal(new UiElement[] { first, replacement, second }, container.Children);

        var newFirst = new FocusablePane("new first");
        container.FirstPane = newFirst;
        Assert.Equal(new UiElement[] { newFirst, replacement, second }, container.Children);

        // Semantic children follow the same order, so assistive technology meets the splitter between the panes.
        var roles = container.GetSemanticNode().Children.Select(node => node.Name).ToArray();
        Assert.Equal("new first", roles[0]);
        Assert.Equal(UiSemanticRole.Splitter, container.GetSemanticNode().Children[1].Role);
        Assert.Equal("second", roles[2]);
    }

    [Fact]
    public void TabMeetsTheSplitterBetweenThePanes()
    {
        using var scene = CreateScene();
        var focus = new StandardFocusScope(scene.Session);

        Assert.True(focus.MoveFocus(1));
        Assert.Same(scene.First, scene.Session.FocusedElement);
        Assert.True(focus.MoveFocus(1));
        Assert.Same(scene.Container.Splitter, scene.Session.FocusedElement);
        Assert.True(focus.MoveFocus(1));
        Assert.Same(scene.Second, scene.Session.FocusedElement);
    }

    [Fact]
    public void CollapsedPaneAndItsSplitterAreHiddenFromAccessibilityAndFocusTraversal()
    {
        using var scene = CreateScene();
        var container = scene.Container;
        var focus = new StandardFocusScope(scene.Session);

        container.CollapseFirstPane();
        scene.Session.RenderFrame();
        Assert.True(scene.First.IsHiddenFromAccessibility);
        Assert.True(container.Splitter.IsHiddenFromAccessibility);
        Assert.False(scene.Second.IsHiddenFromAccessibility);
        Assert.Equal(new[] { "second" }, container.GetSemanticNode().Children.Select(node => node.Name));
        scene.Session.SetFocus(scene.Second);
        Assert.True(focus.MoveFocus(1));
        Assert.Same(scene.Second, scene.Session.FocusedElement);

        container.CollapseSecondPane();
        Assert.False(scene.First.IsHiddenFromAccessibility);
        Assert.True(scene.Second.IsHiddenFromAccessibility);
        Assert.True(container.Splitter.IsHiddenFromAccessibility);

        container.RestorePanes();
        Assert.False(scene.First.IsHiddenFromAccessibility);
        Assert.False(scene.Second.IsHiddenFromAccessibility);
        Assert.False(container.Splitter.IsHiddenFromAccessibility);
        Assert.Equal(3, container.GetSemanticNode().Children.Count);
    }

    [Fact]
    public void ReplacingACollapsedPaneReleasesTheOldOneAndHidesTheNewOne()
    {
        using var scene = CreateScene();
        var container = scene.Container;
        container.CollapseSecondPane();
        var old = scene.Second;
        var replacement = new FocusablePane("replacement");

        container.SecondPane = replacement;

        Assert.False(old.IsHiddenFromAccessibility);
        Assert.True(replacement.IsHiddenFromAccessibility);
    }

    private static Scene CreateScene()
    {
        UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new Host(new BSize(800, 600)));
        var first = new FocusablePane("first");
        var second = new FocusablePane("second");
        var container = new StandardSplitContainer { FirstPane = first, SecondPane = second, PreferredSize = new BSize(800, 600) };
        session.AddRoot(container);
        session.RenderFrame();
        return new Scene(session, container, first, second);
    }

    private sealed record Scene(UiSession Session, StandardSplitContainer Container, FocusablePane First, FocusablePane Second) : System.IDisposable
    {
        public void Dispose() => Session.Dispose();
    }

    private sealed class FocusablePane : UiElement
    {
        private readonly string _name;

        public FocusablePane(string name)
        {
            _name = name;
            Focusable = true;
        }

        protected override BSize MeasureCore(BSize availableSize) => new(100, 100);

        protected override UiSemanticNode GetSemanticNodeCore() =>
            new(UiSemanticRole.Group, _name, Bounds, UiSemanticState.Visible, []);
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
