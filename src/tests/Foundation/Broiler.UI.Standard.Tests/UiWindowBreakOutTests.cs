using Broiler.Graphics;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Dialog;
using Broiler.UI.Dialog.Standard;
using Broiler.UI.FileDialog.Standard;
using Broiler.UI.Tooltip.Standard;
using Broiler.UI.Window;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class UiWindowBreakOutTests
{
    [Fact(Timeout = 600000)]
    public void CanBreakOut_Is_False_Without_Window_Host_Capability()
    {
        var host = new PlainHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow();
        owner.OpenOwnedWindow(child);

        // Automatic break-out is the default, but a host without the capability leaves the window
        // logical rather than failing to open it.
        Assert.False(child.CanBreakOut);
        Assert.False(child.BreakOut());
        Assert.False(child.IsBrokenOut);
        Assert.Contains(child, owner.OwnedWindows);
    }

    [Fact(Timeout = 600000)]
    public void CanBreakOut_Is_False_For_A_Top_Level_Window()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var top = new StandardWindow();
        session.AddRoot(top);

        Assert.Null(top.Owner);
        Assert.False(top.CanBreakOut);
    }

    [Fact(Timeout = 600000)]
    public void Owned_Window_Breaks_Out_Automatically_When_The_Host_Supports_It()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow { Title = "Main" };
        session.AddRoot(owner);
        var child = new StandardWindow { Title = "Panel" };

        owner.OpenOwnedWindow(child, new BRect(10, 10, 200, 150));

        Assert.True(child.IsBrokenOut);
        Assert.Null(child.Owner);
        Assert.Equal(UiWindowKind.TopLevel, child.Kind);
        Assert.DoesNotContain(child, owner.OwnedWindows);

        FakeHostWindow created = Assert.Single(host.Created);
        Assert.Equal("Panel", created.Title);
        Assert.False(created.IsModal);
        Assert.NotNull(created.BoundSession);
        Assert.Same(child, Assert.Single(created.BoundSession!.Roots));
        Assert.NotSame(session, created.BoundSession);
        Assert.True(created.Activated);

        // Break-out is one-way for the window's lifetime.
        Assert.False(child.CanBreakOut);
        Assert.False(child.BreakOut());
    }

    [Fact(Timeout = 600000)]
    public void Break_Out_Asks_The_Host_For_Owner_Drawn_Chrome()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        owner.OpenOwnedWindow(new StandardWindow { Title = "Panel" });

        Assert.Equal(UiHostWindowChrome.Owner, Assert.Single(host.Created).Request.Chrome);
    }

    [Fact(Timeout = 600000)]
    public void Manual_Mode_Keeps_The_Window_Logical_Until_BreakOut_Is_Called()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow { BreakOutMode = UiWindowBreakOutMode.Manual };

        owner.OpenOwnedWindow(child, new BRect(10, 10, 200, 150));

        Assert.False(child.IsBrokenOut);
        Assert.Empty(host.Created);
        Assert.Contains(child, owner.OwnedWindows);

        Assert.True(child.CanBreakOut);
        Assert.True(child.BreakOut());
        Assert.True(child.IsBrokenOut);
        Assert.Single(host.Created);
    }

    [Fact(Timeout = 600000)]
    public void Tooltips_Never_Break_Out_Automatically()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var tooltip = new StandardTooltip { Text = "Hint" };

        owner.OpenOwnedWindow(tooltip, new BRect(0, 0, 1, 1));

        Assert.False(tooltip.IsBrokenOut);
        Assert.Empty(host.Created);
        Assert.Contains(tooltip, owner.OwnedWindows);
    }

    [Fact(Timeout = 600000)]
    public void Broken_Out_Window_Reuses_Origin_Session_Services()
    {
        var host = new FakeWindowHost();
        var dispatcher = new InlineDispatcher();
        var clock = new ManualClock();
        using UiSession session = new StandardUiSessionBuilder()
            .WithDispatcher(dispatcher)
            .WithClock(clock)
            .Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        owner.OpenOwnedWindow(new StandardWindow());

        UiSession hosted = host.Created[0].BoundSession!;
        Assert.Same(dispatcher, hosted.Dispatcher);
        Assert.Same(clock, hosted.Clock);
        Assert.Same(session.Factories, hosted.Factories);
    }

    [Fact(Timeout = 600000)]
    public void Modal_Dialog_Breaks_Out_And_Blocks_Origin_Until_Completed()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardDialog { Title = "Modal" };
        Task<UiDialogResult> result = dialog.ShowModal(owner, new BRect(20, 20, 200, 120));

        Assert.True(dialog.IsBrokenOut);
        Assert.True(host.Created[0].IsModal);
        Assert.True(session.IsBlockedByExternalModal);
        Assert.False(session.DispatchInput(CreateMouseDown(5, 5)));
        Assert.DoesNotContain(dialog, session.ModalElements);

        Assert.True(dialog.Accept("ok"));

        Assert.False(session.IsBlockedByExternalModal);
        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(UiDialogResultKind.Accepted, dialog.CompletedResult.Kind);
        Assert.True(host.Created[0].IsDisposed);
    }

    [Fact(Timeout = 600000)]
    public void Modeless_Dialog_Break_Out_Does_Not_Block_Origin()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardDialog { Title = "Modeless" };
        _ = dialog.ShowModeless(owner, new BRect(20, 20, 200, 120));

        Assert.True(dialog.IsBrokenOut);
        Assert.False(host.Created[0].IsModal);
        Assert.False(session.IsBlockedByExternalModal);
    }

    [Fact(Timeout = 600000)]
    public void Modal_Dialog_Stays_Logical_In_Manual_Mode()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardDialog { Title = "Modal", BreakOutMode = UiWindowBreakOutMode.Manual };
        _ = dialog.ShowModal(owner, new BRect(20, 20, 200, 120));

        Assert.False(dialog.IsBrokenOut);
        Assert.Empty(host.Created);
        Assert.Contains(dialog, session.ModalElements);
    }

    [Fact(Timeout = 600000)]
    public void Host_Window_Close_Request_Closes_And_Disposes_Everything()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var dialog = new StandardDialog();
        _ = dialog.ShowModal(owner, new BRect(20, 20, 200, 120));
        Assert.True(dialog.IsBrokenOut);

        FakeHostWindow created = host.Created[0];
        UiSession hosted = created.BoundSession!;

        created.RaiseCloseRequested();

        Assert.True(dialog.IsClosed);
        Assert.True(dialog.IsDisposed);
        Assert.True(created.IsDisposed);
        Assert.True(hosted.IsDisposed);
        Assert.False(session.IsBlockedByExternalModal);
    }

    [Fact(Timeout = 600000)]
    public void Closing_Broken_Out_Window_Programmatically_Tears_Down_Once()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var child = new StandardWindow();
        owner.OpenOwnedWindow(child);
        Assert.True(child.IsBrokenOut);

        FakeHostWindow created = host.Created[0];
        UiSession hosted = created.BoundSession!;

        Assert.True(child.Close());
        Assert.True(created.IsDisposed);
        Assert.True(hosted.IsDisposed);
        Assert.Equal(1, created.DisposeCount);
    }

    [Fact(Timeout = 600000)]
    public void A_Broken_Out_Dialog_Moves_Through_The_Window_Manager()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        // A dialog that grips its own title bar rather than driving UiWindowChromeController -
        // which is what the file and font dialogs do.
        var dialog = new StandardFileDialog { Title = "Open" };
        _ = dialog.ShowOpenModal(owner, new BRect(20, 20, 300, 200));
        Assert.True(dialog.IsBrokenOut);

        dialog.Measure(new BSize(300, 200));
        dialog.Arrange(new BRect(0, 0, 300, 200));
        BRect before = dialog.Placement;

        Assert.True(dialog.DispatchInput(CreateMouseDown(150, 17)));

        // It has no owner left to be placed against, so the drag has to reach the window manager;
        // simulating it from pointer deltas moves nothing at all.
        Assert.Equal(1, host.Created[0].MoveDrags);

        dialog.DispatchInput(CreateMouseMove(210, 92));
        Assert.Equal(before, dialog.Placement);
    }

    [Fact(Timeout = 600000)]
    public void A_Logical_Dialog_Still_Moves_Itself_By_Placement()
    {
        var host = new FakeWindowHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardFileDialog { Title = "Open", BreakOutMode = UiWindowBreakOutMode.Manual };
        _ = dialog.ShowOpenModal(owner, new BRect(20, 20, 300, 200));
        Assert.False(dialog.IsBrokenOut);

        owner.Measure(new BSize(640, 480));
        owner.Arrange(new BRect(0, 0, 640, 480));

        dialog.DispatchInput(CreateMouseDown(150, 37));
        dialog.DispatchInput(CreateMouseMove(190, 77));

        Assert.Empty(host.Created);
        Assert.Equal(new BRect(60, 60, 300, 200), dialog.Placement);
    }

    private static UiInputEvent CreateMouseMove(double x, double y)
    {
        var header = new InputEventHeader(
            InputDeviceId.FromOpaqueValue("mouse:test"),
            new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"),
            1);

        return UiInputEvent.FromMouseMove(new MouseMoveEvent(
            header,
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.Left,
            InputEventSource.Synthetic));
    }

    private static UiInputEvent CreateMouseDown(double x, double y)
    {
        var header = new InputEventHeader(
            InputDeviceId.FromOpaqueValue("mouse:test"),
            new InputTimestamp(1, TimeSpan.TicksPerSecond, "test"),
            1);

        return UiInputEvent.FromMouseButton(new MouseButtonEvent(
            header,
            InputPoint.ClientDeviceIndependentPixels(x, y),
            MouseButtons.Left,
            MouseButton.Left,
            MouseButtonTransition.Down,
            InputEventSource.Synthetic));
    }

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

    internal sealed class FakeWindowHost : IUiHost, IUiWindowHost
    {
        public List<FakeHostWindow> Created { get; } = [];

        /// <summary>Stands in for a platform that cannot suppress its own window frame.</summary>
        public UiHostWindowChrome? ChromeOverride { get; set; }

        public BSize ViewportSize => new(800, 600);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public IUiHostWindow CreateHostWindow(UiHostWindowRequest request)
        {
            var window = new FakeHostWindow(request) { Chrome = ChromeOverride ?? request.Chrome };
            Created.Add(window);
            return window;
        }
    }

    internal sealed class FakeHostWindow : IUiHostWindow, IUiWindowChromeHost
    {
        public FakeHostWindow(UiHostWindowRequest request)
        {
            Request = request;
            Title = request.Title;
            Placement = request.Placement;
            IsModal = request.IsModal;
            Chrome = request.Chrome;
        }

        public UiHostWindowRequest Request { get; }

        public string Title { get; private set; }

        public BRect Placement { get; }

        public bool IsModal { get; }

        public UiSession? BoundSession { get; private set; }

        public bool Activated { get; private set; }

        public bool IsDisposed { get; private set; }

        public int DisposeCount { get; private set; }

        public BPixelBuffer? Icon { get; private set; }

        public List<UiWindowEdge> ResizeDrags { get; } = [];

        public int MoveDrags { get; private set; }

        public int CloseRequests { get; private set; }

        public event EventHandler? CloseRequested;

        public event EventHandler? WindowStateChanged;

        public BSize ViewportSize => new(400, 300);

        public double Scale => 1.0;

        public UiHostWindowChrome Chrome { get; set; }

        public bool IsResizable { get; set; } = true;

        public UiHostWindowState WindowState { get; private set; }

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public void Bind(UiSession session) => BoundSession = session;

        public void SetTitle(string title) => Title = title;

        public void SetIcon(BPixelBuffer? icon) => Icon = icon;

        public void Activate() => Activated = true;

        public void SetWindowState(UiHostWindowState state)
        {
            if (WindowState == state)
                return;

            WindowState = state;
            WindowStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Simulates the window manager changing the state behind the framework's back.</summary>
        public void RaiseExternalWindowState(UiHostWindowState state)
        {
            WindowState = state;
            WindowStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RequestClose()
        {
            CloseRequests++;
            RaiseCloseRequested();
        }

        public void BeginMoveDrag() => MoveDrags++;

        public void BeginResizeDrag(UiWindowEdge edge) => ResizeDrags.Add(edge);

        public void RaiseCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            DisposeCount++;
            IsDisposed = true;
        }
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action callback) => callback();
    }

    private sealed class ManualClock : IUiClock
    {
        public UiTimestamp Now { get; private set; }

        public void Advance(TimeSpan elapsed) => Now = new UiTimestamp(Now.Elapsed + elapsed);
    }
}
