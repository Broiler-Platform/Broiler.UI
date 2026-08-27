using System;
using Broiler.Graphics;
using Broiler.Input.Mouse;

namespace Broiler.UI.Window;

/// <summary>
/// The behaviour behind an owner-drawn title bar: hover and pressed tracking for the system
/// buttons, the commands they run, double-click to maximize, and handing a title-bar drag to the
/// window manager. Implementations own the painting; this owns what the chrome *does*, so every
/// control family behaves identically.
/// </summary>
/// <remarks>
/// On a window that has no native window behind it — a logical subwindow rendered inside its
/// owner — <see cref="UiWindow.BeginMoveDrag"/> reports false and a title-bar press is left
/// unhandled, so the owner's own logical move (e.g. <c>UiDialog</c>'s move grip) still runs.
/// </remarks>
public sealed class UiWindowChromeController
{
    private static readonly TimeSpan DoubleClickInterval = TimeSpan.FromMilliseconds(500);

    private readonly UiWindow _window;
    private UiWindowChromePart _hotPart;
    private UiWindowChromePart _pressedPart;
    private TimeSpan? _lastTitleBarPress;

    public UiWindowChromeController(UiWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <summary>Sizes used the next time <see cref="UpdateLayout"/> runs.</summary>
    public UiWindowChromeMetrics Metrics { get; set; } = UiWindowChromeMetrics.Default;

    /// <summary>The chrome geometry from the last <see cref="UpdateLayout"/>.</summary>
    public UiWindowChromeLayout Layout { get; private set; }

    /// <summary>The part the pointer is over, for hover painting.</summary>
    public UiWindowChromePart HotPart => _hotPart;

    /// <summary>The part being held down, for pressed painting.</summary>
    public UiWindowChromePart PressedPart => _pressedPart;

    /// <summary>Recomputes the layout for <paramref name="bounds"/> and returns it.</summary>
    public UiWindowChromeLayout UpdateLayout(BRect bounds)
    {
        Layout = UiWindowChromeLayout.Create(_window, bounds, Metrics);
        if (!Layout.IsVisible)
            ClearInteraction();

        return Layout;
    }

    /// <summary>Drops hover and pressed state, e.g. when the pointer leaves the window.</summary>
    public void ClearInteraction()
    {
        if (_hotPart == UiWindowChromePart.None && _pressedPart == UiWindowChromePart.None)
            return;

        _hotPart = UiWindowChromePart.None;
        _pressedPart = UiWindowChromePart.None;
        _window.Invalidate(UiInvalidationKind.Render);
    }

    /// <summary>
    /// Runs the chrome's share of an input event. Returns true when the chrome consumed it, which
    /// the caller should treat as handled before anything else looks at the event.
    /// </summary>
    public bool HandleInput(UiInputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Layout.IsVisible)
            return false;

        return input.Kind switch
        {
            UiInputEventKind.PointerMove => HandlePointerMove(input.Position),
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            _ => false,
        };
    }

    private bool HandlePointerMove(BPoint position)
    {
        UiWindowChromePart part = Layout.HitTest(position);
        if (part == _hotPart)
            return false;

        _hotPart = part;

        // A button press that drifts off the button cancels, the way every other button does.
        if (_pressedPart != UiWindowChromePart.None && _pressedPart != part)
            _pressedPart = UiWindowChromePart.None;

        _window.Invalidate(UiInvalidationKind.Render);
        return false;
    }

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        UiWindowChromePart part = Layout.HitTest(input.Position);

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            _hotPart = part;
            if (IsButton(part))
            {
                _pressedPart = part;
                _window.Activate();
                _window.Invalidate(UiInvalidationKind.Render);
                return true;
            }

            _pressedPart = UiWindowChromePart.None;
            return part == UiWindowChromePart.TitleBar && HandleTitleBarPress();
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            UiWindowChromePart pressed = _pressedPart;
            _pressedPart = UiWindowChromePart.None;
            if (pressed == UiWindowChromePart.None || pressed != part)
                return false;

            _window.Invalidate(UiInvalidationKind.Render);
            Execute(pressed);
            return true;
        }

        return false;
    }

    private bool HandleTitleBarPress()
    {
        // A strictly positive delta is required, not just one inside the interval: a session
        // driven by a clock that does not advance would otherwise read every second press as a
        // double click.
        TimeSpan now = _window.Session?.Clock.Now.Elapsed ?? TimeSpan.Zero;
        bool isDoubleClick = _lastTitleBarPress is { } previous
            && now - previous > TimeSpan.Zero
            && now - previous <= DoubleClickInterval;
        _lastTitleBarPress = isDoubleClick ? null : now;

        _window.Activate();
        if (isDoubleClick && _window.CanMaximize)
            return _window.ToggleMaximize();

        // BeginMoveDrag hands the press to the window manager, so no move events follow; hover
        // state would otherwise stay stuck on the title bar for the whole drag.
        if (!_window.BeginMoveDrag())
            return false;

        ClearInteraction();
        return true;
    }

    private void Execute(UiWindowChromePart part)
    {
        switch (part)
        {
            case UiWindowChromePart.Minimize:
                _window.Minimize();
                break;
            case UiWindowChromePart.Maximize:
                _window.ToggleMaximize();
                break;
            case UiWindowChromePart.Close:
                _window.Close(UiWindowCloseReason.User);
                break;
        }
    }

    private static bool IsButton(UiWindowChromePart part) =>
        part is UiWindowChromePart.Minimize or UiWindowChromePart.Maximize or UiWindowChromePart.Close;
}
