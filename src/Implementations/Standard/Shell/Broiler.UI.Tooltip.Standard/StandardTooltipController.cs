using System;
using Broiler.Graphics;

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
    private UiElement? _target;

    public StandardTooltipController(UiTooltip tooltip)
    {
        _tooltip = tooltip ?? throw new ArgumentNullException(nameof(tooltip));
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
    /// Lets the delay advance. Returns true when the tooltip appeared or disappeared and the frame
    /// should be redrawn. Safe to call every frame.
    /// </summary>
    public bool Tick() => _target is not null && _tooltip.UpdateVisibility();

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
        _target = target;

        if (target is null)
        {
            _tooltip.Hide();
            return wasOpen;
        }

        _tooltip.Text = target.ToolTipText;
        _tooltip.Start(target.Bounds);

        // Moving between two controls that both have tooltips restarts the wait rather than
        // carrying the old one over, so the tooltip does not chase the pointer along a toolbar.
        return wasOpen;
    }
}
