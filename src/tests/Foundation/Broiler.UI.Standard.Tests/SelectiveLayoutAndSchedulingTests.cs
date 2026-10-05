using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.ListView.Standard;
using Broiler.UI.SpinBox.Standard;
using Xunit;

namespace Broiler.UI.Standard.Tests;

public sealed class SelectiveLayoutAndSchedulingTests
{
    [Fact]
    public void Hover_On_Unchanged_Size_Does_Not_Rewrap_Large_Document()
    {
        var host = new TestHost(new BSize(800, 600));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());

        var container = new TestContainer();
        string documentText = string.Join("\n\n", Enumerable.Range(1, 10).Select(i =>
            $"Paragraph {i}: This is a long sample text inside paragraph {i} designed to test paragraph wrapping across multiple lines in Broiler.UI standard label implementation. It should wrap nicely."));
        var label = new StandardLabel(documentText)
        {
            Wrapping = UiTextWrapping.Wrap,
        };
        var button = new StandardButton
        {
            Text = "Hover Me",
            PreferredSize = new BSize(120, 32),
        };

        container.AddChild(label);
        container.AddChild(button);
        session.AddRoot(container);

        // Frame 1: Initial measure and render
        session.RenderFrame();
        int initialBuildCount = label.LayoutBuildCount;
        Assert.True(initialBuildCount > 0, "Document should have been wrapped and built on the first frame.");
        Assert.True(label.IsMeasureValid, "Label should be measure-valid after initial frame.");
        Assert.True(label.IsArrangeValid, "Label should be arrange-valid after initial frame.");

        // Mouse hover on button
        var pointerMove = PointerMove(button.Bounds.Left + 5, button.Bounds.Top + 5);
        session.DispatchInput(pointerMove);

        Assert.True(session.HasPendingInvalidations, "Hover should have queued a Render invalidation for the button.");

        // Frame 2: Render frame servicing hover
        session.RenderFrame();

