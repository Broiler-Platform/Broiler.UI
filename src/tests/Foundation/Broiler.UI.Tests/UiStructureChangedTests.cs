using Broiler.Graphics.Geometry;
using Broiler.Input;
using Broiler.Input.Mouse;

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

    [Fact]
    public void Changes_Made_While_Input_Is_Dispatched_Arrive_Once_Per_Element_Afterwards()
    {
        using UiSession session = CreateSession(out List<UiSemanticChangedEventArgs> events, out AccessibilityHost host);
        var panel = new TestElement("panel");
        var other = new TestElement("other");
        var gone = new TestElement("gone");
        var root = new InputAction();
        root.AddChild(panel);
        root.AddChild(other);
        root.AddChild(gone);
        session.AddRoot(root);
        session.RenderFrame();
        events.Clear();
        host.Notifications.Clear();

        // A click that builds a form: fifty fields, the first collapsed again, a note elsewhere, and
        // an element that is changed and then removed.
        int raisedDuringTheHandler = -1;
        root.Action = () =>
        {
            for (int index = 0; index < 50; index++)
                panel.AddChild(new TestElement($"field {index}"));
            panel.Children[0].Visibility = UiVisibility.Collapsed;
            other.AddChild(new TestElement("note"));
            gone.AddChild(new TestElement("child"));
            Assert.True(root.RemoveChild(gone));
            raisedDuringTheHandler = Structure(events).Length;
        };
        Assert.True(session.DispatchInput(MouseDown(10, 10)));

        Assert.Equal(0, raisedDuringTheHandler);
        // In the order they first changed; the removed element is reported by the parent it left.
        Assert.Equal<UiElement>([panel, other, root], Structure(events));
        Assert.Equal(3, host.Notifications.Count(notification => notification.Item2 == UiSemanticChangeKind.StructureChanged));

        // Outside input and frames, a change is reported as it happens.
        events.Clear();
        panel.AddChild(new TestElement("later"));
        Assert.Equal<UiElement>([panel], Structure(events));
    }

    [Fact]
    public void Changes_Made_During_Layout_Are_Reported_After_The_Frame()
    {
        using UiSession session = CreateSession(out List<UiSemanticChangedEventArgs> events, out AccessibilityHost host);
        var form = new LayoutToggle();
        session.AddRoot(form);
        session.RenderFrame();
        var presentedWhenRaised = new List<int>();
        session.SemanticChanged += (_, e) =>
        {
            if (e.Change == UiSemanticChangeKind.StructureChanged)
                presentedWhenRaised.Add(host.Presented);
        };
        events.Clear();

        form.ShowFeedback = true;
        int presented = host.Presented;
        session.RenderFrame();

        Assert.Equal([presented + 1], presentedWhenRaised);
        Assert.Equal<UiElement>([form], Structure(events));
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

    private static UiInputEvent MouseDown(double x, double y) =>
        UiInputEvent.FromMouseButton(new MouseButtonEvent(
            new InputEventHeader(
                InputDeviceId.FromOpaqueValue("mouse:structure"),
                new InputTimestamp(1, TimeSpan.TicksPerSecond, "structure-test"),
                1),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.Left,
            MouseButton.Left,
            MouseButtonTransition.Down,
            InputEventSource.Synthetic));

    /// <summary>Runs an action for any pointer button that reaches it.</summary>
    private sealed class InputAction : UiElement
    {
        public Action? Action { get; set; }

        protected override bool OnInput(UiInputEvent input)
        {
            if (input.Kind != UiInputEventKind.PointerButton || Action is null)
                return false;

            Action();
            return true;
        }
    }

    /// <summary>Decides while measuring whether its feedback shows, as a form surface does.</summary>
    private sealed class LayoutToggle : UiElement
    {
        private readonly TestElement _feedback = new("feedback") { Visibility = UiVisibility.Collapsed };
        private bool _showFeedback;

        public LayoutToggle() => AddChild(_feedback);

        public bool ShowFeedback
        {
            get => _showFeedback;
            set
            {
                _showFeedback = value;
                InvalidateMeasure();
            }
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _feedback.Visibility = _showFeedback ? UiVisibility.Visible : UiVisibility.Collapsed;
            return base.MeasureCore(availableSize);
        }
    }

    private sealed class AccessibilityHost : IUiHost, IUiAccessibilityHost
    {
        public BSize ViewportSize => new(100, 50);
        public double Scale => 1;
        public List<(UiElement, UiSemanticChangeKind)> Notifications { get; } = [];
        public int Presented { get; private set; }
        public Broiler.Graphics.RenderList.BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(Broiler.Graphics.RenderList.BRenderList renderList) => Presented++;
        public void PublishSemanticSnapshot(IReadOnlyList<UiSemanticNode> roots) { }
        public void NotifySemanticChanged(UiElement element, UiSemanticChangeKind change) => Notifications.Add((element, change));
    }
}
