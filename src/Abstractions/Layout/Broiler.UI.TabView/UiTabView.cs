using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.TabView;

// Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: after MoveTab, or RemoveTab of an unselected tab, SelectedTab is a different tab from the one selected before, so the host binds its editor to a document the user did not activate
// Broiler-Human:        PENDING
public abstract class UiTabView : UiElement
{
    private readonly List<UiTabItem> _tabs = [];
    private int _selectedIndex = -1;
    private BSize _preferredSize = new(320, 220);
    private UiTabContentLifetimePolicy _inactiveContentPolicy;

    protected UiTabView()
    {
        Focusable = true;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiTabSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// A close affordance was used. The control does not act on it; a host that
    /// ignores this leaves the tab open. See ADR 0024.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiTabCloseRequestedEventArgs>? CloseRequested;

    /// <summary>Raised before a reorder is applied, and cancellable.</summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiTabReorderingEventArgs>? Reordering;

    /// <summary>A tab's header or dirty state changed.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiTabChangedEventArgs>? TabChanged;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiTabChangedEventArgs>? TabRemoved;

    public IReadOnlyList<UiTabItem> Tabs => _tabs;

    /// <summary>
    /// How many tabs fit in the strip. Tabs beyond this are reachable through
    /// the overflow affordance rather than being shrunk below legibility.
    /// </summary>
    public int VisibleTabCapacity { get; set; } = int.MaxValue;

    /// <summary>Tabs that do not fit, in order.</summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: with more tabs than VisibleTabCapacity, OverflowTabs omits the tab at index VisibleTabCapacity or includes one below it
    // Broiler-Human:        PENDING
    public IReadOnlyList<UiTabItem> OverflowTabs =>
        _tabs.Count <= VisibleTabCapacity ? [] : [.. _tabs.Skip(VisibleTabCapacity)];

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning an index below zero or at or beyond Tabs.Count changes SelectedIndex instead of leaving the selection as it was
    // Broiler-Human:        PENDING
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            ThrowIfDisposed();
            SelectIndex(value);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a SelectedIndex of -1 or equal to Tabs.Count returns a tab or throws instead of returning null
    // Broiler-Human:        PENDING
    public UiTabItem? SelectedTab =>
        (uint)SelectedIndex < (uint)Tabs.Count ? Tabs[SelectedIndex] : null;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a negative width or height is stored and reported to measure as the desired size
    // Broiler-Human:        PENDING
    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred tab view size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a value outside the defined policies, such as (UiTabContentLifetimePolicy)7, is stored instead of throwing
    // Broiler-Human:        PENDING
    public UiTabContentLifetimePolicy InactiveContentPolicy
    {
        get => _inactiveContentPolicy;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_inactiveContentPolicy == value)
                return;

            _inactiveContentPolicy = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an AddTab whose content already belongs to another tree throws but leaves the tab in Tabs with its id taken and, for the first tab, SelectedIndex at -1
    // Broiler-Human:        PENDING
    public UiTabItem AddTab(string id, string header, UiElement? content = null)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Tab IDs must be non-empty.", nameof(id));
        if (_tabs.Any(tab => StringComparer.Ordinal.Equals(tab.Id, id)))
            throw new ArgumentException("Tab IDs must be unique.", nameof(id));

        var item = new UiTabItem(id, header ?? string.Empty, content);
        item.Changed += OnTabChanged;
        _tabs.Add(item);
        if (content is not null)
            AddChild(content);
        if (_selectedIndex < 0)
            _selectedIndex = 0;

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return item;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an id that differs from the id of an open tab only by letter case or surrounding whitespace finds that tab
    // Broiler-Human:        PENDING
    public UiTabItem? FindTab(string id) =>
        _tabs.FirstOrDefault(tab => StringComparer.Ordinal.Equals(tab.Id, id));

    /// <summary>
    /// Activates a tab by identity. Returns false when no such tab exists, so a
    /// host can tell "already open, now focused" from "not open".
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: with VisibleTabCapacity at least 1, a tab selected from the overflow is still in OverflowTabs after SelectTab returns true
    // Broiler-Human:        PENDING
    public bool SelectTab(string id)
    {
        ThrowIfDisposed();
        int index = IndexOf(id);
        if (index < 0)
            return false;

        // Selecting a tab in the overflow brings it into the visible strip: the
        // active tab must always be reachable.
        if (index >= VisibleTabCapacity)
            MoveTab(id, Math.Max(0, VisibleTabCapacity - 1), raiseEvent: false);

        int target = IndexOf(id);
        return _selectedIndex == target || SelectIndex(target);
    }

    /// <summary>
    /// Asks the host to close a tab. The control changes nothing.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: RequestClose removes the tab or changes the selection, or raises CloseRequested for an id that is not open
    // Broiler-Human:        PENDING
    public void RequestClose(string id)
    {
        ThrowIfDisposed();
        if (FindTab(id) is { } tab)
            CloseRequested?.Invoke(this, new UiTabCloseRequestedEventArgs(tab));
    }

    /// <summary>
    /// Removes a tab. This is the host acting on a decision it has already
    /// made — the control never calls it in response to a gesture.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: removing the selected last tab leaves SelectedIndex equal to Tabs.Count, or removing a tab before the selected one moves the selection to a different tab
    // Broiler-Human:        PENDING
    public bool RemoveTab(string id)
    {
        ThrowIfDisposed();
        int index = IndexOf(id);
        if (index < 0)
            return false;

        UiTabItem tab = _tabs[index];
        tab.Changed -= OnTabChanged;
        _tabs.RemoveAt(index);
        if (tab.Content is not null)
            RemoveChild(tab.Content);

        // Focus moves deterministically: to the next tab, or to the previous
        // one when the closed tab was last, or nowhere when none remain. It
        // never lands on a removed element.
        if (_tabs.Count == 0)
            _selectedIndex = -1;
        else if (_selectedIndex > index || _selectedIndex >= _tabs.Count)
            _selectedIndex = Math.Clamp(_selectedIndex - 1, 0, _tabs.Count - 1);

        TabRemoved?.Invoke(this, new UiTabChangedEventArgs(tab));
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
            UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    /// <summary>Moves a tab, keeping the selection on the same tab.</summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the public MoveTab applies a reorder without raising Reordering, or applies it after a handler set Cancel
    // Broiler-Human:        PENDING
    public bool MoveTab(string id, int newIndex) => MoveTab(id, newIndex, raiseEvent: true);

    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: after a move the selection is on the tab now occupying the previously selected slot rather than on the tab selected before the move
    // Broiler-Human:        PENDING
    private bool MoveTab(string id, int newIndex, bool raiseEvent)
    {
        ThrowIfDisposed();
        int index = IndexOf(id);
        if (index < 0)
            return false;

        newIndex = Math.Clamp(newIndex, 0, _tabs.Count - 1);
        if (newIndex == index)
            return false;

        UiTabItem tab = _tabs[index];
        if (raiseEvent)
        {
            var args = new UiTabReorderingEventArgs(tab, index, newIndex);
            Reordering?.Invoke(this, args);
            if (args.Cancel)
                return false;
        }

        UiTabItem? selected = SelectedTab;
        _tabs.RemoveAt(index);
        _tabs.Insert(newIndex, tab);

        // The selection follows the tab, not the slot. Reordering must not
        // silently activate a different document.
        if (selected is not null)
            _selectedIndex = _tabs.IndexOf(selected);

        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an id matched under a culture or case-insensitive comparison returns the index of another tab, so RemoveTab closes a different document
    // Broiler-Human:        PENDING
    private int IndexOf(string id)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            if (StringComparer.Ordinal.Equals(_tabs[i].Id, id))
                return i;
        }

        return -1;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a header or dirty change raises TabChanged without invalidating Render and Semantic, so the strip keeps the old label
    // Broiler-Human:        PENDING
    private void OnTabChanged(UiTabItem tab)
    {
        TabChanged?.Invoke(this, new UiTabChangedEventArgs(tab));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an index below zero or equal to Tabs.Count changes SelectedIndex and raises SelectionChanged
    // Broiler-Human:        PENDING
    public bool SelectIndex(int index)
    {
        ThrowIfDisposed();
        if ((uint)index >= (uint)_tabs.Count)
            return false;
        if (_selectedIndex == index)
            return false;

        int oldIndex = _selectedIndex;
        _selectedIndex = index;
        SelectionChanged?.Invoke(this, new UiTabSelectionChangedEventArgs(oldIndex, _selectedIndex));
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the semantic node of the tab view has a child count different from Tabs.Count or a name other than the header of the selected tab
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.TabView,
            SelectedTab?.Header ?? string.Empty,
            Bounds,
            CreateSemanticState(),
            CreateTabSemanticNodes());

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a collapsed or hidden tab view reports the Visible or Enabled state
    // Broiler-Human:        PENDING
    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        return state;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the accessible name of a dirty tab does not mention unsaved changes, or a tab at index VisibleTabCapacity lacks the Offscreen state
    // Broiler-Human:        PENDING
    private IReadOnlyList<UiSemanticNode> CreateTabSemanticNodes()
    {
        var nodes = new List<UiSemanticNode>(_tabs.Count);
        for (int index = 0; index < _tabs.Count; index++)
        {
            UiTabItem tab = _tabs[index];
            UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
            if (index == SelectedIndex)
                state |= UiSemanticState.Selected;

            // A tab beyond the visible capacity is reachable but not on screen.
            if (index >= VisibleTabCapacity)
                state |= UiSemanticState.Offscreen;

            // Dirty state is spoken, not just drawn. A marker a screen reader
            // cannot see is a marker half the users do not have.
            string name = tab.IsDirty
                ? $"{tab.Header}, unsaved changes, {index + 1} of {_tabs.Count}"
                : $"{tab.Header}, {index + 1} of {_tabs.Count}";
            nodes.Add(new UiSemanticNode(UiSemanticRole.Generic, name, Bounds, state, []));
        }

        return nodes;
    }
}
