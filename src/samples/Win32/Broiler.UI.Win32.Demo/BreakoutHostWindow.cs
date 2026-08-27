using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Broiler.Graphics;
using Broiler.Graphics.Windows;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.UI.Standard;

namespace Broiler.UI.Win32.Demo;

/// <summary>
/// A real second native window that hosts a broken-out Broiler.UI subwindow. It is a
/// <see cref="Direct2DWindow"/> that does not own the thread message loop (it is serviced by the
/// main window's loop) and exposes the neutral <see cref="IUiHostWindow"/> and
/// <see cref="IUiWindowChromeHost"/> contracts so a <see cref="UiSession"/> can render into it,
/// receive its input, and draw its title bar.
/// </summary>
/// <remarks>
/// The window is created with <see cref="BWindowChrome.Owner"/>, so Windows draws no caption and
/// the broken-out window keeps the one title bar it already had.
/// </remarks>
[SupportedOSPlatform("windows7.0")]
internal sealed class BreakoutHostWindow : Direct2DWindow, IUiHostWindow, IUiWindowChromeHost, IUiClipboardHost, IUiTextInputHost
{
    private UiSession? _session;
    private string _clipboard = string.Empty;
    private UiTextCaretInfo? _caret;

#pragma warning disable CS0618
    private readonly StandardLegacyGraphicsInputAdapter _legacyInput = new("broiler-ui-breakout-window");
#pragma warning restore CS0618

