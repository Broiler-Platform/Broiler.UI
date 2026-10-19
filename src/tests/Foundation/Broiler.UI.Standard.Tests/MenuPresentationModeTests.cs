using System;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class MenuPresentationModeTests
{
    [Fact]
    public void UiMenuPresentationMode_Enum_Defines_Expected_Modes()
    {
        Assert.Equal(0, (int)UiMenuPresentationMode.MenuBar);
        Assert.Equal(1, (int)UiMenuPresentationMode.ContextMenu);
        Assert.Equal(2, (int)UiMenuPresentationMode.Overflow);
        Assert.Equal(3, (int)UiMenuPresentationMode.Drawer);
        Assert.Equal(4, (int)UiMenuPresentationMode.BottomSheet);
        Assert.Equal(5, (int)UiMenuPresentationMode.TabBar);

        var menu = new StandardMenu();
        foreach (UiMenuPresentationMode mode in Enum.GetValues<UiMenuPresentationMode>())
        {
            menu.PresentationMode = mode;
            Assert.Equal(mode, menu.PresentationMode);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => menu.PresentationMode = (UiMenuPresentationMode)999);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MenuBar_Mode_Clicking_Open_Entry_Closes_Popups_And_Allows_Reopening(bool openSubmenu)
    {
        var menu = new StandardMenu();
        UiMenuItem file = new("file", "File");
        UiMenuItem recent = new("recent", "Recent");
        recent.Children.Add(new UiMenuItem("document", "Document"));
        file.Children.Add(recent);
        menu.SetItems([file]);
        int invocationCount = 0;
        menu.ItemInvoked += (_, _) => invocationCount++;

        var host = new TestHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(0, 0, 400, 28));
        session.AddRoot(root);
        session.RenderFrame();

        Assert.True(Click(session, 10, 10));
        Assert.True(menu.IsOpen);
        Assert.Same(menu, session.CapturedElement);
        session.RenderFrame();

        if (openSubmenu)
        {
            Assert.True(Click(session, menu.OverlayBounds.Left + 10, menu.OverlayBounds.Top + 10));
            Assert.Equal(new[] { 0, 0 }, menu.SelectedPath);
            session.RenderFrame();
        }

        Assert.True(Click(session, 10, 10));
        Assert.False(menu.IsOpen);
        Assert.True(menu.OverlayBounds.IsEmpty);
        Assert.Null(session.CapturedElement);
        Assert.Equal(0, invocationCount);
        BRenderList closed = session.RenderFrame();
        Assert.DoesNotContain(closed.Commands.OfType<BRenderCommand.DrawText>(), cmd => cmd.Text.Text.StartsWith("Recent") || cmd.Text.Text == "Document");

        Assert.True(Click(session, 10, 10));
        Assert.True(menu.IsOpen);
        Assert.Equal(new[] { 0 }, menu.SelectedPath);
        Assert.Same(menu, session.CapturedElement);
    }

    [Fact]
    public void MenuBar_Mode_Clicking_Different_Entry_Switches_Popup_Before_Toggling_Closed()
    {
        var menu = new StandardMenu();
        UiMenuItem file = new("file", "File");
        file.Children.Add(new UiMenuItem("new", "New"));
        UiMenuItem edit = new("edit", "Edit");
        edit.Children.Add(new UiMenuItem("copy", "Copy"));
        menu.SetItems([file, edit]);

        var host = new TestHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(new TestContainer(menu, new BRect(0, 0, 400, 28)));
        session.RenderFrame();
        double editX = menu.TopLevelBounds[1].Left + 10;

        Assert.True(Click(session, 10, 10));
        Assert.True(Click(session, editX, 10));
        Assert.True(menu.IsOpen);
        Assert.Equal(new[] { 1 }, menu.SelectedPath);
        Assert.Same(menu, session.CapturedElement);

        Assert.True(Click(session, editX, 10));
        Assert.False(menu.IsOpen);
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void Overflow_Mode_Measures_Desired_Button_Size()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.Overflow,
            MenuBarHeight = 36,
        };
        menu.SetItems([new UiMenuItem("1", "File"), new UiMenuItem("2", "Edit")]);
        menu.Measure(new BSize(500, 500));

        Assert.True(menu.DesiredSize.Width >= 32);
        Assert.Equal(36, menu.DesiredSize.Height);
    }

    [Fact]
    public void Overflow_Mode_Renders_Glyph_And_Toggles_Open_On_Click()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.Overflow,
            OverflowGlyph = "⋮",
        };
        menu.SetItems([new UiMenuItem("1", "New"), new UiMenuItem("2", "Open")]);
        var host = new TestHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(20, 20, 36, 36));
        session.AddRoot(root);

        BRenderList renderList = session.RenderFrame();
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), cmd => cmd.Text.Text == "⋮");
        Assert.False(menu.IsOpen);

        // Click trigger button
        Click(session, 30, 30);
        Assert.True(menu.IsOpen);
        Assert.False(menu.OverlayBounds.IsEmpty);

        // Click trigger again -> closes
        Click(session, 30, 30);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void Overflow_Mode_Supports_Custom_Glyph()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.Overflow,
            OverflowGlyph = "☰",
        };
        menu.SetItems([new UiMenuItem("1", "Item")]);
        var host = new TestHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(20, 20, 36, 36));
        session.AddRoot(root);

        BRenderList renderList = session.RenderFrame();
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.DrawText>(), cmd => cmd.Text.Text == "☰");
    }

    [Fact]
    public void Overflow_Mode_Clicking_Item_In_Popup_Invokes_And_Closes()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.Overflow,
            ItemHeight = 30,
            PopupWidth = 150,
        };
        UiMenuItem item1 = new("item1", "Item 1");
        UiMenuItem item2 = new("item2", "Item 2");
        menu.SetItems([item1, item2]);
        UiMenuItem? invokedItem = null;
        menu.ItemInvoked += (_, e) => invokedItem = e.Item;

        var host = new TestHost(new BSize(400, 300));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(20, 20, 36, 36));
        session.AddRoot(root);
        session.RenderFrame();

        // Click trigger to open
        Click(session, 30, 30);
        Assert.True(menu.IsOpen);

        // Re-render so popup rows are computed
        session.RenderFrame();

        // Origin is at (Bounds.Left, Bounds.Bottom) = (20, 56).
        // Item 0: [56, 86), Item 1: [86, 116). Click item 2 at (40, 100).
        Click(session, 40, 100);

        Assert.Same(item2, invokedItem);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void Drawer_Mode_Measures_With_PopupWidth_And_Renders_Vertical_List()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.Drawer,
            PopupWidth = 220,
            ItemHeight = 36,
        };
        menu.SetItems([new UiMenuItem("1", "Inbox"), new UiMenuItem("2", "Sent"), new UiMenuItem("3", "Drafts")]);
        menu.Measure(new BSize(600, 600));
        Assert.Equal(220, menu.DesiredSize.Width);
        Assert.Equal(3 * 36, menu.DesiredSize.Height);

        var host = new TestHost(new BSize(600, 600));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(0, 0, 220, 600));
        session.AddRoot(root);
        session.RenderFrame();

        Assert.Equal(3, menu.TopLevelBounds.Count);
        Assert.Equal(0, menu.TopLevelBounds[0].Left);
        Assert.Equal(220, menu.TopLevelBounds[0].Width);
        Assert.True(menu.TopLevelBounds[1].Top >= menu.TopLevelBounds[0].Bottom);
    }

    [Fact]
    public void Drawer_Mode_Leaf_Item_Click_Invokes_Directly()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.Drawer,
            ItemHeight = 40,
            PopupWidth = 200,
        };
        UiMenuItem item1 = new("1", "Profile");
        UiMenuItem item2 = new("2", "Settings");
        menu.SetItems([item1, item2]);
        UiMenuItem? invokedItem = null;
        menu.ItemInvoked += (_, e) => invokedItem = e.Item;

        var host = new TestHost(new BSize(400, 400));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(0, 0, 200, 400));
        session.AddRoot(root);
        session.RenderFrame();

        // Click on first item (y = 20)
        Click(session, 50, 20);
        Assert.Same(item1, invokedItem);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void Drawer_Mode_Item_With_Children_Opens_Submenu()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.Drawer,
            ItemHeight = 40,
            PopupWidth = 200,
        };
        UiMenuItem parentItem = new("parent", "Account");
        parentItem.Children.Add(new UiMenuItem("child", "Switch User"));
        menu.SetItems([parentItem]);

        var host = new TestHost(new BSize(400, 400));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(0, 0, 200, 400));
        session.AddRoot(root);
        session.RenderFrame();

        // Click on parentItem
        Click(session, 50, 20);
        Assert.True(menu.IsOpen);
        Assert.Equal(new[] { 0 }, menu.SelectedPath);
    }

    [Fact]
    public void BottomSheet_Mode_Renders_Backdrop_And_Full_Viewport_Overlay()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.BottomSheet,
        };
        menu.SetItems([new UiMenuItem("1", "Share"), new UiMenuItem("2", "Save")]);

        var host = new TestHost(new BSize(500, 700));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(menu);
        session.RenderFrame();

        Assert.False(menu.IsOpen);
        Assert.Equal(BRect.Empty, menu.OverlayBounds);

        menu.Open();
        Assert.True(menu.IsOpen);
        Assert.Equal(new BRect(0, 0, 500, 700), menu.OverlayBounds);

        BRenderList renderList = session.RenderFrame();
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.FillRect>(), cmd => cmd.Rect == new BRect(0, 0, 500, 700));
    }

    [Fact]
    public void BottomSheet_Mode_Clicking_Backdrop_Dismisses_Sheet()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.BottomSheet,
        };
        menu.SetItems([new UiMenuItem("1", "Option A"), new UiMenuItem("2", "Option B")]);

        var host = new TestHost(new BSize(500, 700));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(menu);
        session.RenderFrame();

        menu.Open();
        session.RenderFrame();
        Assert.True(menu.IsOpen);

        // Click near top of screen (y = 50), which is in backdrop area well above the bottom sheet
        Click(session, 250, 50);

        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void BottomSheet_Mode_Clicking_Row_Invokes_Item_And_Closes()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.BottomSheet,
            ItemHeight = 44,
        };
        UiMenuItem item1 = new("1", "Action 1");
        UiMenuItem item2 = new("2", "Action 2");
        menu.SetItems([item1, item2]);
        UiMenuItem? invokedItem = null;
        menu.ItemInvoked += (_, e) => invokedItem = e.Item;

        var host = new TestHost(new BSize(400, 600));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        session.AddRoot(menu);
        session.RenderFrame();

        menu.Open();
        session.RenderFrame();

        // sheet height = (2 * 44) + 24 (handle) + 16 = 88 + 40 = 128.
        // sheet top = 600 - 128 = 472.
        // handle is 24px -> items start at 472 + 24 = 496.
        // item 0 row: [496, 540)
        // item 1 row: [540, 584)
        // Click inside item 1 at y = 560
        Click(session, 200, 560);

        Assert.Same(item2, invokedItem);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void TabBar_Mode_Measures_With_Touch_Height_And_Partitions_Tabs()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.TabBar,
        };
        menu.SetItems([
            new UiMenuItem("1", "Home"),
            new UiMenuItem("2", "Search"),
            new UiMenuItem("3", "Notifications"),
            new UiMenuItem("4", "Profile")
        ]);

        menu.Measure(new BSize(400, 100));
        Assert.True(menu.DesiredSize.Height >= 48);

        var host = new TestHost(new BSize(400, 600));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(0, 550, 400, 50));
        session.AddRoot(root);
        session.RenderFrame();

        Assert.Equal(4, menu.TopLevelBounds.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(100, menu.TopLevelBounds[i].Width, precision: 1);
            Assert.Equal(i * 100, menu.TopLevelBounds[i].Left, precision: 1);
        }
    }

    [Fact]
    public void TabBar_Mode_Clicking_Tab_Invokes_Item_Immediately()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.TabBar,
        };
        UiMenuItem home = new("home", "Home");
        UiMenuItem search = new("search", "Search");
        menu.SetItems([home, search]);
        UiMenuItem? invokedItem = null;
        menu.ItemInvoked += (_, e) => invokedItem = e.Item;

        var host = new TestHost(new BSize(400, 600));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(0, 550, 400, 50));
        session.AddRoot(root);
        session.RenderFrame();

        // Click on search tab (x = 300, y = 575)
        Click(session, 300, 575);

        Assert.Same(search, invokedItem);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void TabBar_Mode_Tab_With_Children_Opens_Popup()
    {
        var menu = new StandardMenu
        {
            PresentationMode = UiMenuPresentationMode.TabBar,
        };
        UiMenuItem more = new("more", "More");
        more.Children.Add(new UiMenuItem("settings", "Settings"));
        menu.SetItems([more]);

        var host = new TestHost(new BSize(400, 600));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var root = new TestContainer(menu, new BRect(0, 550, 400, 50));
        session.AddRoot(root);
        session.RenderFrame();

        // Click on more tab
        Click(session, 200, 575);
        Assert.True(menu.IsOpen);
        Assert.Equal(new[] { 0 }, menu.SelectedPath);
    }

    private static InputEventHeader Header() =>
        new(InputDeviceId.FromOpaqueValue("test-device"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"), 1);

    private static UiInputEvent MouseDown(double x, double y, MouseButton button = MouseButton.Left) =>
        UiInputEvent.FromMouseButton(new MouseButtonEvent(
            Header(),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            button == MouseButton.Right ? MouseButtons.Right : MouseButtons.Left,
            button,
            MouseButtonTransition.Down,
            InputEventSource.Synthetic));

    private static UiInputEvent MouseUp(double x, double y, MouseButton button = MouseButton.Left) =>
        UiInputEvent.FromMouseButton(new MouseButtonEvent(
            Header(),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.None,
            button,
            MouseButtonTransition.Up,
            InputEventSource.Synthetic));

    private static bool Click(UiSession session, double x, double y)
    {
        bool down = session.DispatchInput(MouseDown(x, y));
        session.DispatchInput(MouseUp(x, y));
        return down;
    }

    private sealed class TestContainer : UiElement
    {
        private readonly UiElement _child;
        private readonly BRect _childBounds;

        public TestContainer(UiElement child, BRect childBounds)
        {
            _child = child;
            _childBounds = childBounds;
            AddChild(child);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _child.Measure(_childBounds.Size);
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            _child.Arrange(_childBounds);
        }
    }

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; } = viewportSize;

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
