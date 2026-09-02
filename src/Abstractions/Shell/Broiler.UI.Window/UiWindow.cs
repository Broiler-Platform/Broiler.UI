using System;
using System.Collections.Generic;
using Broiler.Graphics;

namespace Broiler.UI.Window;

public abstract class UiWindow : UiElement
{
    private readonly List<UiWindow> _ownedWindows = [];
    private string _title = string.Empty;
    private UiWindowState _state;
    private BRect _placement = BRect.Empty;
    private UiViewportBinding _viewportBinding;
    private IUiHostWindow? _hostWindow;
    private UiSession? _hostedSession;
    private IUiWindowChromeHost? _subscribedChromeHost;
    private UiWindowIcon? _icon;
    private UiWindowChrome _chrome = UiWindowChrome.Auto;
    private bool _canMinimize = true;
    private bool _canMaximize = true;
    private bool _canResize = true;
    private bool _canClose = true;
    private bool _isActive;
    private bool _isClosed;
    private bool _isBrokenOut;
    private bool _isReparenting;
    private bool _isTearingDownBreakOut;
    private bool _isSyncingHostState;

    public event EventHandler? Activated;

    public event EventHandler? Deactivated;

    public event EventHandler<UiWindowClosingEventArgs>? Closing;

    public event EventHandler<UiWindowClosedEventArgs>? Closed;

