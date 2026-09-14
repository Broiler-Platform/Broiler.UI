using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.Input.Touch;

namespace Broiler.UI.Tests;

public sealed class UiTreeLifecycleTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Removing_Subtree_Clears_Its_Input_State_And_Caret(bool isRoot, bool dispose)
    {
        var host = new CaretHost();
        using var session = new UiSession(host, new InlineUiDispatcher(), new ManualUiClock());
        var outside = new InputElement();
        var branch = new InputElement();
        var descendant = new InputElement();
        session.AddRoot(outside);
        if (isRoot) session.AddRoot(branch); else outside.AddChild(branch);
        branch.AddChild(descendant);
        session.RenderFrame();
        session.PushModalElement(outside);
        session.PushModalElement(descendant);
        session.SetFocus(descendant);
        session.CaptureInput(descendant);

        if (dispose) branch.Dispose();
        else if (isRoot) session.RemoveRoot(branch);
        else outside.RemoveChild(branch);

        Assert.Null(session.FocusedElement);
        Assert.Null(session.CapturedElement);
        Assert.Equal(new[] { outside }, session.ModalElements);
        Assert.Equal(new[] { descendant }, host.ClearedCarets);
        Assert.Null(descendant.Session);
        int count = descendant.InputCount;
        session.DispatchInput(MouseDown());
        Assert.Equal(count, descendant.InputCount);
        Assert.True(outside.InputCount > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detached_Touch_Contact_Does_Not_Retarget_Or_Follow_Reattached_Element(bool isRoot)
    {
        using var session = CreateSession();
        using var otherSession = CreateSession();
        var outside = new InputElement();
        var target = new InputElement();
        session.AddRoot(outside);
        if (isRoot) session.AddRoot(target); else outside.AddChild(target);
        session.RenderFrame();
        session.CaptureInput(target);
        session.DispatchInput(Touch(1, TouchContactState.Pressed));
        if (isRoot) session.RemoveRoot(target); else outside.RemoveChild(target);
        otherSession.AddRoot(target);
        int targetCount = target.InputCount;
        int outsideCount = outside.InputCount;

        session.DispatchInput(Touch(1, TouchContactState.Moved));
        session.DispatchInput(Touch(1, TouchContactState.Released));
        Assert.Equal(targetCount, target.InputCount);
        Assert.Equal(outsideCount, outside.InputCount);
        session.DispatchInput(Touch(1, TouchContactState.Pressed));
        Assert.True(outside.InputCount > outsideCount);
    }

    [Theory]
    [InlineData(UiInputEventKind.TouchContact)]
    [InlineData(UiInputEventKind.PointerButton)]
    public void Disposal_During_Touch_Press_Does_Not_Restore_The_Cancelled_Route(UiInputEventKind disposeOn)
    {
        using var session = CreateSession();
        var outside = new InputElement();
        var target = new InputElement();
        session.AddRoot(outside);
        outside.AddChild(target);
        session.RenderFrame();
        target.Input = input => { if (input.Kind == disposeOn) target.Dispose(); };

        session.DispatchInput(Touch(1, TouchContactState.Pressed));
        int count = target.InputCount;
        int outsideCount = outside.InputCount;
        session.DispatchInput(Touch(1, TouchContactState.Moved));
        session.DispatchInput(Touch(1, TouchContactState.Released));
        Assert.True(target.IsDisposed);
        Assert.Equal(count, target.InputCount);
        Assert.Equal(outsideCount, outside.InputCount);
    }

    [Fact]
    public void Detachment_Preserves_Other_Contacts_And_Session_State()
    {
        using var session = CreateSession();
        var first = new InputElement();
        var second = new InputElement();
        session.AddRoot(first);
        session.AddRoot(second);
        session.CaptureInput(first);
        session.DispatchInput(Touch(1, TouchContactState.Pressed));
        session.CaptureInput(second);
        session.SetFocus(second);
        session.PushModalElement(second);
        session.DispatchInput(Touch(2, TouchContactState.Pressed));
        first.Dispose();
        int count = second.InputCount;

        session.DispatchInput(Touch(1, TouchContactState.Released));
        Assert.Equal(count, second.InputCount);
        session.DispatchInput(Touch(2, TouchContactState.Moved));
        Assert.True(second.InputCount > count);
        Assert.Same(second, session.CapturedElement);
        Assert.Same(second, session.FocusedElement);
        Assert.Same(second, session.ModalElement);
    }

    [Fact]
    public void Attached_Root_Must_Be_Removed_Before_Inserting_As_Child()
    {
        using var session = CreateSession();
        var first = new TestElement("first");
        var second = new TestElement("second");
        session.AddRoot(first);
        session.AddRoot(second);

        Assert.Throws<InvalidOperationException>(() => first.AddChild(second));
        Assert.Null(second.Parent);
        Assert.Empty(first.Children);
        Assert.Equal(new[] { first, second }, session.Roots);
        session.RenderFrame();
        Assert.Equal(1, second.RenderCount);

        Assert.True(session.RemoveRoot(second));
        first.AddChild(second);
        Assert.Single(session.Roots);
        Assert.Same(first, second.Parent);
        Assert.Same(session, second.Session);
        session.RenderFrame();
        Assert.Equal(2, second.RenderCount);
    }

    [Fact]
    public void Disposed_Element_Cannot_Be_Added_As_Root()
    {
        using var session = CreateSession();
        var root = new TestElement("disposed");
        root.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.AddRoot(root));
        Assert.Empty(session.Roots);
        Assert.Null(root.Session);
    }

    private static UiSession CreateSession() =>
        new(new RecordingUiHost(new BSize(100, 100)), new InlineUiDispatcher(), new ManualUiClock());

    private static InputEventHeader Header =>
        new(InputDeviceId.FromOpaqueValue("lifecycle"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"), 1);

    private static UiInputEvent Touch(long id, TouchContactState state) =>
        UiInputEvent.FromTouchContact(new TouchContactEvent(Header, id,
            InputPoint.ClientDeviceIndependentPixels(1, 1), state, 1, InputEventSource.Synthetic));

    private static UiInputEvent MouseDown() =>
        UiInputEvent.FromMouseButton(new MouseButtonEvent(Header, InputPoint.ClientDeviceIndependentPixels(1, 1),
            MouseButtons.Left, MouseButton.Left, MouseButtonTransition.Down, InputEventSource.Synthetic));

    private sealed class InputElement : UiElement
    {
        public Action<UiInputEvent>? Input { get; set; }
        public int InputCount { get; private set; }
        protected override bool OnInput(UiInputEvent input)
        {
            InputCount++;
            Input?.Invoke(input);
            return false;
        }
    }

    private sealed class CaretHost : IUiHost, IUiTextInputHost
    {
        public List<UiElement> ClearedCarets { get; } = [];
        public BSize ViewportSize => new(100, 100);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Present(BRenderList renderList) { }
        public void Invalidate(UiInvalidation invalidation) { }
        public void PublishCaret(UiTextCaretInfo caret) { }
        public void ClearCaret(UiElement owner) => ClearedCarets.Add(owner);
    }
}