    public BreakoutHostWindow(UiHostWindowRequest request)
        : base(new BWindowOptions
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Broiler.UI" : request.Title,
            ClientWidth = ToClientExtent(request.Placement.Width, 480),
            ClientHeight = ToClientExtent(request.Placement.Height, 320),
            Left = request.Placement.IsEmpty ? null : request.Placement.X,
            Top = request.Placement.IsEmpty ? null : request.Placement.Y,
            ClearColor = StandardControlPaint.Theme.SurfaceAlt,
            RenderOptions = new BRenderOptions(Antialias: true, VSync: true, SubpixelText: true),
            OwnsMessageLoop = false,
            Chrome = request.Chrome == UiHostWindowChrome.Owner ? BWindowChrome.Owner : BWindowChrome.System,
            Resizable = request.Resizable,
        })
    {
        StateChanged += (_, _) => WindowStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // IUiHost
    BSize IUiHost.ViewportSize => ClientSize;

    double IUiHost.Scale => DpiScale;

    BRenderList IUiHost.CreateRenderList(int capacity) => new(capacity);

    void IUiHost.Invalidate(UiInvalidation invalidation) => InvalidateIfAlive();

    void IUiHost.Present(BRenderList renderList)
    {
        // The base Direct2DWindow renders the list returned from BuildRenderList; nothing else to do.
    }

    // IUiHostWindow. CloseRequested and SetTitle come from BWindow, which already raises the
    // request from WM_CLOSE without destroying a secondary window.
    public void Bind(UiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        InvalidateIfAlive();
    }

    public void Activate()
    {
        if (IsDisposed || NativeHandle == IntPtr.Zero)
            return;

        SetForegroundWindow(NativeHandle);
        InvalidateIfAlive();
    }

    // IUiWindowChromeHost
    public event EventHandler? WindowStateChanged;

    UiHostWindowChrome IUiWindowChromeHost.Chrome =>
        Options.Chrome == BWindowChrome.Owner ? UiHostWindowChrome.Owner : UiHostWindowChrome.System;

    bool IUiWindowChromeHost.IsResizable => Options.Resizable;

    UiHostWindowState IUiWindowChromeHost.WindowState => ToHostState(WindowState);

    void IUiWindowChromeHost.SetWindowState(UiHostWindowState state) => SetWindowState(state switch
    {
        UiHostWindowState.Minimized => BWindowState.Minimized,
        UiHostWindowState.Maximized => BWindowState.Maximized,
        _ => BWindowState.Normal,
    });

    void IUiWindowChromeHost.SetIcon(BPixelBuffer? icon) => SetIcon(icon);

    void IUiWindowChromeHost.RequestClose() => Close();

    void IUiWindowChromeHost.BeginMoveDrag() => BeginMoveDrag();

    void IUiWindowChromeHost.BeginResizeDrag(UiWindowEdge edge) => BeginResizeDrag(ToWindowEdge(edge));

    // IUiClipboardHost / IUiTextInputHost mirror the main window's DemoUiHost so hosted controls
    // keep clipboard and caret behavior.
    public bool TryGetText(out string text)
    {
        text = _clipboard;
        return true;
    }

    public void SetText(string text) => _clipboard = text ?? string.Empty;

    public void PublishCaret(UiTextCaretInfo caret) => _caret = caret;

    public void ClearCaret(UiElement owner)
    {
        if (_caret?.Owner == owner)
            _caret = null;
    }

    protected override BRenderList? BuildRenderList(BSize clientSize) =>
        _session is { IsDisposed: false } session ? session.RenderFrame() : null;

    protected override void OnResized(BSize clientSize, double dpiScale) => InvalidateIfAlive();

    protected override void OnPointerDown(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnPointerMove(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerMove(e));

    protected override void OnPointerUp(BPointerEventArgs e) => Dispatch(_legacyInput.FromPointerButton(e));

    protected override void OnMouseWheel(BMouseWheelEventArgs e) => Dispatch(_legacyInput.FromMouseWheel(e));

    protected override void OnKeyDown(BKeyEventArgs e) => Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Down));

    protected override void OnKeyUp(BKeyEventArgs e) => Dispatch(_legacyInput.FromKey(e, KeyboardKeyTransition.Up));

    protected override void OnTextInput(BTextInputEventArgs e) => Dispatch(_legacyInput.FromText(e));

    protected override void Dispose(bool disposing)
    {
        // Destroy the native window when the framework disposes this host window.
        if (disposing && !IsDisposed)
            Close();

        base.Dispose(disposing);
    }

    internal static UiHostWindowState ToHostState(BWindowState state) => state switch
    {
        BWindowState.Minimized => UiHostWindowState.Minimized,
        BWindowState.Maximized => UiHostWindowState.Maximized,
        _ => UiHostWindowState.Normal,
    };

    internal static BWindowEdge ToWindowEdge(UiWindowEdge edge) => edge switch
    {
        UiWindowEdge.Left => BWindowEdge.Left,
        UiWindowEdge.Top => BWindowEdge.Top,
        UiWindowEdge.Right => BWindowEdge.Right,
        UiWindowEdge.Bottom => BWindowEdge.Bottom,
        UiWindowEdge.TopLeft => BWindowEdge.TopLeft,
        UiWindowEdge.TopRight => BWindowEdge.TopRight,
        UiWindowEdge.BottomLeft => BWindowEdge.BottomLeft,
        UiWindowEdge.BottomRight => BWindowEdge.BottomRight,
        _ => BWindowEdge.None,
    };

    /// <summary>
    /// Routes native input into the hosted session. Dispatching can close the broken-out window —
    /// a Cancel button, or the owner-drawn close button — which disposes the session *and* this
    /// host window while this call is still on the stack, so nothing here may assume it is still
    /// alive once <see cref="UiSession.DispatchInput"/> returns.
    /// </summary>
    private void Dispatch(UiInputEvent input)
    {
        if (_session is null || _session.IsDisposed)
            return;

        if (_session.DispatchInput(input))
            InvalidateIfAlive();
    }

    /// <summary>Repaints, unless this host window is already gone. See <see cref="Dispatch"/>.</summary>
    private void InvalidateIfAlive()
    {
        if (!IsDisposed && NativeHandle != IntPtr.Zero)
            Invalidate();
    }

    private static int ToClientExtent(double requested, int fallback) =>
        requested > 1 ? (int)Math.Round(requested) : fallback;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
