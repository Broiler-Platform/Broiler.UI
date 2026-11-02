using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.UI.Window;

namespace Broiler.UI.Tooltip.Standard;

/// <summary>
/// Turns hovering into a tooltip: watches where the pointer is, finds the nearest element that has
/// something to say, and drives one <see cref="UiTooltip"/> to say it.
/// </summary>
/// <remarks>
/// <see cref="UiTooltip"/> has always known how to place itself, how long to wait and when to give
/// up, but nothing connected it to a pointer, so every application that wanted a tooltip wrote the
/// same hover tracking by hand. This is that tracking, once.
///
/// The delay is the awkward part of any tooltip, because waiting is not something a render loop
/// does on its own: the tooltip opens when enough time has passed, and if nothing draws a frame in
/// the meantime, "enough time has passed" is never noticed. That is what <see cref="IsWaiting"/>
/// is for - while it is true the host has a reason to keep producing frames, by whatever means it
/// has, and while it is false it has none.
/// </remarks>
public sealed class StandardTooltipController
{
    private readonly UiTooltip _tooltip;
    private readonly Action<bool>? _onWaitingChanged;
    private UiElement? _target;

    public event EventHandler<bool>? WaitingChanged;

    public StandardTooltipController(UiTooltip tooltip)
        : this(tooltip, null)
    {
    }

    public StandardTooltipController(UiTooltip tooltip, Action<bool>? onWaitingChanged)
    {
        _tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
        _onWaitingChanged = onWaitingChanged;
    }

    /// <summary>The tooltip being driven.</summary>
    public UiTooltip Tooltip => _tooltip;

    /// <summary>The element the tooltip is currently about, or null when none is.</summary>
    public UiElement? Target => _target;

    /// <summary>
    /// True while a tooltip has been asked for but has not appeared yet, so its delay is still
    /// running. A host that only draws when something changes has to keep drawing while this is
    /// true, or the delay never elapses and the tooltip never appears.
    /// </summary>
    public bool IsWaiting => _target is not null && !_tooltip.IsTooltipOpen;

    /// <summary>
    /// Tells the controller where the pointer is. Returns true when the frame should be redrawn.
    /// </summary>
    public bool PointerMoved(BPoint position)
    {
        UiSession? session = _tooltip.Session;
        UiElement? hit = session?.HitTest(position);
        return SetTarget(FindTipBearer(hit));
    }

    /// <summary>
    /// Tells the controller the pointer has gone, or that something happened that should dismiss
    /// the tooltip - a click, a key, a menu opening. Returns true when the frame should be redrawn.
    /// </summary>
    public bool Dismiss() => SetTarget(null);

    /// <summary>
    /// Routes an incoming user input event to update tooltip state. Returns true when the frame should be redrawn.
    /// </summary>
    public bool Dispatch(UiInputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Kind == UiInputEventKind.PointerMove
            ? PointerMoved(input.Position)
            : Dismiss();
    }

    /// <summary>
    /// Lets the delay advance. Returns true when the tooltip appeared or disappeared and the frame
    /// should be redrawn. Safe to call every frame.
    /// </summary>
    public bool Tick()
    {
        if (_target is null)
            return false;

        bool wasWaiting = IsWaiting;
        bool changed = _tooltip.UpdateVisibility();
        CheckWaitingChanged(wasWaiting);
        return changed;
    }

    /// <summary>
    /// The nearest element at or above <paramref name="element"/> that has tooltip text. Walking
    /// up matters because the thing under the pointer is often a part of the control rather than
    /// the control - the pointer is over a toolbar's button, and the button is what has the text.
    /// </summary>
    private static UiElement? FindTipBearer(UiElement? element)
    {
        for (UiElement? candidate = element; candidate is not null; candidate = candidate.Parent)
        {
            if (candidate.Visibility != UiVisibility.Visible)
                return null;

            if (!string.IsNullOrEmpty(candidate.ToolTipText))
                return candidate;
        }

        return null;
    }

    private bool SetTarget(UiElement? target)
    {
        if (ReferenceEquals(target, _target))
            return false;

        bool wasOpen = _tooltip.IsTooltipOpen;
        bool wasWaiting = IsWaiting;
        _target = target;

        if (target is null)
        {
            _tooltip.Hide();
            CheckWaitingChanged(wasWaiting);
            return wasOpen;
        }

        _tooltip.Text = target.ToolTipText;
        _tooltip.Start(target.Bounds);

        CheckWaitingChanged(wasWaiting);

        // Moving between two controls that both have tooltips restarts the wait rather than
        // carrying the old one over, so the tooltip does not chase the pointer along a toolbar.
        return wasOpen;
    }

    private void CheckWaitingChanged(bool wasWaiting)
    {
        bool nowWaiting = IsWaiting;
        if (nowWaiting != wasWaiting)
        {
            if (nowWaiting)
                (_tooltip.Session?.Host as IUiAnimationHost)?.StartAnimation();
            else
                (_tooltip.Session?.Host as IUiAnimationHost)?.StopAnimation();

            _onWaitingChanged?.Invoke(nowWaiting);
            WaitingChanged?.Invoke(this, nowWaiting);
        }
    }

    /// <summary>
    /// Attaches tooltip functionality to a window and session, creating an owned tooltip window
    /// and subscribing to session input dispatching automatically.
    /// </summary>
    public static StandardTooltipController Attach(
        UiWindow rootWindow,
        StandardTooltip? tooltip = null,
        Action<bool>? onWaitingChanged = null)
    {
        ArgumentNullException.ThrowIfNull(rootWindow);
        return Attach(rootWindow.Session, rootWindow, tooltip, onWaitingChanged);
    }

    /// <summary>
    /// Attaches tooltip functionality to a window and session, creating an owned tooltip window
    /// and subscribing to session input dispatching automatically.
    /// </summary>
    public static StandardTooltipController Attach(
        UiSession? session,
        UiWindow rootWindow,
        StandardTooltip? tooltip = null,
        Action<bool>? onWaitingChanged = null)
    {
        ArgumentNullException.ThrowIfNull(rootWindow);
        tooltip ??= new StandardTooltip();
        rootWindow.OpenOwnedWindow(tooltip, new BRect(0, 0, 1, 1), UiWindowKind.Tooltip);
        tooltip.Deactivate();

        var controller = new StandardTooltipController(tooltip, onWaitingChanged);

        void HookSession(UiSession s)
        {
            s.InputDispatched += (_, input) =>
            {
                if (controller.Dispatch(input))
                    controller.Tooltip.Invalidate(UiInvalidationKind.Render);
            };
        }

        if (session is not null)
        {
            HookSession(session);
        }
        else if (rootWindow.Session is UiSession rootSession)
        {
            HookSession(rootSession);
        }

        return controller;
    }
}
