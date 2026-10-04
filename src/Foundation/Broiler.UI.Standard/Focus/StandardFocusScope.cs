using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.UI.Standard;

/// <summary>
/// Scoped focus management providing sequential traversal (Tab/Shift+Tab), active tab
/// and modal containment, and focus restoration across popups and dialogs.
/// </summary>
public sealed class StandardFocusScope
{
    private readonly UiSession _session;
    private readonly Stack<UiElement?> _focusStack = new();

    public StandardFocusScope(UiSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public UiElement? FocusedElement => _session.FocusedElement;

    public bool TryFocus(UiElement? element)
    {
        if (element is not null && element.Session != _session)
            return false;

        _session.SetFocus(element);
        if (_session.FocusedElement == element && element is not null)
        {
            element.BringIntoView();
        }
        return true;
    }

    /// <summary>Saves the current focused element to the restoration stack.</summary>
    public void SaveFocus()
    {
        _focusStack.Push(_session.FocusedElement);
    }

    /// <summary>Restores focus to the last saved element that is still focusable.</summary>
    public bool RestoreFocus()
    {
        while (_focusStack.Count > 0)
        {
            UiElement? element = _focusStack.Pop();
            if (element is null)
            {
                _session.SetFocus(null);
                return true;
            }

            if (!element.IsDisposed && element.Session == _session && element.CanFocus)
            {
                _session.SetFocus(element);
                element.BringIntoView();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Captures the current focus and returns an <see cref="IDisposable"/> that automatically restores
    /// focus when disposed (useful for popups, flyouts, and dialog scopes).
    /// </summary>
    public IDisposable CaptureFocus()
    {
        SaveFocus();
        return new FocusRestorationCookie(this);
    }

    /// <summary>
    /// Moves focus sequentially in the specified direction (+1 forward, -1 backward).
    /// Respects active modal boundaries, tab boundaries, disabled/collapsed controls, and tab stops.
    /// Content a container hides while keeping it alive (<see cref="UiElement.IsHiddenFromAccessibility"/>,
    /// such as inactive tab content or a collapsed split pane) is skipped. Candidates are ordered by
    /// <see cref="UiElement.TabIndex"/>; equal indexes keep document order.
    /// </summary>
    public bool MoveFocus(int direction, UiElement? scopeRoot = null)
    {
        UiElement? root = scopeRoot ?? _session.ModalElement;
        List<UiElement> candidates = [];

        if (root is not null)
        {
            CollectFocusable(root, candidates);
        }
        else
        {
            foreach (UiElement sessionRoot in _session.Roots)
            {
                CollectFocusable(sessionRoot, candidates);
            }
        }

        if (candidates.Count == 0)
            return false;

        // List.Sort is unstable and would scramble document order among equal TabIndex values.
        candidates = candidates.OrderBy(candidate => candidate.TabIndex).ToList();

        int currentIndex = candidates.IndexOf(_session.FocusedElement!);
        int nextIndex = currentIndex < 0
            ? (direction > 0 ? 0 : candidates.Count - 1)
            : (currentIndex + direction + candidates.Count) % candidates.Count;

        UiElement next = candidates[nextIndex];
        _session.SetFocus(next);
        next.BringIntoView();
        return _session.FocusedElement == next;
    }

    /// <summary>
    /// The tab stop that follows <paramref name="from"/> in Tab order (+1), or that precedes it (-1), among the
    /// stops <see cref="MoveFocus"/> moves between: in <paramref name="scopeRoot"/>, or else the active modal element,
    /// or else every root. <paramref name="from"/> need not be a stop itself, such as a control that can no longer
    /// take focus: its place is where it would sort among the stops, by <see cref="UiElement.TabIndex"/> and then
    /// document order. Null when <paramref name="from"/> is not in the scope or no stop lies that way; it does not
    /// wrap. Nothing is focused or scrolled.
    /// </summary>
    public UiElement? FindAdjacentStop(UiElement from, int direction, UiElement? scopeRoot = null)
    {
        ArgumentNullException.ThrowIfNull(from);
        UiElement? root = scopeRoot ?? _session.ModalElement;
        IEnumerable<UiElement> roots = root is not null ? [root] : _session.Roots;

        // Every element has a place, shown or not, so one that was just hidden still has its own.
        var places = new Dictionary<UiElement, int>(ReferenceEqualityComparer.Instance);
        List<UiElement> candidates = [];
        foreach (UiElement sessionRoot in roots)
        {
            Number(sessionRoot, places);
            CollectFocusable(sessionRoot, candidates);
        }

        if (!places.TryGetValue(from, out int place))
            return null;

        // Candidates in Tab order, as MoveFocus orders them; the neighbor is the first past from's key that way.
        int Compare(UiElement stop) =>
            stop.TabIndex != from.TabIndex ? stop.TabIndex.CompareTo(from.TabIndex) : places[stop].CompareTo(place);
        IEnumerable<UiElement> ordered = candidates.OrderBy(candidate => candidate.TabIndex);
        return direction > 0
            ? ordered.FirstOrDefault(stop => Compare(stop) > 0)
            : ordered.LastOrDefault(stop => Compare(stop) < 0);

        static void Number(UiElement element, Dictionary<UiElement, int> places)
        {
            places[element] = places.Count;
            foreach (UiElement child in element.Children)
                Number(child, places);
        }
    }

    private static void CollectFocusable(UiElement element, List<UiElement> candidates)
    {
        if (element.Visibility != UiVisibility.Visible || element.IsHiddenFromAccessibility)
            return;

        if (element.CanFocus && element.IsTabStop)
        {
            candidates.Add(element);
        }

        foreach (UiElement child in element.Children)
        {
            CollectFocusable(child, candidates);
        }
    }

    private sealed class FocusRestorationCookie(StandardFocusScope scope) : IDisposable
    {
        private StandardFocusScope? _scope = scope;

        public void Dispose()
        {
            _scope?.RestoreFocus();
            _scope = null;
        }
    }
}
