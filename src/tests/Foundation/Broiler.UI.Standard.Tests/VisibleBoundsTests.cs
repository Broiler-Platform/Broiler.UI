using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Forms.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.Standard;
using Broiler.UI.TabView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// What a host reports as on screen is what is drawn: bounds are clipped to every ancestor viewport,
/// content scrolled out of view is Offscreen, and list items and tab headers carry their real
/// geometry. ADR 0028.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class VisibleBoundsTests
{
    [Fact]
    public void ScrolledContentIsClippedToTheViewport()
    {
        // A 100x100 scroll view with a 10 DIP vertical scrollbar: the viewport is 90x100.
        var stack = new Stack(40, 40, 40, 40, 40);
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth };
        scroll.AddChild(stack);
        Layout(scroll, 100, 100);
        Assert.Equal(new BRect(0, 0, 90, 100), scroll.ContentBounds);

        // Not scrolled: whole blocks are unchanged, the block across the edge is cut there.
        Assert.Equal(stack.Children[0].Bounds, stack.Children[0].GetVisibleBounds());
        Assert.Equal(new BRect(0, 80, 90, 20), stack.Children[2].GetVisibleBounds());

        scroll.SetOffset(new BPoint(0, 30));
        Layout(scroll, 100, 100);

        Assert.Equal(new BRect(0, -30, 90, 40), stack.Children[0].Bounds);
        Assert.Equal(new BRect(0, 0, 90, 10), stack.Children[0].GetVisibleBounds());
        Assert.Equal(stack.Children[1].Bounds, stack.Children[1].GetVisibleBounds());
        Assert.Equal(new BRect(0, 90, 90, 10), stack.Children[3].GetVisibleBounds());
        Assert.True(stack.Children[4].GetVisibleBounds().IsEmpty);

        // Entirely scrolled out: Offscreen, but still Visible, since nothing hid it.
        UiSemanticState hidden = stack.Children[4].GetSemanticNode().State;
        Assert.True(hidden.HasFlag(UiSemanticState.Offscreen));
        Assert.True(hidden.HasFlag(UiSemanticState.Visible));
        Assert.False(stack.Children[3].GetSemanticNode().State.HasFlag(UiSemanticState.Offscreen));
        Assert.False(stack.Children[0].GetSemanticNode().State.HasFlag(UiSemanticState.Offscreen));
    }

    [Fact]
    public void NestedViewportsBothClipAndHiddenAncestorsHideEverything()
    {
        var inner = new StandardScrollView { ScrollbarThickness = 0, PreferredSize = new BSize(80, 60) };
        var innerContent = new Stack(50, 50, 50);
        inner.AddChild(innerContent);
        var outerContent = new Stack(30) { Tail = inner };
        var outer = new StandardScrollView { ScrollbarThickness = 0, Constraint = UiScrollConstraint.ConstrainWidth };
        outer.AddChild(outerContent);
        Layout(outer, 100, 70);

        // The inner viewport starts at y=30 and the outer one ends at y=70.
        Assert.Equal(new BRect(0, 30, 80, 60), inner.Bounds);
        Assert.Equal(new BRect(0, 30, 80, 40), inner.GetVisibleBounds());
        Assert.Equal(new BRect(0, 30, 80, 40), innerContent.Children[0].GetVisibleBounds());
        Assert.True(innerContent.Children[1].GetVisibleBounds().IsEmpty);

        inner.SetOffset(new BPoint(0, 40));
        Layout(outer, 100, 70);
        Assert.Equal(new BRect(0, 30, 80, 10), innerContent.Children[0].GetVisibleBounds());
        Assert.Equal(new BRect(0, 40, 80, 30), innerContent.Children[1].GetVisibleBounds());

        outerContent.Visibility = UiVisibility.Hidden;
        Assert.True(innerContent.Children[1].GetVisibleBounds().IsEmpty);
        innerContent.Visibility = UiVisibility.Collapsed;
        Assert.True(innerContent.GetVisibleBounds().IsEmpty);
    }

    [Fact]
    public void AFormFieldBelowTheViewportIsOffscreenUntilRevealed()
    {
        var fields = new StandardPanel { Spacing = 8 };
        FormField? last = null;
        for (int index = 1; index <= 12; index++)
            fields.AddChild(last = new FormField($"Field {index}", new StandardEdit()));
        using var surface = new FormSurface(fields, FormSurface.ActionBar(new StandardButton { Text = "Save" }), new InlineFeedback());
        using var session = new StandardUiSessionBuilder().Build(new TestHost(new BSize(640, 480)));
        session.AddRoot(surface);
        session.RenderFrame();

        Assert.True(last!.Control.GetVisibleBounds().IsEmpty);
        Assert.True(last.Control.GetSemanticNode().State.HasFlag(UiSemanticState.Offscreen));

        surface.Reveal(last);
        session.RenderFrame();

        BRect visible = last.Control.GetVisibleBounds();
        Assert.Equal(last.Control.Bounds, visible);
        Assert.False(last.Control.GetSemanticNode().State.HasFlag(UiSemanticState.Offscreen));
        Assert.True(visible.Bottom <= surface.Content.Scroll.ContentBounds.Bottom + 0.5);
    }

    [Fact]
    public void ListItemNodesCarryThePresenterNameAndTheirVisiblePart()
    {
        var list = new StandardListView { ItemHeight = 25, ItemPresenter = StandardTwoLineListItemPresenter.Instance };
        list.SetItems(Enumerable.Range(0, 20).Select(i => new UiListItem($"m{i}", $"Sender {i}") { SecondaryText = $"Subject {i}", IsRead = false }));
        using UiSession session = Attach(list, 200, 110);
        double rowHeight = list.GetItemSemanticNode(1)!.Bounds.Top - list.GetItemSemanticNode(0)!.Bounds.Top;
        int partial = (int)(110 / rowHeight);
        Assert.InRange(110 - (partial * rowHeight), 1, rowHeight - 1);

        UiSemanticNode first = list.GetItemSemanticNode(0)!;
        Assert.Equal("Unread, Sender 0, Subject 0", first.Name);
        Assert.True(first.State.HasFlag(UiSemanticState.Visible));
        Assert.Equal(rowHeight, first.Bounds.Height);

        // The row across the bottom edge is cut at the content area, like its drawing.
        UiSemanticNode cut = list.GetItemSemanticNode(partial)!;
        Assert.True(cut.State.HasFlag(UiSemanticState.Visible));
        Assert.False(cut.State.HasFlag(UiSemanticState.Offscreen));
        Assert.Equal(110, cut.Bounds.Bottom, 3);
        Assert.True(cut.Bounds.Height < rowHeight);
        Assert.True(cut.Bounds.Right <= list.Bounds.Right);
        // The list's own children use the same geometry, including the extra row it realizes below.
        IReadOnlyList<UiSemanticNode> rows = list.GetSemanticNode().Children;
        Assert.Equal(cut.Bounds, rows[partial].Bounds);
        Assert.True(rows[^1].State.HasFlag(UiSemanticState.Offscreen) || rows.Count == partial + 1);

        // A row out of view is Offscreen, not Visible, and says where scrolling would bring it.
        UiSemanticNode below = list.GetItemSemanticNode(partial + 2)!;
        Assert.True(below.State.HasFlag(UiSemanticState.Offscreen));
        Assert.False(below.State.HasFlag(UiSemanticState.Visible));
        Assert.Equal(rowHeight, below.Bounds.Height);
        Assert.Equal("Unread, Sender " + (partial + 2) + ", Subject " + (partial + 2), below.Name);

        Assert.Equal(5, list.IndexOf("m5"));
        Assert.Equal(-1, list.IndexOf("M5"));
    }

    [Fact]
    public void ListItemsAreClippedByAScrollViewAroundTheList()
    {
        var list = new StandardListView { ItemHeight = 25, PreferredSize = new BSize(200, 200) };
        list.SetItems(Enumerable.Range(0, 8).Select(i => new UiListItem($"i{i}", $"Item {i}")));
        var content = new Stack(60) { Tail = list };
        var scroll = new StandardScrollView { ScrollbarThickness = 0, Constraint = UiScrollConstraint.ConstrainWidth };
        scroll.AddChild(content);
        using UiSession session = Attach(scroll, 200, 100);

        // The list starts at y=60 in a 100 DIP viewport: only its first 40 DIP show.
        UiSemanticNode first = list.GetItemSemanticNode(0)!;
        Assert.True(first.State.HasFlag(UiSemanticState.Visible));
        Assert.Equal(85, first.Bounds.Bottom, 3);
        UiSemanticNode second = list.GetItemSemanticNode(1)!;
        Assert.True(second.State.HasFlag(UiSemanticState.Visible));
        Assert.Equal(100, second.Bounds.Bottom, 3);
        Assert.Equal(15, second.Bounds.Height, 3);
        Assert.True(list.GetItemSemanticNode(2)!.State.HasFlag(UiSemanticState.Offscreen));
    }

    [Fact]
    public void TabHeaderBoundsAreThePaintedHeadersAtLargeText()
    {
        var tabs = new StandardTabView();
        tabs.AddTab("inbox", "Inbox", new StandardPanel());
        tabs.AddTab("compose", "Compose a message", new StandardPanel());
        tabs.AddTab("settings", "Settings", new StandardPanel());
        tabs.Font = tabs.Font with { Size = tabs.Font.Size * 2 };
        using UiSession session = Attach(tabs, 640, 300);

        Assert.True(tabs.EffectiveHeaderHeight > tabs.HeaderHeight);
        double left = tabs.Bounds.Left;
        for (int index = 0; index < tabs.Tabs.Count; index++)
        {
            BRect header = tabs.GetTabHeaderBounds(index);
            Assert.Equal(left, header.Left, 3);
            Assert.Equal(tabs.EffectiveHeaderHeight, header.Height, 3);
            left = header.Right;

            tabs.SelectedIndex = index;
            BRenderList frame = session.RenderFrame();
            Assert.Contains(frame.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == header && fill.Color == tabs.SelectedHeaderBackground);
            Assert.Contains(frame.Commands.OfType<BRenderCommand.DrawText>(), text => text.Text.Text == tabs.Tabs[index].Header && Math.Abs(text.Origin.X - (header.Left + tabs.HeaderPaddingX)) < 0.001);

            UiSemanticNode node = tabs.GetSemanticNode().Children[index];
            AssertClose(header, node.Bounds);
            Assert.False(node.State.HasFlag(UiSemanticState.Offscreen));
        }

        // Widths follow the text, so a host cannot assume equal tabs.
        Assert.NotEqual(tabs.GetTabHeaderBounds(0).Width, tabs.GetTabHeaderBounds(1).Width);
        Assert.Equal(BRect.Empty, tabs.GetTabHeaderBounds(3));
        Assert.Equal(BRect.Empty, tabs.GetTabHeaderBounds(-1));

        // The pointer agrees: the centre of a header selects that tab.
        BRect settings = tabs.GetTabHeaderBounds(2);
        tabs.SelectedIndex = 0;
        session.DispatchInput(MouseDown(settings.Left + (settings.Width / 2), settings.Top + (settings.Height / 2)));
        Assert.Equal(2, tabs.SelectedIndex);
    }

    private static void AssertClose(BRect expected, BRect actual)
    {
        Assert.Equal(expected.Left, actual.Left, 6);
        Assert.Equal(expected.Top, actual.Top, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }

    private static void Layout(UiElement element, double width, double height)
    {
        element.Measure(new BSize(width, height));
        element.Arrange(new BRect(0, 0, width, height));
    }

    private static UiSession Attach(UiElement root, double width, double height)
    {
        UiSession session = new StandardUiSessionBuilder().Build(new TestHost(new BSize(width, height)));
        session.AddRoot(root);
        session.RenderFrame();
        return session;
    }

    private static UiInputEvent MouseDown(double x, double y) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                new InputEventHeader(
                    InputDeviceId.FromOpaqueValue("mouse"),
                    new InputTimestamp(1, TimeSpan.TicksPerSecond, "visible-bounds-test"),
                    1),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

    /// <summary>Fixed-height blocks stacked top to bottom, optionally followed by one more element.</summary>
    private sealed class Stack : UiElement
    {
        public Stack(params double[] heights)
        {
            foreach (double height in heights)
                AddChild(new Block(height));
        }

        public UiElement? Tail
        {
            get => Children.Count > 0 && Children[^1] is not Block ? Children[^1] : null;
            init => AddChild(value!);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 100;
            double height = 0;
            foreach (UiElement child in Children)
                height += child.Measure(new BSize(width, double.PositiveInfinity)).Height;
            return new BSize(width, height);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            double top = finalRect.Top;
            foreach (UiElement child in Children)
            {
                double width = child is Block ? finalRect.Width : child.DesiredSize.Width;
                child.Arrange(new BRect(finalRect.Left, top, width, child.DesiredSize.Height));
                top += child.DesiredSize.Height;
            }
        }
    }

    private sealed class Block(double height) : UiElement
    {
        protected override BSize MeasureCore(BSize availableSize) =>
            new(double.IsFinite(availableSize.Width) ? availableSize.Width : 100, height);
    }

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize => viewportSize;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
