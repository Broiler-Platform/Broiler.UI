using System;
using System.Collections.Generic;

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

        candidates.Sort((a, b) => a.TabIndex.CompareTo(b.TabIndex));

        int currentIndex = candidates.IndexOf(_session.FocusedElement!);
        int nextIndex = currentIndex < 0
            ? (direction > 0 ? 0 : candidates.Count - 1)
            : (currentIndex + direction + candidates.Count) % candidates.Count;

        UiElement next = candidates[nextIndex];
        _session.SetFocus(next);
        next.BringIntoView();
        return _session.FocusedElement == next;
    }

    private static void CollectFocusable(UiElement element, List<UiElement> candidates)
    {
        if (element.Visibility != UiVisibility.Visible)
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
