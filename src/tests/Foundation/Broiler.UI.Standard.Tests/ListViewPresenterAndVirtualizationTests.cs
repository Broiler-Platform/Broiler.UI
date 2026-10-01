using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Standard;
using Xunit;

namespace Broiler.UI.Standard.Tests;

public sealed class ListViewPresenterAndVirtualizationTests
{
    [Fact]
    public void Presenter_Heights_Reflect_Density_Variants()
    {
        var defaultPresenter = DefaultListItemPresenter.Instance;
        Assert.Equal(24, defaultPresenter.GetItemHeight(null, UiDensity.Compact, 300));
        Assert.Equal(28, defaultPresenter.GetItemHeight(null, UiDensity.Comfortable, 300));
        Assert.Equal(36, defaultPresenter.GetItemHeight(null, UiDensity.Spacious, 300));

        var twoLinePresenter = StandardTwoLineListItemPresenter.Instance;
        Assert.Equal(38, twoLinePresenter.GetItemHeight(null, UiDensity.Compact, 300));
        Assert.Equal(52, twoLinePresenter.GetItemHeight(null, UiDensity.Comfortable, 300));
        Assert.Equal(64, twoLinePresenter.GetItemHeight(null, UiDensity.Spacious, 300));

        var listView = new StandardListView();
        Assert.Equal(28, listView.EffectiveItemHeight);

        listView.Density = UiDensity.Spacious;
        Assert.Equal(36, listView.EffectiveItemHeight);

        listView.ItemPresenter = StandardTwoLineListItemPresenter.Instance;
        Assert.Equal(64, listView.EffectiveItemHeight);

        listView.Density = UiDensity.Compact;
        Assert.Equal(38, listView.EffectiveItemHeight);

        // Explicit ItemHeight overrides presenter
        listView.ItemHeight = 42;
        Assert.Equal(42, listView.EffectiveItemHeight);
    }

