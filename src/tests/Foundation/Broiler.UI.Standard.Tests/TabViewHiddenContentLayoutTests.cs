using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Forms;
using Broiler.UI.Forms.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.ScrollView;
using Broiler.UI.TabView;
using Broiler.UI.TabView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// A tab the user leaves keeps its layout and scroll positions while another tab is shown, and is laid
/// out again only when it comes back at another size.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class TabViewHiddenContentLayoutTests
{
    [Theory]
    [InlineData(UiTabContentLifetimePolicy.RetainInactive)]
    [InlineData(UiTabContentLifetimePolicy.CollapseInactive)]
    public void AHiddenTabKeepsItsScrollPositionsAndLayoutUntilItIsShownAgain(UiTabContentLifetimePolicy policy)
    {
        using var scene = Scene.Create(policy, new BSize(640, 480));
        UiScrollView[] scrolled = [scene.Form.Content.Scroll, scene.FeedbackScroll];
        foreach (UiScrollView scroll in scrolled)
        {
            Assert.True(scroll.ExtentSize.Height > scroll.ViewportSize.Height + 2, "The test needs content that scrolls.");
            scroll.SetOffset(new BPoint(0, Math.Round((scroll.ExtentSize.Height - scroll.ViewportSize.Height) / 2)));
        }
        scene.Render();
        BPoint[] offsets = scrolled.Select(scroll => scroll.Offset).ToArray();
        (UiElement Element, BRect Bounds)[] layout = Layout(scene.Form);

        scene.Tabs.SelectTab("other");
        scene.Render();

        // Hidden, it is neither laid out again nor moved: the layout is the one the user left.
        Assert.Equal(layout, Layout(scene.Form));
        Assert.Equal(offsets, scrolled.Select(scroll => scroll.Offset));

        scene.Tabs.SelectTab("form");
        scene.Render();

        Assert.Equal(offsets, scrolled.Select(scroll => scroll.Offset));
        Assert.Equal(layout, Layout(scene.Form));
    }

    [Theory]
    [InlineData(UiTabContentLifetimePolicy.RetainInactive, 0, 160)]
    [InlineData(UiTabContentLifetimePolicy.RetainInactive, 200, 0)]
    [InlineData(UiTabContentLifetimePolicy.CollapseInactive, 0, 160)]
    [InlineData(UiTabContentLifetimePolicy.CollapseInactive, 200, 0)]
    public void AHiddenTabShownAgainAfterAResizeIsLaidOutAtItsNewSizeInEitherDimension(UiTabContentLifetimePolicy policy, double extraWidth, double extraHeight)
    {
        using var scene = Scene.Create(policy, new BSize(640, 480));
        scene.Tabs.SelectTab("other");
        scene.Render();

        scene.Host.ViewportSize = new BSize(640 + extraWidth, 480 + extraHeight);
        scene.Render();
        scene.Tabs.SelectTab("form");
        // One frame: the first one that shows the tab again must already have it right.
        scene.Session.RenderFrame();

        BRect area = scene.Form.Bounds;
        Assert.Equal(new BRect(0, scene.Tabs.EffectiveHeaderHeight, 640 + extraWidth, 480 + extraHeight - scene.Tabs.EffectiveHeaderHeight), area);
        AssertLaidOutAtItsRectangle(scene.Form, area);
    }

    [Theory]
    [InlineData(UiTabContentLifetimePolicy.RetainInactive, 0, 160)]
    [InlineData(UiTabContentLifetimePolicy.RetainInactive, 200, 0)]
    [InlineData(UiTabContentLifetimePolicy.CollapseInactive, 0, 160)]
    [InlineData(UiTabContentLifetimePolicy.CollapseInactive, 200, 0)]
    public void ATabShownAgainIsMeasuredAtTheSizeItIsArrangedAt(UiTabContentLifetimePolicy policy, double extraWidth, double extraHeight)
    {
        var content = new RecordsMeasure();
        using var tabs = new StandardTabView { InactiveContentPolicy = policy };
        tabs.AddTab("content", "Content", content);
        tabs.AddTab("other", "Other", new StandardPanel());
        var host = new Host(new BSize(640, 480));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(tabs);
        session.RenderFrame();
        tabs.SelectTab("other");
        session.RenderFrame();

        host.ViewportSize = new BSize(640 + extraWidth, 480 + extraHeight);
        session.RenderFrame();
        tabs.SelectTab("content");
        session.RenderFrame();

        Assert.Equal(new BSize(640 + extraWidth, 480 + extraHeight - tabs.EffectiveHeaderHeight), content.Bounds.Size);
        Assert.Equal(content.Bounds.Size, content.LastAvailableSize);
    }

    [Fact]
    public void TheShownTabIsMeasuredAtTheHeightItGetsWhenTheTabViewWasMeasuredUnbounded()
    {
        // A container that asks the tab view for its natural height and then gives it the window.
        // The width is the same either way, which is all the tab view used to compare.
        var content = new RecordsMeasure();
        using var tabs = new StandardTabView();
        tabs.AddTab("only", "Only", content);
        var parent = new MeasuresUnboundedHeight();
        parent.AddChild(tabs);
        using UiSession session = new StandardUiSessionBuilder().Build(new Host(new BSize(640, 480)));
        session.AddRoot(parent);
        session.RenderFrame();

        Assert.Equal(new BSize(640, 480 - tabs.EffectiveHeaderHeight), content.Bounds.Size);
        Assert.Equal(content.Bounds.Size, content.LastAvailableSize);
    }

    [Fact]
    public void ATabViewStackedAtTheHeightItAskedForMeasuresItsContentOncePerLayout()
    {
        // A vertical stack offers the tab view the window's height and arranges it at the height it
        // asked for. The content fits either way, so measuring it again at the smaller height only
        // measured it twice on every pass, at two sizes, and the measure cache missed both times.
        var content = new RecordsMeasure { FixedHeight = 200 };
        using var tabs = new StandardTabView();
        tabs.AddTab("only", "Only", content);
        tabs.AddTab("other", "Other", new StandardPanel());
        var stack = new StandardPanel();
        stack.AddChild(tabs);
        using UiSession session = new StandardUiSessionBuilder().Build(new Host(new BSize(640, 480)));
        session.AddRoot(stack);
        session.RenderFrame();
        Assert.Equal(new BSize(640, 200), content.Bounds.Size);

        int before = content.MeasureCount;
        for (int pass = 0; pass < 10; pass++)
        {
            content.InvalidateMeasure();
            session.RenderFrame();
        }

        Assert.Equal(10, content.MeasureCount - before);
        Assert.Equal(new BSize(640, 200), content.Bounds.Size);
    }

    /// <summary>
    /// The form's feedback ends at the bottom of its rectangle, and measuring it again at that
    /// rectangle changes nothing: it was measured at the size it was given.
    /// </summary>
    private static void AssertLaidOutAtItsRectangle(FormSurface form, BRect area)
    {
        UiElement feedback = Descendants(form).OfType<UiScrollView>().Last();
        Assert.Equal(area.Bottom - 12, feedback.Bounds.Bottom, 0.5);
        // The actions keep the 12 DIP margins; the form's viewport reaches into them by the room it leaves
        // beside its fields for their focus rings.
        Assert.Equal(area.Width - 24, form.Actions.Bounds.Width, 0.5);
        Assert.Equal(area.Width - 24 + (2 * form.Content.Scroll.HorizontalContentInset), form.Content.Bounds.Width, 0.5);

        (UiElement Element, BRect Bounds)[] layout = Layout(form);
        form.InvalidateMeasure();
        form.Measure(area.Size);
        form.Arrange(area);
        Assert.Equal(layout, Layout(form));
    }

    [Fact]
    public void APointerOverTheShownTabNeverReachesAHiddenTabUnderIt()
    {
        // The hidden tab comes after the shown one, so it would be hit first where their bounds meet.
        using var scene = Scene.Create(UiTabContentLifetimePolicy.RetainInactive, new BSize(640, 480), formFirst: false);
        scene.Tabs.SelectTab("form");
        scene.Render();
        scene.Tabs.SelectTab("other");
        scene.Render();

        BPoint point = new(scene.Other.Bounds.Left + 40, scene.Other.Bounds.Top + 20);
        Assert.True(scene.Form.Bounds.Contains(point), "The hidden form keeps its bounds under the shown tab.");
        UiElement? hit = scene.Session.HitTest(point);
        Assert.NotNull(hit);
        Assert.True(ReferenceEquals(hit, scene.Other) || hit!.IsDescendantOf(scene.Other), $"The pointer reached {hit!.GetType().Name}.");

        scene.Session.DispatchInput(UiInputEvent.FromMouseWheel(new MouseWheelEvent(
            new InputEventHeader(InputDeviceId.FromOpaqueValue("mouse"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "tabs"), 1),
            InputPoint.ClientDeviceIndependentPixels(point.X, point.Y),
            MouseButtons.None,
            MouseWheelAxis.Vertical,
            -1,
            InputEventSource.Synthetic)));
        Assert.Equal(0, scene.Form.Content.Scroll.VerticalOffset);
    }

    private static (UiElement Element, BRect Bounds)[] Layout(UiElement root) =>
        Descendants(root).Select(element => (element, element.Bounds)).ToArray();

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
            foreach (UiElement nested in Descendants(child))
                yield return nested;
    }

    private sealed class Scene : IDisposable
    {
        private Scene(StandardTabView tabs, FormSurface form, StandardPanel other, UiSession session, Host host)
        {
            Tabs = tabs;
            Form = form;
            Other = other;
            Session = session;
            Host = host;
        }

        public StandardTabView Tabs { get; }
        public FormSurface Form { get; }
        public StandardPanel Other { get; }
        public UiSession Session { get; }
        public Host Host { get; }
        public UiScrollView FeedbackScroll => Descendants(Form).OfType<UiScrollView>().Last();

        public static Scene Create(UiTabContentLifetimePolicy policy, BSize size, bool formFirst = true)
        {
            var fields = new StandardPanel { Spacing = 8 };
            for (int index = 1; index <= 14; index++)
                fields.AddChild(new FormField($"Field {index}", new StandardEdit(), "A description that wraps in a narrow window."));
            var feedback = new InlineFeedback();
            feedback.Set(string.Join(" ", Enumerable.Repeat("Connection feedback with details.", 40)), FeedbackKind.Error);
            var form = new FormSurface(fields, FormSurface.ActionBar(new StandardButton { Text = "Save" }), feedback);
            var other = new StandardPanel();
            other.AddChild(new StandardEdit());
            var tabs = new StandardTabView { InactiveContentPolicy = policy };
            if (formFirst)
            {
                tabs.AddTab("form", "Form", form);
                tabs.AddTab("other", "Other", other);
            }
            else
            {
                tabs.AddTab("other", "Other", other);
                tabs.AddTab("form", "Form", form);
            }

            var host = new Host(size);
            UiSession session = new StandardUiSessionBuilder().Build(host);
            session.AddRoot(tabs);
            var scene = new Scene(tabs, form, other, session, host);
            scene.Render();
            return scene;
        }

        public void Render()
        {
            Session.RenderFrame();
            Session.RenderFrame();
        }

        public void Dispose() => Session.Dispose();
    }

    private sealed class RecordsMeasure : UiElement
    {
        public BSize LastAvailableSize { get; private set; }
        public int MeasureCount { get; private set; }

        /// <summary>A height asked for whatever is offered; otherwise all of it.</summary>
        public double? FixedHeight { get; init; }

        protected override BSize MeasureCore(BSize availableSize)
        {
            LastAvailableSize = availableSize;
            MeasureCount++;
            return new BSize(availableSize.Width, FixedHeight ?? (double.IsFinite(availableSize.Height) ? availableSize.Height : 100));
        }
    }

    private sealed class MeasuresUnboundedHeight : UiElement
    {
        protected override BSize MeasureCore(BSize availableSize)
        {
            foreach (UiElement child in Children)
                child.Measure(new BSize(availableSize.Width, double.PositiveInfinity));
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            foreach (UiElement child in Children)
                child.Arrange(finalRect);
        }
    }

    private sealed class Host(BSize size) : IUiHost
    {
        public BSize ViewportSize { get; set; } = size;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