        // The document must NOT have been re-wrapped or re-measured!
        Assert.Equal(initialBuildCount, label.LayoutBuildCount);
        Assert.False(session.HasPendingInvalidations);
    }

    [Fact]
    public void Layout_Affecting_Changes_Do_Invalidate_And_Rewrap()
    {
        var host = new TestHost(new BSize(800, 600));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());

        var container = new TestContainer();
        var label = new StandardLabel("First text") { Wrapping = UiTextWrapping.Wrap };
        var button = new StandardButton { Text = "Short" };
        container.AddChild(label);
        container.AddChild(button);
        session.AddRoot(container);

        session.RenderFrame();
        int buildCount = label.LayoutBuildCount;
        BSize initialButtonSize = button.DesiredSize;

        // 1. Text change on label
        label.Text = "Updated text with additional words to measure";
        Assert.False(label.IsMeasureValid, "Changing Text must invalidate measure.");
        Assert.False(container.IsMeasureValid, "Changing child Text must invalidate container measure.");
        session.RenderFrame();
        Assert.True(label.LayoutBuildCount > buildCount, "Label must have rewrapped on Text change.");
        buildCount = label.LayoutBuildCount;

        // 2. Font change on label
        label.Font = new BFontStyle("Segoe UI", 18, BFontWeight.Bold);
        Assert.False(label.IsMeasureValid, "Changing Font must invalidate measure.");
        session.RenderFrame();
        Assert.True(label.LayoutBuildCount > buildCount, "Label must have rewrapped on Font change.");
        buildCount = label.LayoutBuildCount;

        // 3. Viewport resize
        host.ViewportSize = new BSize(400, 600);
        session.RenderFrame();
        Assert.True(label.LayoutBuildCount > buildCount, "Label must have rewrapped on available width change.");
        buildCount = label.LayoutBuildCount;

        // 4. Button text change
        button.Text = "A much longer button caption that needs significantly more layout space";
        Assert.False(button.IsMeasureValid, "Button must invalidate measure when caption changes.");
        session.RenderFrame();
        Assert.True(button.DesiredSize.Width > initialButtonSize.Width, "Button desired size must widen.");
    }

    [Fact]
    public void Audit_Property_Setters_Invalidate_Layout_Correctly()
    {
        var host = new TestHost(new BSize(800, 600));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());

        var container = new TestContainer();
        var button = new StandardButton { Text = "Button" };
        var listView = new StandardListView();
        var comboBox = new StandardComboBox();
        comboBox.SetItems([new UiComboBoxItem("1", "Item 1")]);
        var spinBox = new StandardSpinBox { Minimum = 0, Maximum = 100, Value = 10 };

        container.AddChild(button);
        container.AddChild(listView);
        container.AddChild(comboBox);
        container.AddChild(spinBox);
        session.AddRoot(container);

        session.RenderFrame();
        Assert.True(button.IsMeasureValid);
        Assert.True(listView.IsArrangeValid);
        Assert.True(comboBox.IsArrangeValid);
        Assert.True(spinBox.IsMeasureValid);

        // StandardButton.PaddingX
        button.PaddingX = 40;
        Assert.False(button.IsMeasureValid, "Changing button PaddingX must invalidate measure.");
        session.RenderFrame();

        // StandardListView.ItemHeight
        listView.ItemHeight = 44;
        Assert.False(listView.IsArrangeValid, "Changing listView ItemHeight must invalidate arrange.");
        session.RenderFrame();

        // StandardComboBox.ItemHeight
        comboBox.ItemHeight = 44;
        Assert.False(comboBox.IsArrangeValid, "Changing comboBox ItemHeight must invalidate arrange.");
        session.RenderFrame();

        // StandardSpinBox.ArrowWidth
        spinBox.ArrowWidth = 32;
        Assert.False(spinBox.IsMeasureValid, "Changing spinBox ArrowWidth must invalidate measure.");
        session.RenderFrame();
    }

    [Fact]
    public void Preserves_Invalidations_Raised_During_Rendering_And_Deferred()
    {
        var host = new TestHost(new BSize(400, 300));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());

        var renderInvalidator = new InvalidationDuringRenderElement();
        session.AddRoot(renderInvalidator);

        // Initial render: InvalidationDuringRenderElement will call Invalidate during RenderCore
        session.RenderFrame();

        // The invalidation raised during rendering must be preserved in HasPendingInvalidations!
        Assert.True(session.HasPendingInvalidations, "Invalidation raised during RenderCore must be preserved.");
        Assert.False(renderInvalidator.IsMeasureValid, "Measure should be marked invalid by render-time invalidation.");

        // Next frame cleanly services it:
        session.RenderFrame();
        Assert.False(session.HasPendingInvalidations);
        Assert.True(renderInvalidator.IsMeasureValid);
    }

    [Fact]
    public void Animation_Scheduler_Host_Wake_And_Idle_Lifecycle()
    {
        var host = new AnimationTestHost(new BSize(400, 300));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());
        using var scheduler = new StandardAnimationScheduler(session);

        Assert.False(scheduler.IsRunning);
        Assert.False(host.IsAnimationActive);

        // Register 1st animation
        int tick1Count = 0;
        IDisposable reg1 = scheduler.Register(TimeSpan.FromMilliseconds(16), _ => tick1Count++);
        Assert.True(scheduler.IsRunning);
        Assert.True(host.IsAnimationActive, "Host should be notified to start animation timer.");

        // Register 2nd animation
        int tick2Count = 0;
        IDisposable reg2 = scheduler.Register(TimeSpan.FromMilliseconds(32), _ => tick2Count++);
        Assert.True(scheduler.IsRunning);
        Assert.True(host.IsAnimationActive);

        // Dispose 1st: still 1 animation running
        reg1.Dispose();
        Assert.True(scheduler.IsRunning);
        Assert.True(host.IsAnimationActive);

        // Dispose 2nd: all animations stopped -> idle!
        reg2.Dispose();
        Assert.False(scheduler.IsRunning);
        Assert.False(host.IsAnimationActive, "Host should be notified to stop animation timer when idle.");
    }

    [Fact]
    public void Animation_Scheduler_Transitions_And_Cancellation()
    {
        var host = new AnimationTestHost(new BSize(400, 300));
        var clock = new ManualClock();
        using var session = new UiSession(host, new ImmediateUiDispatcher(), clock);
        using var scheduler = new StandardAnimationScheduler(session);

        // Test normal transition completion
        double lastProgress = 0;
        bool completed = false;
        IDisposable transition = scheduler.StartTransition(
            TimeSpan.FromMilliseconds(100),
            p => lastProgress = p,
            () => completed = true);

        Assert.True(scheduler.IsRunning);
        Assert.True(host.IsAnimationActive);

        // Advance by 50ms
        clock.Advance(TimeSpan.FromMilliseconds(50));
        scheduler.Tick();
        Assert.InRange(lastProgress, 0.49, 0.51);
        Assert.False(completed);
        Assert.True(scheduler.IsRunning);

        // Advance by remaining 50ms
        clock.Advance(TimeSpan.FromMilliseconds(50));
        scheduler.Tick();
        Assert.Equal(1.0, lastProgress);
        Assert.True(completed);
        Assert.False(scheduler.IsRunning, "Scheduler should automatically transition to idle after completion.");
        Assert.False(host.IsAnimationActive);

        // Test cancellation before completion
        bool canceledCompleted = false;
        IDisposable token = scheduler.StartTransition(
            TimeSpan.FromMilliseconds(100),
            _ => { },
            () => canceledCompleted = true);

        Assert.True(scheduler.IsRunning);
        clock.Advance(TimeSpan.FromMilliseconds(30));
        scheduler.Tick();

        // Cancel
        token.Dispose();
        Assert.False(scheduler.IsRunning);
        Assert.False(host.IsAnimationActive);
        Assert.False(canceledCompleted, "Completed callback must not run if transition was cancelled.");
    }

    [Fact]
    public void Reduced_Motion_Completes_Transitions_Immediately_Without_Timer()
    {
        var host = new AnimationTestHost(new BSize(400, 300));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());
        using var scheduler = new StandardAnimationScheduler(session)
        {
            ReducedMotion = true,
        };

        double progress = 0;
        bool completed = false;
        using IDisposable token = scheduler.StartTransition(
            TimeSpan.FromMilliseconds(500),
            p => progress = p,
            () => completed = true);

        Assert.Equal(1.0, progress);
        Assert.True(completed);
        Assert.False(scheduler.IsRunning, "No animations should be scheduled under reduced motion.");
        Assert.False(host.IsAnimationActive, "Host animation timer must not start under reduced motion.");
    }

    [Fact]
    public void Queue_Ordering_Stays_Deterministic()
    {
        var list = new List<int>();
        var dispatcher = new StandardQueuedUiDispatcher();

        for (int i = 0; i < 50; i++)
        {
            int val = i;
            dispatcher.Post(() => list.Add(val));
        }

        Assert.True(dispatcher.HasPendingWork);
        dispatcher.Drain();
        Assert.False(dispatcher.HasPendingWork);

        Assert.Equal(50, list.Count);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(i, list[i]);
        }
    }

    [Fact]
    public void Invalidation_Benchmark_FastPath_Performance()
    {
        var host = new TestHost(new BSize(800, 600));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());

        var container = new TestContainer();
        var measuredElements = new List<CountingMeasureElement>();
        for (int i = 0; i < 100; i++)
        {
            var el = new CountingMeasureElement();
            measuredElements.Add(el);
            container.AddChild(el);
        }
        session.AddRoot(container);

        // Frame 1: initial layout
        session.RenderFrame();
        foreach (var el in measuredElements)
        {
            Assert.Equal(1, el.MeasureCount);
        }

        // Frames 2..100: Only Render invalidation occurs (e.g. mouse hover or frame repaint)
        for (int frame = 0; frame < 100; frame++)
        {
            container.Invalidate(UiInvalidationKind.Render);
            session.RenderFrame();
        }

        // In all 100 frames, NOT A SINGLE MeasureCore should have run!
        foreach (var el in measuredElements)
        {
            Assert.Equal(1, el.MeasureCount);
        }
    }

    [Fact]
    public void A_Change_Below_An_Element_Left_Invalid_During_Its_Parents_Measure_Still_Relayouts()
    {
        var host = new TestHost(new BSize(800, 600));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());

        // The middle element invalidates its child while measuring, as a layout that changes a
        // child's visibility mid-measure does. The child is left invalid under a valid parent.
        var leaf = new SizedElement { Size = new BSize(100, 20) };
        var child = new TestContainer();
        child.AddChild(leaf);
        var middle = new InvalidatesChildWhileMeasuring(child);
        var root = new TestContainer();
        root.AddChild(middle);
        session.AddRoot(root);
        session.RenderFrame();
        Assert.True(middle.IsMeasureValid);
        Assert.False(child.IsMeasureValid);

        // Later the leaf grows. Stopping the walk at the already-invalid child left the root valid,
        // so the frame kept the old layout until something else re-measured it (a resize).
        leaf.Size = new BSize(100, 60);
        session.RenderFrame();
        Assert.Equal(60, root.DesiredSize.Height);
        Assert.Equal(60, leaf.Bounds.Height);
    }

    [Fact]
    public void An_Offset_Change_Below_An_Element_Left_Arrange_Invalid_Under_A_Valid_Parent_Still_Moves_The_Content()
    {
        var host = new TestHost(new BSize(800, 600));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());

        // The middle element invalidates its child's arrange while arranging, so the child is left
        // arrange-invalid under an arrange-valid parent, as Broiler.Mail's compact inbox left its
        // content under the tab view.
        var leaf = new SizedElement { Size = new BSize(100, 20) };
        var scroller = new OffsetElement(leaf);
        var child = new TestContainer();
        child.AddChild(scroller);
        var middle = new InvalidatesChildArrangeWhileArranging(child);
        var root = new TestContainer();
        root.AddChild(middle);
        session.AddRoot(root);
        session.RenderFrame();
        Assert.True(middle.IsArrangeValid);
        Assert.False(child.IsArrangeValid);
        double top = leaf.Bounds.Top;

        // Stopping the arrange walk at the already-invalid child left the root valid: the offset
        // changed, and the content stayed where it was until a resize.
        scroller.Offset = 30;
        session.RenderFrame();
        Assert.Equal(top - 30, leaf.Bounds.Top);
    }

    [Fact]
    public void An_Element_Whose_Desired_Size_Changes_Is_Arranged_Again_Under_A_Parent_That_Keeps_Its_Size()
    {
        var host = new TestHost(new BSize(800, 600));
        using var session = new UiSession(host, new ImmediateUiDispatcher(), new ManualClock());

        // Like a layout that chooses its mode while arranging: the first arrange changes what it
        // will measure to, so the next frame measures it taller. Its parent fills the window, so the
        // parent's size, and with it the parent's arrange, did not change.
        var adaptive = new GrowsAfterFirstArrange(new BSize(100, 20), new BSize(100, 60));
        var root = new TopAlignedFill();
        root.AddChild(adaptive);
        session.AddRoot(root);
        session.RenderFrame();
        Assert.Equal(20, adaptive.Bounds.Height);

        session.RenderFrame();
        Assert.Equal(60, adaptive.DesiredSize.Height);
        Assert.Equal(60, adaptive.Bounds.Height);
        Assert.True(adaptive.IsArrangeValid);
    }

    private sealed class OffsetElement : UiElement
    {
        private double _offset;

        public OffsetElement(UiElement content) => AddChild(content);

        public double Offset
        {
            get => _offset;
            set { _offset = value; InvalidateArrange(); }
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            foreach (UiElement child in Children)
                child.Measure(availableSize);
            return new BSize(availableSize.Width, 100);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            foreach (UiElement child in Children)
                child.Arrange(new BRect(finalRect.Left, finalRect.Top - _offset, finalRect.Width, child.DesiredSize.Height));
        }
    }

    private sealed class InvalidatesChildArrangeWhileArranging : UiElement
    {
        private readonly UiElement _child;

        public InvalidatesChildArrangeWhileArranging(UiElement child)
        {
            _child = child;
            AddChild(child);
        }

        protected override BSize MeasureCore(BSize availableSize) => _child.Measure(availableSize);

        protected override void ArrangeCore(BRect finalRect)
        {
            _child.Arrange(finalRect);
            _child.InvalidateArrange();
        }
    }

    private sealed class GrowsAfterFirstArrange(BSize before, BSize after) : UiElement
    {
        private bool _arranged;

        protected override BSize MeasureCore(BSize availableSize) => _arranged ? after : before;

        protected override void ArrangeCore(BRect finalRect)
        {
            if (_arranged)
                return;
            _arranged = true;
            InvalidateMeasure();
        }
    }

    /// <summary>Fills what it is given and stacks its children from the top at their desired heights.</summary>
    private sealed class TopAlignedFill : UiElement
    {
        protected override BSize MeasureCore(BSize availableSize)
        {
            foreach (UiElement child in Children)
                child.Measure(availableSize);
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            double y = finalRect.Top;
            foreach (UiElement child in Children)
            {
                child.Arrange(new BRect(finalRect.Left, y, finalRect.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
            }
        }
    }

    private sealed class SizedElement : UiElement
    {
        private BSize _size;

        public BSize Size
        {
            get => _size;
            set { _size = value; InvalidateMeasure(); }
        }

        protected override BSize MeasureCore(BSize availableSize) => _size;
    }

    private sealed class InvalidatesChildWhileMeasuring : UiElement
    {
        private readonly UiElement _child;

        public InvalidatesChildWhileMeasuring(UiElement child)
        {
            _child = child;
            AddChild(child);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            BSize size = _child.Measure(availableSize);
            _child.InvalidateMeasure();
            return size;
        }

        protected override void ArrangeCore(BRect finalRect) => _child.Arrange(finalRect);
    }

    private sealed class CountingMeasureElement : UiElement
    {
        public int MeasureCount { get; private set; }

        protected override BSize MeasureCore(BSize availableSize)
        {
            MeasureCount++;
            return new BSize(50, 20);
        }

        protected override void RenderCore(UiRenderContext context)
        {
        }
    }

    private sealed class InvalidationDuringRenderElement : UiElement
    {
        private bool _hasInvalidated;

        protected override BSize MeasureCore(BSize availableSize) => new(100, 50);

        protected override void RenderCore(UiRenderContext context)
        {
            if (!_hasInvalidated)
            {
                _hasInvalidated = true;
                Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange);
            }
        }
    }

    private sealed class TestContainer : UiElement
    {
        protected override BSize MeasureCore(BSize availableSize)
        {
            double width = 0;
            double height = 0;
            foreach (UiElement child in Children)
            {
                BSize desired = child.Measure(availableSize);
                width = Math.Max(width, desired.Width);
                height += desired.Height;
            }
            return new BSize(width, height);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            double y = finalRect.Top;
            foreach (UiElement child in Children)
            {
                child.Arrange(new BRect(finalRect.Left, y, finalRect.Width, child.DesiredSize.Height));
                y += child.DesiredSize.Height;
            }
        }
    }

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; set; } = viewportSize;
        public double Scale => 1.0;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }

    private sealed class AnimationTestHost(BSize viewportSize) : IUiHost, IUiAnimationHost
    {
        public BSize ViewportSize { get; set; } = viewportSize;
        public double Scale => 1.0;
        public bool IsAnimationActive { get; private set; }

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }

        public void StartAnimation() => IsAnimationActive = true;
        public void StopAnimation() => IsAnimationActive = false;
    }

    private sealed class ManualClock : IUiClock
    {
        public UiTimestamp Now { get; set; } = new(TimeSpan.Zero);
        public void Advance(TimeSpan delta) => Now = new UiTimestamp(Now.Elapsed + delta);
    }

    private static InputEventHeader Header(string id, long sequence = 1) =>
        new(InputDeviceId.FromOpaqueValue(id), new InputTimestamp(sequence, TimeSpan.TicksPerSecond, "test"), sequence);

    private static UiInputEvent PointerMove(double x, double y, long sequence = 1) =>
        UiInputEvent.FromMouseMove(
            new MouseMoveEvent(
                Header("mouse", sequence),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.None,
                InputEventSource.Synthetic));
}