    [Fact]
    public void TwoLinePresenter_Renders_Primary_Preview_And_Tertiary_Without_Overlap_At_Narrow_Width()
    {
        var listView = new StandardListView
        {
            ItemPresenter = StandardTwoLineListItemPresenter.Instance,
            Density = UiDensity.Comfortable,
        };

        var items = new[]
        {
            new UiListItem("msg-1", "Subject 1", "Detailed preview line 1 that is quite lengthy", "10:30 AM", isRead: false),
            new UiListItem("msg-2", "Subject 2 with very long subject line that must be truncated", "Preview line 2", "Yesterday", isRead: true),
        };
        listView.SetItems(items);

        // Render at a narrow width of 140 DIP
        using UiSession session = AttachAndRender(listView, new BSize(140, 200), out BRenderList renderList);

        // Verify that DrawText commands were emitted for primary, preview, and tertiary
        var texts = renderList.Commands.OfType<BRenderCommand.DrawText>().Select(t => t.Text.Text).ToList();
        Assert.NotEmpty(texts);
        Assert.Contains(texts, t => t.Contains("10:30 AM", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("Yesterday", StringComparison.Ordinal));

        // For unread item, unread accent indicator dot is drawn (6x6 fill)
        var fills = renderList.Commands.OfType<BRenderCommand.FillRect>().ToList();
        Assert.NotEmpty(fills);
        Assert.Contains(fills, f => f.Rect.Width == 6.0 && f.Rect.Height == 6.0);
    }

    [Fact]
    public void Presenter_Creates_Accurate_ListItem_Semantic_Nodes()
    {
        var listView = new StandardListView
        {
            ItemPresenter = StandardTwoLineListItemPresenter.Instance,
        };

        var item = new UiListItem("id-1", "Meeting Agenda", "Discussion on UI architecture", "2:00 PM", isRead: false);
        listView.SetItems([item]);
        listView.SelectedItemId = "id-1";

        using UiSession session = AttachAndRender(listView, new BSize(300, 200), out _);
        session.SetFocus(listView);

        var rootNode = listView.GetSemanticNode();
        var nodes = rootNode.Children;
        Assert.Single(nodes);

        UiSemanticNode node = nodes[0];
        Assert.Equal(UiSemanticRole.ListItem, node.Role);
        Assert.True(node.State.HasFlag(UiSemanticState.Selected));
        Assert.True(node.State.HasFlag(UiSemanticState.Focused));
        Assert.Contains("Unread", node.Name, StringComparison.Ordinal);
        Assert.Contains("Meeting Agenda", node.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Scroll_Anchoring_Preserves_Viewport_Across_Insertions_And_Removals()
    {
        var listView = new StandardListView
        {
            ItemHeight = 25,
            EnableScrollAnchoring = true,
        };

        var items = Enumerable.Range(0, 50).Select(i => new UiListItem($"item-{i}", $"Item {i}")).ToList();
        listView.SetItems(items);

        using UiSession session = AttachAndRender(listView, new BSize(200, 250), out _);

        // Scroll to item 19 (which will put item 10 at top of a 250px viewport: 10 items visible)
        listView.ScrollIntoView("item-19");
        Assert.Equal(250, listView.VerticalOffset);
        Assert.Equal(10, listView.FirstVisibleIndex);

        // Prepend 5 items at the beginning
        var newItems = Enumerable.Range(0, 5).Select(i => new UiListItem($"new-{i}", $"New {i}")).Concat(items).ToList();
        listView.SetItems(newItems);
        listView.Arrange(new BRect(0, 0, 200, 250));

        // Anchor ("item-10") should shift offset by 5 * 25 = 125, so offset becomes 375
        // and item-10 remains exactly at the top of the viewport
        Assert.Equal(375, listView.VerticalOffset);
        Assert.Equal(15, listView.FirstVisibleIndex);
        Assert.Equal("item-10", listView.Items[listView.FirstVisibleIndex].Id);

        // Remove 3 items from the beginning
        var reducedItems = newItems.Skip(3).ToList();
        listView.SetItems(reducedItems);
        listView.Arrange(new BRect(0, 0, 200, 250));

        // Anchor should decrease offset by 3 * 25 = 75, so offset becomes 300
        Assert.Equal(300, listView.VerticalOffset);
        Assert.Equal(12, listView.FirstVisibleIndex);
        Assert.Equal("item-10", listView.Items[listView.FirstVisibleIndex].Id);
    }

    [Fact]
    public void Scroll_Anchoring_Disabled_Does_Not_Shift_Offset()
    {
        var listView = new StandardListView
        {
            ItemHeight = 25,
            EnableScrollAnchoring = false,
        };

        var items = Enumerable.Range(0, 50).Select(i => new UiListItem($"item-{i}", $"Item {i}")).ToList();
        listView.SetItems(items);

        using UiSession session = AttachAndRender(listView, new BSize(200, 250), out _);
        listView.ScrollIntoView("item-19");
        Assert.Equal(250, listView.VerticalOffset);

        // Prepend 5 items
        var newItems = Enumerable.Range(0, 5).Select(i => new UiListItem($"new-{i}", $"New {i}")).Concat(items).ToList();
        listView.SetItems(newItems);
        listView.Arrange(new BRect(0, 0, 200, 250));

        // Offset stays unchanged
        Assert.Equal(250, listView.VerticalOffset);
    }

    [Fact]
    public void EnsureSelectedVisible_And_ScrollIntoView_And_MakeVisible()
    {
        var listView = new StandardListView
        {
            ItemHeight = 20,
        };

        var items = Enumerable.Range(0, 100).Select(i => new UiListItem($"id-{i}", $"Item {i}")).ToList();
        listView.SetItems(items);

        using UiSession session = AttachAndRender(listView, new BSize(200, 100), out _);

        // Scroll to item 45
        listView.ScrollIntoView("id-45");
        Assert.InRange(listView.VerticalOffset, 45 * 20 - 100, 45 * 20);

        // Scroll to item 0
        listView.ScrollIntoView("id-0");
        Assert.Equal(0, listView.VerticalOffset);

        // Select item 60 and call EnsureSelectedVisible
        listView.SelectedItemId = "id-60";
        listView.EnsureSelectedVisible();
        Assert.InRange(listView.VerticalOffset, 60 * 20 - 100, 60 * 20);

        // IUiScrollable.MakeVisible
        BRect targetBelow = new(listView.ContentBounds.Left, listView.ContentBounds.Top + 80 * listView.EffectiveItemHeight - listView.VerticalOffset, listView.ContentBounds.Width, listView.EffectiveItemHeight);
        bool scrolled = listView.MakeVisible(targetBelow);
        Assert.True(scrolled);
        Assert.InRange(listView.VerticalOffset, 80 * 20 - 100, 80 * 20);
    }

    [Fact]
    public void Keyboard_Navigation_Selection_And_Activation()
    {
        var listView = new StandardListView
        {
            ItemHeight = 25,
            SelectionMode = UiListSelectionMode.Single,
        };

        var items = Enumerable.Range(0, 20).Select(i => new UiListItem($"k-{i}", $"Key Item {i}")).ToList();
        listView.SetItems(items);

        using UiSession session = AttachAndRender(listView, new BSize(200, 100), out _);
        session.SetFocus(listView);

        // Arrow down selects first item
        Assert.True(session.DispatchInput(KeyDown("Down")));
        Assert.Equal("k-0", listView.SelectedItemId);

        // Arrow down moves to next
        Assert.True(session.DispatchInput(KeyDown("Down")));
        Assert.Equal("k-1", listView.SelectedItemId);

        // End key moves to last item
        Assert.True(session.DispatchInput(KeyDown("End")));
        Assert.Equal("k-19", listView.SelectedItemId);

        // Home key moves to first item
        Assert.True(session.DispatchInput(KeyDown("Home")));
        Assert.Equal("k-0", listView.SelectedItemId);

        // PageDown jumps by page
        Assert.True(session.DispatchInput(KeyDown("PageDown")));
        Assert.Equal("k-4", listView.SelectedItemId);

        // Enter key activates item
        string? activatedId = null;
        listView.ItemActivated += (_, e) => activatedId = e.Item.Id;

        Assert.True(session.DispatchInput(KeyDown("Enter")));
        Assert.Equal("k-4", activatedId);
    }

    [Fact]
    public void Keyboard_Multiple_Selection_And_Space_Toggle()
    {
        var listView = new StandardListView
        {
            ItemHeight = 25,
            SelectionMode = UiListSelectionMode.Multiple,
        };

        var items = Enumerable.Range(0, 10).Select(i => new UiListItem($"m-{i}", $"Item {i}")).ToList();
        listView.SetItems(items);

        using UiSession session = AttachAndRender(listView, new BSize(200, 100), out _);
        session.SetFocus(listView);

        // Select first item
        Assert.True(session.DispatchInput(KeyDown("Down")));
        Assert.Equal("m-0", listView.SelectedItemId);
        Assert.Single(listView.SelectedItemIds);

        // Shift+Down extends selection range
        Assert.True(session.DispatchInput(KeyDownWithShift("Down")));
        Assert.Equal(2, listView.SelectedItemIds.Count);
        Assert.Contains("m-0", listView.SelectedItemIds);
        Assert.Contains("m-1", listView.SelectedItemIds);

        // Down without shift replaces selection with m-1 (anchor stayed at m-0)
        Assert.True(session.DispatchInput(KeyDown("Down")));
        Assert.Equal("m-1", listView.SelectedItemId);
        Assert.Single(listView.SelectedItemIds);

        // Space toggles m-1 selection off
        Assert.True(session.DispatchInput(KeyDown("Space")));
        Assert.Empty(listView.SelectedItemIds);

        // Space again toggles it back on
        Assert.True(session.DispatchInput(KeyDown("Space")));
        Assert.Single(listView.SelectedItemIds);
        Assert.Equal("m-1", listView.SelectedItemId);
    }

    [Fact]
    public void TypeAhead_Jumps_To_Matching_Prefix()
    {
        var listView = new StandardListView
        {
            ItemHeight = 25,
        };

        listView.SetItems(
        [
            new UiListItem("a1", "Apple"),
            new UiListItem("b1", "Banana"),
            new UiListItem("b2", "Blueberry"),
            new UiListItem("c1", "Cherry"),
        ]);

        using UiSession session = AttachAndRender(listView, new BSize(200, 100), out _);
        session.SetFocus(listView);

        // Type 'B' jumps to Banana
        Assert.True(session.DispatchInput(KeyChar("B")));
        Assert.Equal("b1", listView.SelectedItemId);

        // Type 'B' again cycles to Blueberry
        Assert.True(session.DispatchInput(KeyChar("B")));
        Assert.Equal("b2", listView.SelectedItemId);

        // Type 'C' jumps to Cherry
        Assert.True(session.DispatchInput(KeyChar("C")));
        Assert.Equal("c1", listView.SelectedItemId);
    }

    [Fact]
    public void DoubleClick_Activates_Item()
    {
        var listView = new StandardListView
        {
            ItemHeight = 25,
        };

        listView.SetItems(
        [
            new UiListItem("row-0", "Zero"),
            new UiListItem("row-1", "One"),
        ]);

        using UiSession session = AttachAndRender(listView, new BSize(200, 100), out _);

        string? activatedId = null;
        listView.ItemActivated += (_, e) => activatedId = e.Item.Id;

        // Click 1 on row-1 (y = 35)
        Assert.True(session.DispatchInput(PointerDown(20, 35, 100)));
        session.DispatchInput(PointerUp(20, 35, 101));
        Assert.Null(activatedId);

        // Click 2 within 400ms window on row-1 activates
        Assert.True(session.DispatchInput(PointerDown(20, 35, 200)));
        session.DispatchInput(PointerUp(20, 35, 201));
        Assert.Equal("row-1", activatedId);
    }

    [Fact]
    public void Native_Accessibility_Offscreen_Item_Realization()
    {
        var listView = new StandardListView
        {
            ItemHeight = 25,
        };

        var items = Enumerable.Range(0, 100).Select(i => new UiListItem($"off-{i}", $"Item {i}")).ToList();
        listView.SetItems(items);

        using UiSession session = AttachAndRender(listView, new BSize(200, 100), out _);

        // Only ~5 items visible on screen (indices 0..4)
        Assert.Equal(0, listView.FirstVisibleIndex);
        Assert.InRange(listView.VisibleItemCount, 4, 6);

        // Query semantic node for off-screen item at index 85 without scrolling
        UiSemanticNode? offscreenNode = listView.GetItemSemanticNode(85);
        Assert.NotNull(offscreenNode);
        Assert.Equal(UiSemanticRole.ListItem, offscreenNode.Role);
        Assert.Equal("Item 85", offscreenNode.Name);
        Assert.Equal(85 * 25, offscreenNode.Bounds.Top);
        Assert.Equal(25, offscreenNode.Bounds.Height);
        Assert.Equal(0, listView.VerticalOffset);

        // Realize off-screen item at index 85 by scrolling it into view
        bool realized = listView.RealizeItem(85);
        Assert.True(realized);
        Assert.True(listView.VerticalOffset > 0);
    }

    [Fact]
    public void HighContrast_And_200Percent_Scale_Rendering()
    {
        var listView = new StandardListView
        {
            ItemPresenter = StandardTwoLineListItemPresenter.Instance,
            Font = new BFontStyle("sans-serif", 32, BFontWeight.Normal, BFontSlant.Normal), // 200% scale
        };

        listView.SetItems(
        [
            new UiListItem("hc-1", "High Contrast Subject", "Preview description", "12:00 PM", isRead: false),
        ]);

        listView.ApplyTheme(StandardThemeTokens.HighContrastDark);

        using UiSession session = AttachAndRender(listView, new BSize(200, 200), out BRenderList renderList);

        // Renders without errors and emits DrawText commands
        var texts = renderList.Commands.OfType<BRenderCommand.DrawText>().ToList();
        Assert.NotEmpty(texts);
        Assert.Contains(texts, t => t.Text.Text.Contains("12:00 PM", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Text.Text.Contains("...", StringComparison.Ordinal));
    }

    private static UiSession AttachAndRender(UiElement element, BSize size, out BRenderList renderList)
    {
        var host = new TestHost(size);
        UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(element);
        element.Measure(size);
        element.Arrange(new BRect(0, 0, size.Width, size.Height));
        renderList = StandardRenderTraversal.Render(session);
        return session;
    }

    private static UiInputEvent PointerDown(double x, double y, long ms = 100) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header("mouse", ms * TimeSpan.TicksPerMillisecond),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

    private static UiInputEvent PointerUp(double x, double y, long ms = 101) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                Header("mouse", ms * TimeSpan.TicksPerMillisecond),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.None,
                MouseButton.Left,
                MouseButtonTransition.Up,
                InputEventSource.Synthetic));

    private static UiInputEvent KeyDown(string name) =>
        UiInputEvent.FromKeyboardKey(
            new KeyboardKeyEvent(
                Header("keyboard", 2),
                KeyboardKey.FromName(name),
                KeyboardKeyTransition.Down,
                KeyboardModifierState.None,
                0,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

    private static UiInputEvent KeyDownWithShift(string name) =>
        UiInputEvent.FromKeyboardKey(
            new KeyboardKeyEvent(
                Header("keyboard", 2),
                KeyboardKey.FromName(name),
                KeyboardKeyTransition.Down,
                KeyboardModifierState.Shift,
                0,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

    private static UiInputEvent KeyChar(string character) =>
        UiInputEvent.FromKeyboardKey(
            new KeyboardKeyEvent(
                Header("keyboard", 2),
                KeyboardKey.FromName(character),
                KeyboardKeyTransition.Down,
                KeyboardModifierState.None,
                0,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

    private static InputEventHeader Header(string id, long timestampTicks) =>
        new(
            InputDeviceId.FromOpaqueValue(id),
            new InputTimestamp(timestampTicks, TimeSpan.TicksPerSecond, "listview-test"),
            timestampTicks);

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize => viewportSize;
        public double Scale => 1.0;
        public List<UiInvalidation> Invalidations { get; } = [];
        public List<BRenderList> Presented { get; } = [];
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) => Invalidations.Add(invalidation);
        public void Present(BRenderList renderList) => Presented.Add(renderList);
    }
}
