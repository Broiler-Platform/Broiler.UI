using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
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
// Broiler-AI:           Origin=AI; Spec=ADR-0026; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: a press that starts on one system button and is released over another part still runs the first button's command
// Broiler-Human:        PENDING
public sealed class UiWindowChromeController
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static readonly TimeSpan DoubleClickInterval = TimeSpan.FromMilliseconds(500);

    private readonly UiWindow _window;
    private UiWindowChromePart _hotPart;
    private UiWindowChromePart _pressedPart;
    private TimeSpan? _lastTitleBarPress;

    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a layout that turns invisible keeps a stale hot or pressed part, so a later release runs a system button command
    // Broiler-Human:        PENDING
    public UiWindowChromeLayout UpdateLayout(BRect bounds)
    {
        Layout = UiWindowChromeLayout.Create(_window, bounds, Metrics);
        if (!Layout.IsVisible)
            ClearInteraction();

        return Layout;
    }

    /// <summary>Drops hover and pressed state, e.g. when the pointer leaves the window.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
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
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: an input event arriving while the chrome layout is hidden is consumed, so a window without chrome loses content clicks
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a press that drifts from a system button onto another part keeps that button as the pressed part
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a left-button release over Close runs Close although the press began on the title bar or on another button
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: two title-bar presses with the same clock reading, or more than 500 ms apart, toggle maximize
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: the Minimize or Maximize part closes the window, or the Close part closes it with a reason other than User
    // Broiler-Human:        PENDING
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static bool IsButton(UiWindowChromePart part) =>
        part is UiWindowChromePart.Minimize or UiWindowChromePart.Maximize or UiWindowChromePart.Close;
}