    public string Title
    {
        get => _title;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_title, value))
                return;

            _title = value;
            ChromeHost?.SetTitle(value);
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// The window icon: drawn in owner-drawn chrome, and — when it carries native pixels — used
    /// for the taskbar and Alt+Tab entry of the native window this one is hosted in.
    /// </summary>
    public UiWindowIcon? Icon
    {
        get => _icon;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_icon, value))
                return;

            _icon = value;
            ChromeHost?.SetIcon(value?.NativePixels);
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// Who draws this window's title bar and system buttons. <see cref="UiWindowChrome.Auto"/>
    /// resolves per host, so the same window renders one title bar whether it is a logical
    /// subwindow, broken out into a frameless native window, or hosted by a window manager that
    /// insists on drawing its own.
    /// </summary>
    public UiWindowChrome Chrome
    {
        get => _chrome;
        set
        {
            ThrowIfDisposed();
            if (_chrome == value)
                return;

            _chrome = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// Whether this window draws its own title bar. False when the platform draws one, so a
    /// broken-out window never stacks an owner-drawn bar on top of a native one.
    /// </summary>
    public bool IsTitleBarVisible => _chrome switch
    {
        UiWindowChrome.None => false,
        UiWindowChrome.Owner => true,
        // A logical subwindow is drawn entirely by the framework, so it always owns its chrome.
        // A root window only does when its host says the platform frame is suppressed.
        _ => Parent is not null || ChromeHost?.Chrome == UiHostWindowChrome.Owner,
    };

    /// <summary>Whether the window offers a minimize button. Only meaningful with a native host.</summary>
    public bool CanMinimize
    {
        get => _canMinimize;
        set => SetChromeFlag(ref _canMinimize, value);
    }

    /// <summary>Whether the window offers a maximize/restore button.</summary>
    public bool CanMaximize
    {
        get => _canMaximize;
        set => SetChromeFlag(ref _canMaximize, value);
    }

    /// <summary>
    /// Whether the user may resize the window by dragging its edges.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="CanMaximize"/>, because the two are separate wishes: a dialog
    /// listing a folder is worth stretching without being worth parking over the whole screen.
    /// Maximizing still needs a resizable frame, so a window that refuses this shows no maximize
    /// button either — <see cref="ShowsMaximizeButton"/> asks the host, and the host is only
    /// resizable when this said so.
    ///
    /// A break-out asks its host window for this once, when the native window is created, so it
    /// has to be set before the window is shown; a fixed-size native window cannot grow one later.
    /// </remarks>
    public bool CanResize
    {
        get => _canResize;
        set => SetChromeFlag(ref _canResize, value);
    }

    /// <summary>Whether the window offers a close button.</summary>
    public bool CanClose
    {
        get => _canClose;
        set => SetChromeFlag(ref _canClose, value);
    }

    /// <summary>Whether owner-drawn chrome should paint a minimize button.</summary>
    public bool ShowsMinimizeButton => IsTitleBarVisible && _canMinimize && ChromeHost is not null;

    /// <summary>Whether owner-drawn chrome should paint a maximize/restore button.</summary>
    public bool ShowsMaximizeButton => IsTitleBarVisible && _canMaximize && ChromeHost is { IsResizable: true };

    /// <summary>Whether owner-drawn chrome should paint a close button.</summary>
    public bool ShowsCloseButton => IsTitleBarVisible && _canClose;

    /// <summary>
    /// The host capability that drives the native window behind this one, when this window is the
    /// root of a session bound to a chrome-capable host. Null for a logical subwindow, which the
    /// framework moves and closes itself.
    /// </summary>
    protected IUiWindowChromeHost? ChromeHost =>
        Parent is null && !IsDisposed && Session?.Host is IUiWindowChromeHost chrome ? chrome : null;

    public UiWindowKind Kind { get; private set; } = UiWindowKind.TopLevel;

    public UiWindowState State
    {
        get => _state;
        set
        {
            ThrowIfDisposed();
            if (_state == value)
                return;

            _state = value;
            if (!_isSyncingHostState)
                ChromeHost?.SetWindowState(ToHostState(value));
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiWindow? Owner { get; private set; }

    public IReadOnlyList<UiWindow> OwnedWindows => _ownedWindows;

    public BRect Placement
    {
        get => _placement;
        private set
        {
            if (_placement == value)
                return;

            _placement = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiViewportBinding ViewportBinding => _viewportBinding;

    public bool IsActive => _isActive;

    public bool IsClosed => _isClosed;

    /// <summary>
    /// True once this window has broken out into its own native host window via
    /// <see cref="BreakOut"/>. Break-out is one-way for the window's lifetime.
    /// </summary>
    public bool IsBrokenOut => _isBrokenOut;

    /// <summary>
    /// Whether this window promotes itself into its own native window as soon as it opens.
    /// Defaults to <see cref="UiWindowBreakOutMode.Automatic"/>: an owned window or dialog is a
    /// real OS window wherever the host supports it, and stays a logical subwindow where it does
    /// not. Set <see cref="UiWindowBreakOutMode.Manual"/> to keep a window inside its owner.
    /// </summary>
    public UiWindowBreakOutMode BreakOutMode { get; set; } = UiWindowBreakOutMode.Automatic;

    /// <summary>
    /// True when this window can break out into its own native top-level host window: it is an
    /// attached owned/dialog subwindow, is not already broken out, and its session host supports
    /// the <see cref="IUiWindowHost"/> capability.
    /// </summary>
    public bool CanBreakOut =>
        !IsDisposed && !_isClosed && !_isBrokenOut && Owner is not null && Session?.Host is IUiWindowHost;

    /// <summary>
    /// Promotes this owned/dialog subwindow into its own native top-level host window. The window
    /// is detached from its owner and re-rooted in a fresh <see cref="UiSession"/> bound to a host
    /// window created by the session host's <see cref="IUiWindowHost"/> capability. Returns false
    /// when <see cref="CanBreakOut"/> is false. Break-out is one-way; closing the window disposes
    /// the host window.
    /// </summary>
    /// <param name="placement">
    /// Requested initial placement in device-independent pixels. When empty, the window's current
    /// bounds (or placement) seed the new window, and the host may pick a default.
    /// </param>
    public bool BreakOut(BRect placement = default)
    {
        ThrowIfDisposed();
        if (!CanBreakOut)
            return false;

        UiSession origin = Session!;
        var windowHost = (IUiWindowHost)origin.Host;
        UiWindow owner = Owner!;
        string title = _title;
        bool isModal = BreakOutIsModal;
        BRect requested = placement.IsEmpty ? ResolveBreakOutPlacement() : placement;

        // Detach from the owner: clears Owner/Kind/Placement and detaches from the origin session.
        // The reparenting guard keeps this transient detach from finalizing a live dialog.
        _isReparenting = true;
        owner.RemoveChild(this);

        IUiHostWindow hostWindow = windowHost.CreateHostWindow(
            new UiHostWindowRequest(title, requested, isModal, ResolveRequestedChrome(), _canResize));
        var hosted = new UiSession(hostWindow, origin.Dispatcher, origin.Clock, origin.Factories);

        _hostWindow = hostWindow;
        _hostedSession = hosted;
        _isBrokenOut = true;

        hostWindow.CloseRequested += HandleHostWindowCloseRequested;
        Closed += HandleBrokenOutClosed;

        hosted.AddRoot(this);
        _isReparenting = false;
        hostWindow.Bind(hosted);
        if (_icon?.NativePixels is not null && hostWindow is IUiWindowChromeHost chrome)
            chrome.SetIcon(_icon.NativePixels);
        OnBrokenOut(origin, hosted);
        hostWindow.Activate();
        Activate();
        return true;
    }

    /// <summary>
    /// Breaks out when <see cref="BreakOutMode"/> allows it and the host supports it. Popups,
    /// menus, and tooltips never do: they are transient overlays positioned against their owner,
    /// and a native window for each would flash on screen and steal activation.
    /// </summary>
    protected bool TryBreakOutAutomatically()
    {
        if (BreakOutMode != UiWindowBreakOutMode.Automatic)
            return false;
        if (Kind is UiWindowKind.Popup or UiWindowKind.Tooltip)
            return false;

        return CanBreakOut && BreakOut();
    }

    /// <summary>
    /// Called once this window has been attached to an owner and activated. The base
    /// implementation breaks it out into its own native window (see
    /// <see cref="TryBreakOutAutomatically"/>); a subclass that finishes its own presentation
    /// first overrides this and breaks out at the end of that.
    /// </summary>
    protected virtual void OnOpened() => TryBreakOutAutomatically();

    private UiHostWindowChrome ResolveRequestedChrome() =>
        _chrome == UiWindowChrome.None ? UiHostWindowChrome.System : UiHostWindowChrome.Owner;

    /// <summary>
    /// Whether a break-out of this window is application-modal to its origin window. The base
    /// window is modeless; <c>UiDialog</c> overrides this to report its presentation mode.
    /// </summary>
    protected virtual bool BreakOutIsModal => false;

    /// <summary>
    /// True while this window is being detached from one session and re-attached to another during
    /// a break-out. Subclasses use it to skip teardown that a permanent detach would trigger.
    /// </summary>
    protected bool IsReparenting => _isReparenting;

    /// <summary>
    /// Called after this window has been re-rooted into <paramref name="hostedSession"/> during a
    /// break-out. <paramref name="originSession"/> is the session it left. Subclasses migrate
    /// cross-window state (e.g. modality) here.
    /// </summary>
    protected virtual void OnBrokenOut(UiSession originSession, UiSession hostedSession)
    {
    }

    private BRect ResolveBreakOutPlacement()
    {
        if (!_placement.IsEmpty)
            return _placement;
        if (!Bounds.IsEmpty)
            return new BRect(0, 0, Bounds.Width, Bounds.Height);
        return BRect.Empty;
    }

    private void HandleHostWindowCloseRequested(object? sender, EventArgs e) =>
        Close(UiWindowCloseReason.User);

    private void HandleBrokenOutClosed(object? sender, UiWindowClosedEventArgs e) =>
        TearDownBreakOut();

    private void TearDownBreakOut()
    {
        if (_isTearingDownBreakOut || !_isBrokenOut)
            return;

        _isTearingDownBreakOut = true;
        IUiHostWindow? hostWindow = _hostWindow;
        UiSession? hosted = _hostedSession;
        _hostWindow = null;
        _hostedSession = null;

        if (hostWindow is not null)
            hostWindow.CloseRequested -= HandleHostWindowCloseRequested;

        // The base close/dispose already removed this window from the hosted session as a root.
        if (hosted is not null && !hosted.IsDisposed)
            hosted.Dispose();
        hostWindow?.Dispose();
    }

    public void SetPlacement(BRect placement)
    {
        ThrowIfDisposed();
        Placement = placement;
    }

    public void BindViewport(UiViewportBinding binding)
    {
        ThrowIfDisposed();
        if (_viewportBinding == binding)
            return;

        _viewportBinding = binding;
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>Minimizes the window. Requires a chrome-capable host; otherwise a no-op.</summary>
    public bool Minimize() => ApplyWindowState(UiWindowState.Minimized);

    /// <summary>Maximizes the window. Requires a chrome-capable host; otherwise a no-op.</summary>
    public bool Maximize() => ApplyWindowState(UiWindowState.Maximized);

    /// <summary>Restores the window from minimized or maximized.</summary>
    public bool Restore() => ApplyWindowState(UiWindowState.Normal);

    /// <summary>Maximizes a normal window and restores a maximized one — the title-bar double-click.</summary>
    public bool ToggleMaximize() =>
        ApplyWindowState(_state == UiWindowState.Maximized ? UiWindowState.Normal : UiWindowState.Maximized);

    /// <summary>
    /// Hands an in-progress pointer press to the window manager as a window move. Returns false
    /// for a logical subwindow, which has no native window and moves itself by placement instead.
    /// </summary>
    public bool BeginMoveDrag()
    {
        ThrowIfDisposed();
        IUiWindowChromeHost? host = ChromeHost;
        if (host is null)
            return false;

        // Dragging a maximized window restores it first, the way a native title bar does.
        if (_state == UiWindowState.Maximized)
            Restore();

        host.BeginMoveDrag();
        return true;
    }

    /// <summary>
    /// Hands an in-progress pointer press to the window manager as a resize of
    /// <paramref name="edge"/>. Returns false without a chrome-capable host.
    /// </summary>
    public bool BeginResizeDrag(UiWindowEdge edge)
    {
        ThrowIfDisposed();
        if (edge == UiWindowEdge.None || !_canResize)
            return false;

        IUiWindowChromeHost? host = ChromeHost;
        if (host is null || !host.IsResizable)
            return false;

        host.BeginResizeDrag(edge);
        return true;
    }

    public void OpenOwnedWindow(UiWindow window) =>
        OpenOwnedWindow(window, BRect.Empty, UiWindowKind.Owned);

    public void OpenOwnedWindow(UiWindow window, BRect placement, UiWindowKind kind = UiWindowKind.Owned)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(window);
        window.ThrowIfDisposed();

        if (window.Owner is not null || window.Parent is not null || window.Session is not null)
            throw new InvalidOperationException("Owned windows must be unattached logical windows.");
        if (ReferenceEquals(window, this) || IsDescendantOf(window))
            throw new InvalidOperationException("A logical window cannot own itself or one of its ancestors.");

        window.Owner = this;
        window.Kind = kind == UiWindowKind.TopLevel ? UiWindowKind.Owned : kind;
        window.Placement = placement;
        try
        {
            AddChild(window);
            _ownedWindows.Add(window);
        }
        catch
        {
            window.Owner = null;
            window.Kind = UiWindowKind.TopLevel;
            window.Placement = BRect.Empty;
            throw;
        }

        window.Activate();
        window.OnOpened();
    }

    public void BringToFront()
    {
        ThrowIfDisposed();
        if (Parent is not null)
            Parent.MoveChildToFront(this);
        else
            Session?.BringRootToFront(this);
    }

    public void Activate()
    {
        ThrowIfDisposed();
        if (_isClosed)
            return;

        if (Session is not null)
            DeactivateWindows(Session.Roots, this);

        BringToFront();
        if (_isActive)
            return;

        _isActive = true;
        Activated?.Invoke(this, EventArgs.Empty);
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public void Deactivate()
    {
        ThrowIfDisposed();
        DeactivateInternal();
    }

    public bool Close(UiWindowCloseReason reason = UiWindowCloseReason.Programmatic)
    {
        if (_isClosed)
            return true;

        ThrowIfDisposed();
        var closing = new UiWindowClosingEventArgs(reason);
        Closing?.Invoke(this, closing);
        if (closing.Cancel)
            return false;

        foreach (UiWindow ownedWindow in _ownedWindows.ToArray())
            ownedWindow.Close(UiWindowCloseReason.OwnerClosed);

        // A broken-out window tears its own host window down from the Closed handler; any other
        // window backed by a native one has to ask the host to close it, or the logical window
        // would go away while its OS window stayed on screen.
        IUiWindowChromeHost? chromeHost = _isBrokenOut ? null : ChromeHost;

        _isClosed = true;
        DeactivateInternal();
        Closed?.Invoke(this, new UiWindowClosedEventArgs(reason));
        chromeHost?.RequestClose();
        Dispose();
        return true;
    }

    protected override bool OnInput(UiInputEvent input)
    {
        Activate();
        return base.OnInput(input);
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Window,
            string.IsNullOrWhiteSpace(Title) ? GetType().Name : Title,
            Bounds,
            CreateSemanticState(),
            CreateChildSemanticNodes());

    protected override void OnAttached()
    {
        base.OnAttached();
        SubscribeToChromeHost();
    }

    protected override void OnDetached()
    {
        UnsubscribeFromChromeHost();
        base.OnDetached();
    }

    protected override void OnChildRemoved(UiElement child)
    {
        if (child is UiWindow window && ReferenceEquals(window.Owner, this))
        {
            _ownedWindows.Remove(window);
            window.Owner = null;
            window.Kind = UiWindowKind.TopLevel;
            window.Placement = BRect.Empty;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnsubscribeFromChromeHost();
            if (!_isClosed)
            {
                _isClosed = true;
                foreach (UiWindow ownedWindow in _ownedWindows.ToArray())
                    ownedWindow.Close(UiWindowCloseReason.OwnerClosed);
                DeactivateInternal();
            }
        }

        base.Dispose(disposing);
    }

    private UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsActive)
            state |= UiSemanticState.Focused;
        return state;
    }

    private IReadOnlyList<UiSemanticNode> CreateChildSemanticNodes()
    {
        if (Children.Count == 0)
            return [];

        var nodes = new List<UiSemanticNode>(Children.Count);
        foreach (UiElement child in Children)
        {
            if (child.Visibility != UiVisibility.Collapsed)
                nodes.Add(child.GetSemanticNode());
        }

        return nodes;
    }

    private void DeactivateInternal()
    {
        if (!_isActive)
            return;

        _isActive = false;
        Deactivated?.Invoke(this, EventArgs.Empty);
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private void SetChromeFlag(ref bool field, bool value)
    {
        ThrowIfDisposed();
        if (field == value)
            return;

        field = value;
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private bool ApplyWindowState(UiWindowState state)
    {
        ThrowIfDisposed();
        if (_isClosed)
            return false;

        State = state;
        return ChromeHost is not null;
    }

    private void SubscribeToChromeHost()
    {
        IUiWindowChromeHost? host = ChromeHost;
        if (host is null || ReferenceEquals(host, _subscribedChromeHost))
            return;

        UnsubscribeFromChromeHost();
        _subscribedChromeHost = host;
        host.WindowStateChanged += HandleHostWindowStateChanged;

        // Adopt whatever the native window already is, then push what this window already knows.
        SyncStateFromHost(host);
        host.SetTitle(_title);
        if (_icon?.NativePixels is not null)
            host.SetIcon(_icon.NativePixels);
    }

    private void UnsubscribeFromChromeHost()
    {
        if (_subscribedChromeHost is null)
            return;

        _subscribedChromeHost.WindowStateChanged -= HandleHostWindowStateChanged;
        _subscribedChromeHost = null;
    }

    private void HandleHostWindowStateChanged(object? sender, EventArgs e)
    {
        if (sender is IUiWindowChromeHost host)
            SyncStateFromHost(host);
    }

    /// <summary>
    /// Adopts the native show state without echoing it back to the host — the user snapping or
    /// minimizing from the taskbar must not turn into a redundant command.
    /// </summary>
    private void SyncStateFromHost(IUiWindowChromeHost host)
    {
        if (IsDisposed)
            return;

        _isSyncingHostState = true;
        try
        {
            State = FromHostState(host.WindowState);
        }
        finally
        {
            _isSyncingHostState = false;
        }
    }

    private static UiHostWindowState ToHostState(UiWindowState state) => state switch
    {
        UiWindowState.Minimized => UiHostWindowState.Minimized,
        UiWindowState.Maximized => UiHostWindowState.Maximized,
        _ => UiHostWindowState.Normal,
    };

    private static UiWindowState FromHostState(UiHostWindowState state) => state switch
    {
        UiHostWindowState.Minimized => UiWindowState.Minimized,
        UiHostWindowState.Maximized => UiWindowState.Maximized,
        _ => UiWindowState.Normal,
    };

    private static void DeactivateWindows(IEnumerable<UiElement> elements, UiWindow except)
    {
        foreach (UiElement element in elements)
        {
            if (ReferenceEquals(element, except))
                continue;
            if (element is UiWindow window)
                window.DeactivateInternal();

            DeactivateWindows(element.Children, except);
        }
    }
}
