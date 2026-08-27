using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Dialog.Standard;
using Broiler.UI.ScrollView.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

using FakeHostWindow = Broiler.UI.Standard.Tests.UiWindowBreakOutTests.FakeHostWindow;
using FakeWindowHost = Broiler.UI.Standard.Tests.UiWindowBreakOutTests.FakeWindowHost;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// Owner-drawn window chrome: when a window is responsible for its own title bar, how the bar is
/// laid out and hit-tested, and what its system buttons do to the native window behind it.
/// </summary>
public sealed class UiWindowChromeTests
{
    [Fact(Timeout = 600000)]
    public void A_Logical_Subwindow_Always_Draws_Its_Own_Title_Bar()
    {
        var host = new PlainHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow { Title = "Panel" };
        owner.OpenOwnedWindow(child, new BRect(10, 10, 200, 150));

        Assert.True(child.IsTitleBarVisible);
    }

    [Fact(Timeout = 600000)]
    public void A_Root_Window_Leaves_The_Title_Bar_To_A_Host_Without_The_Chrome_Capability()
    {
        var host = new PlainHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var window = new StandardWindow { Title = "Main" };
        session.AddRoot(window);

        Assert.False(window.IsTitleBarVisible);
    }

    [Fact(Timeout = 600000)]
    public void A_Root_Window_Draws_Its_Own_Title_Bar_When_The_Host_Suppresses_The_Frame()
    {
        var host = new FakeChromeHost { Chrome = UiHostWindowChrome.Owner };
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var window = new StandardWindow { Title = "Main" };
        session.AddRoot(window);

        Assert.True(window.IsTitleBarVisible);
        Assert.True(window.ShowsMinimizeButton);
        Assert.True(window.ShowsMaximizeButton);
        Assert.True(window.ShowsCloseButton);
    }

    [Fact(Timeout = 600000)]
    public void A_Broken_Out_Window_Draws_No_Title_Bar_Over_A_System_Chrome_Host()
    {
        var host = new FakeWindowHost { ChromeOverride = UiHostWindowChrome.System };
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow { Title = "Panel" };

        owner.OpenOwnedWindow(child);

        // The whole point: a host that keeps its native title bar must not get a second one
        // painted underneath it.
        Assert.True(child.IsBrokenOut);
        Assert.False(child.IsTitleBarVisible);
    }

    [Fact(Timeout = 600000)]
    public void A_Broken_Out_Window_Draws_Its_Own_Title_Bar_Over_A_Frameless_Host()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow { Title = "Panel" };

        owner.OpenOwnedWindow(child);

