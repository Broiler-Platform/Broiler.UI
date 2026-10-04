using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Windowing;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.UI.Button.Standard;
using Broiler.UI.TabView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The tab strip's arrow, Home and End keys switch tabs only while the strip itself has focus. A key
/// a control inside a tab leaves unhandled bubbles up through the tab view and must not move the user
/// to another tab.
/// </summary>
public sealed class TabViewKeyboardTests
{
    private static readonly (string Name, int Code)[] StripKeys =
        [("Right", BVirtualKey.Right), ("Left", BVirtualKey.Left), ("Home", BVirtualKey.Home), ("End", BVirtualKey.End)];

    [Fact]
    public void TheStripsKeysMoveBetweenTabsWhileTheStripHasFocus()
    {
        using var scene = Scene.Create();
        scene.Session.SetFocus(scene.Tabs);

        Assert.True(scene.Press("Right", BVirtualKey.Right));
        Assert.Equal(1, scene.Tabs.SelectedIndex);
        Assert.True(scene.Press("Right", BVirtualKey.Right));
        Assert.True(scene.Press("Right", BVirtualKey.Right));
        Assert.Equal(0, scene.Tabs.SelectedIndex);
        Assert.True(scene.Press("Left", BVirtualKey.Left));
        Assert.Equal(2, scene.Tabs.SelectedIndex);
        Assert.True(scene.Press("Home", BVirtualKey.Home));
        Assert.Equal(0, scene.Tabs.SelectedIndex);
        Assert.True(scene.Press("End", BVirtualKey.End));
        Assert.Equal(2, scene.Tabs.SelectedIndex);
        Assert.Same(scene.Tabs, scene.Session.FocusedElement);
    }

    [Fact]
    public void AKeyAControlInsideATabLeavesUnhandledDoesNotSwitchTabs()
    {
        using var scene = Scene.Create();
        StandardButton button = scene.Buttons[1];
        scene.Tabs.SelectedIndex = 1;
        scene.Render();
        scene.Session.SetFocus(button);

        foreach ((string name, int code) in StripKeys)
        {
            Assert.False(scene.Press(name, code), name);
            Assert.Equal(1, scene.Tabs.SelectedIndex);
            Assert.Same(button, scene.Session.FocusedElement);
        }
    }

    [Fact]
    public void AKeyThatReachesTheTabViewWithNothingFocusedDoesNotSwitchTabs()
    {
        // With no focus a key goes to whatever lies under its position, here the strip.
        using var scene = Scene.Create();
        Assert.Null(scene.Session.FocusedElement);

        foreach ((string name, int code) in StripKeys)
        {
            Assert.False(scene.Press(name, code), name);
            Assert.Equal(0, scene.Tabs.SelectedIndex);
        }
    }

    [Fact]
    public void AnInnerTabViewsUnhandledKeyDoesNotSwitchTheOuterTabs()
    {
        // End on an inner strip already at its last tab changes nothing there and is left unhandled.
        var inner = new StandardTabView();
        inner.AddTab("first", "First", new StandardButton { Text = "One" });
        inner.AddTab("second", "Second", new StandardButton { Text = "Two" });
        var outer = new StandardTabView();
        outer.AddTab("documents", "Documents", inner);
        outer.AddTab("settings", "Settings", new StandardButton { Text = "Three" });
        using var scene = Scene.Create(outer);
        inner.SelectedIndex = 1;
        scene.Render();
        scene.Session.SetFocus(inner);

        Assert.False(scene.Press("End", BVirtualKey.End));
        Assert.Equal(0, outer.SelectedIndex);
        Assert.True(scene.Press("Left", BVirtualKey.Left));
        Assert.Equal(0, inner.SelectedIndex);
        Assert.Equal(0, outer.SelectedIndex);
        Assert.Same(inner, scene.Session.FocusedElement);
    }

    private sealed class Scene : IDisposable
    {
        private Scene(StandardTabView tabs, UiSession session)
        {
            Tabs = tabs;
            Session = session;
        }

        public StandardTabView Tabs { get; }
        public UiSession Session { get; }
        public List<StandardButton> Buttons { get; } = [];

        public static Scene Create()
        {
            var tabs = new StandardTabView();
            var buttons = new List<StandardButton>();
            foreach (string name in new[] { "Inbox", "Account", "Settings" })
            {
                var button = new StandardButton { Text = name + " action" };
                buttons.Add(button);
                tabs.AddTab(name.ToLowerInvariant(), name, button);
            }

            Scene scene = Create(tabs);
            scene.Buttons.AddRange(buttons);
            return scene;
        }

        public static Scene Create(StandardTabView tabs)
        {
            UiSession session = new StandardUiSessionBuilder()
                .WithDispatcher(new ImmediateUiDispatcher())
                .Build(new Host(new BSize(480, 240)));
            session.AddRoot(tabs);
            var scene = new Scene(tabs, session);
            scene.Render();
            return scene;
        }

        public void Render() => Session.RenderFrame();

        public bool Press(string name, int nativeKeyCode) =>
            new StandardInputRoute(Session).Dispatch(new KeyboardKeyEvent(
                new InputEventHeader(InputDeviceId.FromOpaqueValue("keyboard"), new InputTimestamp(1, TimeSpan.TicksPerSecond, "tabs"), 1),
                KeyboardKey.FromName(name),
                KeyboardKeyTransition.Down,
                KeyboardModifierState.None,
                nativeKeyCode,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

        public void Dispose() => Session.Dispose();
    }

    private sealed class Host(BSize size) : IUiHost
    {
        public BSize ViewportSize { get; } = size;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