        Assert.True(child.IsTitleBarVisible);
    }

    [Fact(Timeout = 600000)]
    public void Chrome_Mode_None_Suppresses_The_Title_Bar_Entirely()
    {
        var host = new FakeChromeHost { Chrome = UiHostWindowChrome.Owner };
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var window = new StandardWindow { Title = "Main", Chrome = UiWindowChrome.None };
        session.AddRoot(window);

        Assert.False(window.IsTitleBarVisible);
        Assert.False(UiWindowChromeLayout.Create(window, new BRect(0, 0, 400, 300), UiWindowChromeMetrics.Default).IsVisible);
    }

    [Fact(Timeout = 600000)]
    public void Chrome_Layout_Packs_System_Buttons_Against_The_Right_Edge()
    {
        var host = new FakeChromeHost { Chrome = UiHostWindowChrome.Owner };
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var window = new StandardWindow { Title = "Main" };
        session.AddRoot(window);

        var metrics = new UiWindowChromeMetrics(32, 10, 44, 16);
        UiWindowChromeLayout layout = UiWindowChromeLayout.Create(window, new BRect(0, 0, 400, 300), metrics);

        Assert.True(layout.IsVisible);
        Assert.Equal(new BRect(0, 0, 400, 32), layout.TitleBar);
        Assert.Equal(new BRect(356, 0, 44, 32), layout.CloseButton);
        Assert.Equal(new BRect(312, 0, 44, 32), layout.MaximizeButton);
        Assert.Equal(new BRect(268, 0, 44, 32), layout.MinimizeButton);
        Assert.Equal(new BRect(0, 32, 400, 268), layout.Content);

        // No icon set, so the title starts at the padding inset and runs to the first button.
        Assert.Equal(10, layout.Title.Left);
        Assert.Equal(268, layout.Title.Right);
    }

    [Fact(Timeout = 600000)]
    public void Chrome_Layout_Reserves_Space_For_The_Window_Icon()
    {
        var host = new FakeChromeHost { Chrome = UiHostWindowChrome.Owner };
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var window = new StandardWindow
        {
            Title = "Main",
            Icon = new UiWindowIcon(BImageHandle.FromId(7, new BSize(16, 16))),
        };
        session.AddRoot(window);

        UiWindowChromeLayout layout = UiWindowChromeLayout.Create(
            window,
            new BRect(0, 0, 400, 300),
            new UiWindowChromeMetrics(32, 10, 44, 16));

        Assert.Equal(new BRect(10, 8, 16, 16), layout.Icon);
        Assert.Equal(31, layout.Title.Left);
    }

    [Fact(Timeout = 600000)]
    public void Chrome_Hit_Test_Prefers_Buttons_Over_The_Title_Bar()
    {
        var host = new FakeChromeHost { Chrome = UiHostWindowChrome.Owner };
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var window = new StandardWindow { Title = "Main" };
        session.AddRoot(window);

        UiWindowChromeLayout layout = UiWindowChromeLayout.Create(
            window,
            new BRect(0, 0, 400, 300),
            new UiWindowChromeMetrics(32, 10, 44, 16));

        Assert.Equal(UiWindowChromePart.Close, layout.HitTest(new BPoint(378, 16)));
        Assert.Equal(UiWindowChromePart.Maximize, layout.HitTest(new BPoint(334, 16)));
        Assert.Equal(UiWindowChromePart.Minimize, layout.HitTest(new BPoint(290, 16)));
        Assert.Equal(UiWindowChromePart.TitleBar, layout.HitTest(new BPoint(120, 16)));
        Assert.Equal(UiWindowChromePart.None, layout.HitTest(new BPoint(120, 100)));
    }

    [Fact(Timeout = 600000)]
    public void Close_Button_Closes_The_Window()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, _) = CreateChromeWindow();
        using (session)
        {
            ClickChrome(window, window.ChromeLayout.CloseButton);

            Assert.True(window.IsClosed);
            // Closing the logical window also asks the host to close the native one behind it.
            Assert.Equal(1, host.CloseRequests);
        }
    }

    [Fact(Timeout = 600000)]
    public void Maximize_Button_Toggles_The_Native_Window_State()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, _) = CreateChromeWindow();
        using (session)
        {
            ClickChrome(window, window.ChromeLayout.MaximizeButton);

            Assert.Equal(UiHostWindowState.Maximized, host.WindowState);
            Assert.Equal(UiWindowState.Maximized, window.State);

            ClickChrome(window, window.ChromeLayout.MaximizeButton);

            Assert.Equal(UiHostWindowState.Normal, host.WindowState);
            Assert.Equal(UiWindowState.Normal, window.State);
        }
    }

    [Fact(Timeout = 600000)]
    public void Minimize_Button_Minimizes_The_Native_Window()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, _) = CreateChromeWindow();
        using (session)
        {
            ClickChrome(window, window.ChromeLayout.MinimizeButton);

            Assert.Equal(UiHostWindowState.Minimized, host.WindowState);
        }
    }

    [Fact(Timeout = 600000)]
    public void A_Press_Outside_The_Button_It_Started_On_Runs_No_Command()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, _) = CreateChromeWindow();
        using (session)
        {
            BRect close = window.ChromeLayout.CloseButton;
            window.DispatchInput(MouseButton(close.Left + 4, close.Top + 4, MouseButtonTransition.Down));
            window.DispatchInput(MouseMove(20, 200));
            window.DispatchInput(MouseButton(20, 200, MouseButtonTransition.Up));

            Assert.False(window.IsClosed);
            Assert.Equal(0, host.CloseRequests);
        }
    }

    [Fact(Timeout = 600000)]
    public void A_Title_Bar_Press_Hands_The_Drag_To_The_Window_Manager()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, _) = CreateChromeWindow();
        using (session)
        {
            Assert.True(window.DispatchInput(MouseButton(120, 16, MouseButtonTransition.Down)));

            Assert.Equal(1, host.MoveDrags);
        }
    }

    [Fact(Timeout = 600000)]
    public void A_Second_Title_Bar_Press_Within_The_Double_Click_Window_Maximizes()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, ManualClock clock) = CreateChromeWindow();
        using (session)
        {
            window.DispatchInput(MouseButton(120, 16, MouseButtonTransition.Down));
            window.DispatchInput(MouseButton(120, 16, MouseButtonTransition.Up));
            clock.Advance(TimeSpan.FromMilliseconds(120));
            window.DispatchInput(MouseButton(120, 16, MouseButtonTransition.Down));

            Assert.Equal(UiHostWindowState.Maximized, host.WindowState);
            Assert.Equal(1, host.MoveDrags);
        }
    }

    [Fact(Timeout = 600000)]
    public void A_Slow_Second_Title_Bar_Press_Starts_Another_Drag_Instead()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, ManualClock clock) = CreateChromeWindow();
        using (session)
        {
            window.DispatchInput(MouseButton(120, 16, MouseButtonTransition.Down));
            window.DispatchInput(MouseButton(120, 16, MouseButtonTransition.Up));
            clock.Advance(TimeSpan.FromSeconds(2));
            window.DispatchInput(MouseButton(120, 16, MouseButtonTransition.Down));

            Assert.Equal(UiHostWindowState.Normal, host.WindowState);
            Assert.Equal(2, host.MoveDrags);
        }
    }

    [Fact(Timeout = 600000)]
    public void A_State_Change_Made_By_The_Window_Manager_Is_Adopted_Without_Echoing_Back()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, _) = CreateChromeWindow();
        using (session)
        {
            host.RaiseExternalWindowState(UiHostWindowState.Maximized);

            Assert.Equal(UiWindowState.Maximized, window.State);
            Assert.Equal(0, host.StateCommands);
        }
    }

    [Fact(Timeout = 600000)]
    public void Title_And_Icon_Reach_The_Chrome_Host()
    {
        (UiSession session, StandardWindow window, FakeChromeHost host, _) = CreateChromeWindow();
        using (session)
        {
            var pixels = new BPixelBuffer(2, 2, new byte[16]);
            window.Title = "Renamed";
            window.Icon = new UiWindowIcon(BImageHandle.Invalid, pixels);

            Assert.Equal("Renamed", host.Title);
            Assert.Same(pixels, host.Icon);
        }
    }

    [Fact(Timeout = 600000)]
    public void A_Broken_Out_Window_Adopts_The_Title_And_Icon_It_Already_Had()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var pixels = new BPixelBuffer(2, 2, new byte[16]);
        var child = new StandardWindow
        {
            Title = "Panel",
            Icon = new UiWindowIcon(BImageHandle.Invalid, pixels),
        };

        owner.OpenOwnedWindow(child);

        FakeHostWindow created = Assert.Single(host.Created);
        Assert.Equal("Panel", created.Title);
        Assert.Same(pixels, created.Icon);
    }

    [Fact(Timeout = 600000)]
    public void A_Dialog_Reserves_No_Title_Bar_Height_When_The_Platform_Draws_One()
    {
        var host = new FakeWindowHost { ChromeOverride = UiHostWindowChrome.System };
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardDialog { Title = "Dialog", Padding = 10, TitleBarHeight = 30 };
        var content = new StandardScrollView();
        dialog.AddChild(content);
        _ = dialog.ShowModal(owner, new BRect(0, 0, 300, 200));

        dialog.Measure(new BSize(300, 200));
        dialog.Arrange(new BRect(0, 0, 300, 200));

        Assert.False(dialog.IsTitleBarVisible);
        Assert.False(dialog.ChromeLayout.IsVisible);

        // Content starts at the padding, not below a title bar that is not drawn.
        Assert.Equal(10, content.Bounds.Top);
    }

    [Fact(Timeout = 600000)]
    public void A_Dialog_Reserves_The_Title_Bar_It_Draws_Itself()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardDialog { Title = "Dialog", Padding = 10, TitleBarHeight = 30 };
        var content = new StandardScrollView();
        dialog.AddChild(content);
        _ = dialog.ShowModal(owner, new BRect(0, 0, 300, 200));

        dialog.Measure(new BSize(300, 200));
        dialog.Arrange(new BRect(0, 0, 300, 200));

        Assert.True(dialog.ChromeLayout.IsVisible);
        Assert.Equal(40, content.Bounds.Top);
    }

    [Fact(Timeout = 600000)]
    public void A_Dialog_Offers_Only_A_Close_Button_By_Default()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardDialog { Title = "Dialog" };
        _ = dialog.ShowModal(owner, new BRect(0, 0, 300, 200));

        Assert.True(dialog.IsTitleBarVisible);
        Assert.True(dialog.ShowsCloseButton);
        Assert.False(dialog.ShowsMinimizeButton);
        Assert.False(dialog.ShowsMaximizeButton);
    }

    private static (UiSession Session, StandardWindow Window, FakeChromeHost Host, ManualClock Clock) CreateChromeWindow()
    {
        var host = new FakeChromeHost { Chrome = UiHostWindowChrome.Owner };
        // A clock the test drives: double-click detection reads elapsed time, and a wall clock
        // would make synthesized presses land in the same millisecond.
        var clock = new ManualClock();
        UiSession session = new StandardUiSessionBuilder().WithClock(clock).Build(host);
        var window = new StandardWindow { Title = "Main" };
        session.AddRoot(window);
        window.Measure(new BSize(400, 300));
        window.Arrange(new BRect(0, 0, 400, 300));
        return (session, window, host, clock);
    }

    private sealed class ManualClock : IUiClock
    {
        public UiTimestamp Now { get; private set; } = new(TimeSpan.FromSeconds(1));

        public void Advance(TimeSpan elapsed) => Now = new UiTimestamp(Now.Elapsed + elapsed);
    }

    private static void ClickChrome(StandardWindow window, BRect button)
    {
        double x = button.Left + (button.Width / 2);
        double y = button.Top + (button.Height / 2);
        window.DispatchInput(MouseButton(x, y, MouseButtonTransition.Down));
        window.DispatchInput(MouseButton(x, y, MouseButtonTransition.Up));
    }

    private static UiInputEvent MouseButton(double x, double y, MouseButtonTransition transition) =>
        UiInputEvent.FromMouseButton(new MouseButtonEvent(
            CreateHeader(),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.Left,
            Broiler.Input.Mouse.MouseButton.Left,
            transition,
            InputEventSource.Synthetic));

    private static UiInputEvent MouseMove(double x, double y) =>
        UiInputEvent.FromMouseMove(new MouseMoveEvent(
            CreateHeader(),
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.None,
            InputEventSource.Synthetic));

    private static InputEventHeader CreateHeader() =>
        new(
            InputDeviceId.FromOpaqueValue("mouse:chrome-test"),
            new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"),
            1);

    private sealed class PlainHost : IUiHost
    {
        public BSize ViewportSize => new(640, 480);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }

    /// <summary>A primary host that also drives its native window's chrome — the frameless case.</summary>
    private sealed class FakeChromeHost : IUiHost, IUiWindowChromeHost
    {
        public BSize ViewportSize => new(400, 300);

        public double Scale => 1.0;

        public UiHostWindowChrome Chrome { get; set; } = UiHostWindowChrome.Owner;

        public bool IsResizable { get; set; } = true;

        public UiHostWindowState WindowState { get; private set; }

        public string Title { get; private set; } = string.Empty;

        public BPixelBuffer? Icon { get; private set; }

        public int MoveDrags { get; private set; }

        public int CloseRequests { get; private set; }

        /// <summary>How often the framework pushed a state down, as opposed to adopting one.</summary>
        public int StateCommands { get; private set; }

        public List<UiWindowEdge> ResizeDrags { get; } = [];

        public event EventHandler? WindowStateChanged;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public void SetWindowState(UiHostWindowState state)
        {
            StateCommands++;
            if (WindowState == state)
                return;

            WindowState = state;
            WindowStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseExternalWindowState(UiHostWindowState state)
        {
            WindowState = state;
            WindowStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetTitle(string title) => Title = title;

        public void SetIcon(BPixelBuffer? icon) => Icon = icon;

        public void RequestClose() => CloseRequests++;

        public void BeginMoveDrag() => MoveDrags++;

        public void BeginResizeDrag(UiWindowEdge edge) => ResizeDrags.Add(edge);
    }
}
