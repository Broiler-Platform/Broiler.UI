using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.TreeView;

// Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public enum TreeSelectionMode
{
    Single,

    /// <summary>Shift and control extend the selection.</summary>
    Extended,
}

/// <summary>One row of the flattened, expanded tree.</summary>
// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public readonly record struct TreeRow(TreeNodeId Id, int Depth, bool HasChildren, bool IsExpanded);

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class TreeSelectionChangedEventArgs(IReadOnlyList<TreeNodeId> selection) : EventArgs
{
    public IReadOnlyList<TreeNodeId> Selection { get; } = selection;
}

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class TreeNodeEventArgs(TreeNodeId node) : EventArgs
{
    public TreeNodeId Node { get; } = node;
}

/// <summary>
/// Platform-neutral state and commands for a virtualized tree.
///
/// The control materializes rows for the expanded set, not for the whole tree,
/// and the renderer paints only the visible slice of those. Expanding a node
/// with ten thousand children costs ten thousand row structs and forty painted
/// rows — never ten thousand elements.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
// Broiler-Falsified-If: collapsing an expanded ancestor of the focused node after Rows has been built leaves FocusedNode, and so Enter's NodeActivated, on a node with no visible row
// Broiler-Human:        PENDING
public abstract class UiTreeView : UiElement
{
    private readonly HashSet<TreeNodeId> _expanded = [];
    private readonly List<TreeNodeId> _selection = [];
    private readonly List<TreeRow> _rows = [];
    private ITreeDataSource? _dataSource;
    private TreeSelectionMode _selectionMode = TreeSelectionMode.Single;
    private BSize _preferredSize = new(280, 400);
    private TreeNodeId _focused = TreeNodeId.None;
    private int _firstVisibleRow;
    private TreeSecondaryLabelPlacement _secondaryLabelPlacement;
    private bool _rowsValid;

    protected UiTreeView()
    {
        Focusable = true;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<TreeSelectionChangedEventArgs>? SelectionChanged;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<TreeNodeEventArgs>? NodeExpanded;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<TreeNodeEventArgs>? NodeCollapsed;

    /// <summary>A node was activated — double-click or Enter.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<TreeNodeEventArgs>? NodeActivated;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: replacing an observable DataSource leaves the view subscribed to the old source's DataChanged, so a change there still invalidates this view
    // Broiler-Human:        PENDING
    public ITreeDataSource? DataSource
    {
        get => _dataSource;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_dataSource, value))
                return;

            if (_dataSource is IObservableTreeDataSource oldObservable)
                oldObservable.DataChanged -= OnDataChanged;
            _dataSource = value;
            if (_dataSource is IObservableTreeDataSource newObservable)
                newObservable.DataChanged += OnDataChanged;

            // Expansion and selection are kept: they are keyed by ID, and a
            // refresh that replaced the node objects should not collapse the
            // tree the user has arranged.
            InvalidateRows();
        }
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: switching SelectionMode to Single while several nodes are selected leaves more than one node in Selection
    // Broiler-Human:        PENDING
    public TreeSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            ThrowIfDisposed();
            if (_selectionMode == value)
                return;
            _selectionMode = value;
            if (value == TreeSelectionMode.Single && _selection.Count > 1)
                SetSelection([_selection[^1]]);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a size whose width or height is NaN passes the non-negative check and is stored as the preferred size
    // Broiler-Human:        PENDING
    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_preferredSize == value)
                return;
            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>The flattened expanded set. Rebuilt lazily.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: Rows read after Expand, Collapse, a DataSource change or DataChanged returns the row list from before that change
    // Broiler-Human:        PENDING
    public IReadOnlyList<TreeRow> Rows
    {
        get
        {
            EnsureRows();
            return _rows;
        }
    }

    public IReadOnlyList<TreeNodeId> Selection => _selection;

    /// <summary>
    /// The single tab stop. Focus never moves to a node that is not in the
    /// expanded set, so it cannot land on a row that has scrolled out of
    /// existence.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public TreeNodeId FocusedNode => _focused;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a negative value, or one at or beyond Rows.Count on a non-empty tree, is stored instead of clamped to [0, Rows.Count - 1]
    // Broiler-Human:        PENDING
    public int FirstVisibleRow
    {
        get => _firstVisibleRow;
        set
        {
            ThrowIfDisposed();
            EnsureRows();
            int clamped = Math.Clamp(value, 0, Math.Max(0, _rows.Count - 1));
            if (_firstVisibleRow == clamped)
                return;
            _firstVisibleRow = clamped;

            // Semantic as well: the rows in view are the tree's semantic children, so scrolling (the
            // wheel, the bar, a row brought into view) changes them, and a host that only heard about
            // a render kept exposing the rows that had scrolled away.
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>How many rows the current bounds can show.</summary>
    public int VisibleRowCapacity { get; set; } = 20;

    /// <summary>
    /// Where a row draws its secondary label. See
    /// <see cref="TreeSecondaryLabelPlacement"/> for which trees want which.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: changing SecondaryLabelPlacement does not call OnSecondaryLabelPlacementChanged or does not invalidate Measure, so hit tests keep dividing by the old row height
    // Broiler-Human:        PENDING
    public TreeSecondaryLabelPlacement SecondaryLabelPlacement
    {
        get => _secondaryLabelPlacement;
        set
        {
            ThrowIfDisposed();
            if (_secondaryLabelPlacement == value)
                return;

            _secondaryLabelPlacement = value;
            OnSecondaryLabelPlacementChanged();

            // Measure as well as render: an implementation that puts the second
            // label on its own line makes every row taller, and the row height
            // is what the arranged bounds are divided by to decide how many
            // rows fit and which one a click landed on.
            Invalidate(
                UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
                UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// Called when <see cref="SecondaryLabelPlacement"/> changes, so an
    /// implementation that caches a row height from it can recompute one.
    ///
    /// A hook rather than leaving it to be noticed at render time: the row
    /// height answers a hit test, and a stale one sends a click to the wrong
    /// row before anything has been drawn.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual void OnSecondaryLabelPlacementChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool IsExpanded(TreeNodeId node) => _expanded.Contains(node);

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Expand returns true and raises NodeExpanded for TreeNodeId.None, for a node whose CanExpand is false, or for a node already expanded
    // Broiler-Human:        PENDING
    public bool Expand(TreeNodeId node)
    {
        ThrowIfDisposed();
        if (_dataSource is null || node.IsNone || !_dataSource.CanExpand(node) || !_expanded.Add(node))
            return false;
        InvalidateRows();
        NodeExpanded?.Invoke(this, new TreeNodeEventArgs(node));
        return true;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: collapsing an expanded ancestor of the focused node after Rows has been built leaves FocusedNode on the hidden descendant instead of the collapsed node
    // Broiler-Human:        PENDING
    public bool Collapse(TreeNodeId node)
    {
        ThrowIfDisposed();
        if (!_expanded.Remove(node))
            return false;

        InvalidateRows();

        // Focus inside a collapsed subtree would be focus on nothing, so it
        // moves to the node the user just collapsed.
        if (!_focused.IsNone && !_rows.Any(row => row.Id == _focused))
            _focused = node;

        NodeCollapsed?.Invoke(this, new TreeNodeEventArgs(node));
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: ToggleExpansion on an expanded node leaves it expanded, or on a collapsed expandable node leaves it collapsed
    // Broiler-Human:        PENDING
    public bool ToggleExpansion(TreeNodeId node) =>
        IsExpanded(node) ? Collapse(node) : Expand(node);

    /// <summary>Expands every ancestor so a node becomes visible, then focuses it.</summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: with VisibleRowCapacity at least 1, RevealNode returns true while the node's row is outside [FirstVisibleRow, FirstVisibleRow + VisibleRowCapacity)
    // Broiler-Human:        PENDING
    public bool RevealNode(TreeNodeId node, IReadOnlyList<TreeNodeId> ancestors)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(ancestors);
        foreach (TreeNodeId ancestor in ancestors)
            Expand(ancestor);

        EnsureRows();
        int index = _rows.FindIndex(row => row.Id == node);
        if (index < 0)
            return false;

        _focused = node;
        SetSelection([node]);
        EnsureRowVisible(index);
        return true;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: SetSelection with a node that has no row in Rows, such as a file inside a collapsed folder, moves FocusedNode onto that hidden node
    // Broiler-Human:        PENDING
    public void SetSelection(IReadOnlyList<TreeNodeId> nodes)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(nodes);

        var next = _selectionMode == TreeSelectionMode.Single && nodes.Count > 1
            ? [nodes[^1]]
            : nodes.Distinct().ToList();
        if (next.SequenceEqual(_selection))
            return;

        _selection.Clear();
        _selection.AddRange(next);
        if (_selection.Count > 0)
            _focused = _selection[^1];

        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        SelectionChanged?.Invoke(this, new TreeSelectionChangedEventArgs([.. _selection]));
    }

    /// <summary>Moves focus by row, optionally extending the selection.</summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: Down on the last row or Up on the first row changes focus or selection instead of returning false
    // Broiler-Human:        PENDING
    public bool MoveFocus(int delta, bool extendSelection)
    {
        ThrowIfDisposed();
        EnsureRows();
        if (_rows.Count == 0)
            return false;

        int current = _focused.IsNone ? -1 : _rows.FindIndex(row => row.Id == _focused);
        int target = Math.Clamp(current + delta, 0, _rows.Count - 1);
        if (target == current)
            return false;

        TreeNodeId node = _rows[target].Id;
        _focused = node;
        if (extendSelection && _selectionMode == TreeSelectionMode.Extended)
        {
            var extended = new List<TreeNodeId>(_selection);
            if (!extended.Contains(node))
                extended.Add(node);
            SetSelection(extended);
        }
        else
        {
            SetSelection([node]);
        }

        EnsureRowVisible(target);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: MoveFocusToFirst focuses a row other than the first row of Rows
    // Broiler-Human:        PENDING
    public bool MoveFocusToFirst() => MoveFocusTo(0);

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: MoveFocusToLast focuses a row other than the last row of Rows, or returns true on a tree with no rows
    // Broiler-Human:        PENDING
    public bool MoveFocusToLast()
    {
        EnsureRows();
        return MoveFocusTo(_rows.Count - 1);
    }

    /// <summary>
    /// Selects the next node whose label starts with <paramref name="prefix"/>,
    /// searching from the focused row and wrapping. Only the expanded set is
    /// searched: type-ahead that opened collapsed folders would move the user
    /// somewhere they cannot see.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: extending a prefix the focused row still matches, such as from "r" to "re" with Report focused above Resources, moves focus off Report
    // Broiler-Human:        PENDING
    public bool TypeAhead(string prefix)
    {
        ThrowIfDisposed();
        if (_dataSource is null || string.IsNullOrEmpty(prefix))
            return false;

        EnsureRows();
        int start = _focused.IsNone ? 0 : _rows.FindIndex(row => row.Id == _focused) + 1;
        for (int offset = 0; offset < _rows.Count; offset++)
        {
            int index = (start + offset) % _rows.Count;
            string label = _dataSource.GetPresentation(_rows[index].Id).Label;
            if (label.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                return MoveFocusTo(index);
        }

        return false;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: ActivateFocused with no focused node raises NodeActivated
    // Broiler-Human:        PENDING
    public void ActivateFocused()
    {
        ThrowIfDisposed();
        ActivateNode(_focused);
    }

    /// <summary>
    /// Activates a named node, for a route that is not the focused row: the
    /// double click, which activates what the pointer is over, and a host
    /// command that acts on a row it already has in hand.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: ActivateNode with TreeNodeId.None or default(TreeNodeId) raises NodeActivated
    // Broiler-Human:        PENDING
    public void ActivateNode(TreeNodeId node)
    {
        ThrowIfDisposed();
        if (!node.IsNone)
            NodeActivated?.Invoke(this, new TreeNodeEventArgs(node));
    }

    /// <summary>Rebuilds the expanded set after the data source changed.</summary>
    public void Refresh() => InvalidateRows();

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after the view is disposed, raising DataChanged on its observable DataSource still reaches this view
    // Broiler-Human:        PENDING
    protected override void Dispose(bool disposing)
    {
        if (disposing && _dataSource is IObservableTreeDataSource observable)
            observable.DataChanged -= OnDataChanged;
        base.Dispose(disposing);
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Low; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a row outside [FirstVisibleRow, FirstVisibleRow + VisibleRowCapacity) appears among the tree's semantic children, or a visible selected row lacks the Selected state
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore()
    {
        EnsureRows();
        var children = new List<UiSemanticNode>();

        // Only the visible slice is described. A screen reader asking for the
        // node list of a 50,000-row tree must not receive 50,000 nodes; it
        // walks the visible window and scrolls like a sighted user does.
        int last = Math.Min(_rows.Count, _firstVisibleRow + VisibleRowCapacity);
        for (int i = _firstVisibleRow; i < last; i++)
        {
            TreeRow row = _rows[i];
            UiSemanticState state = UiSemanticState.Visible | UiSemanticState.Enabled;
            if (_selection.Contains(row.Id))
                state |= UiSemanticState.Selected;
            if (row.IsExpanded)
                state |= UiSemanticState.Expanded;
            if (_focused == row.Id)
                state |= UiSemanticState.Focused;

            children.Add(new UiSemanticNode(
                UiSemanticRole.ListView, DescribeRow(row, i), Bounds, state, []));
        }

        return new UiSemanticNode(
            UiSemanticRole.ListView,
            $"Tree, {_rows.Count} items",
            Bounds,
            Visibility == UiVisibility.Visible
                ? UiSemanticState.Visible | UiSemanticState.Enabled
                : UiSemanticState.None,
            children);
    }

    /// <summary>
    /// What a screen reader announces for a row. Level and position within the
    /// level are stated explicitly, because a tree's structure is not otherwise
    /// conveyed by a flat list of names.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Low; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a row's description omits its level, its position within its level, or its expanded or collapsed state
    // Broiler-Human:        PENDING
    protected virtual string DescribeRow(TreeRow row, int rowIndex)
    {
        if (_dataSource is null)
            return string.Empty;

        TreeNodePresentation presentation = _dataSource.GetPresentation(row.Id);
        (int position, int count) = PositionWithinLevel(rowIndex, row.Depth);
        string decoration = presentation.Decoration switch
        {
            TreeNodeDecoration.Dirty => ", unsaved changes",
            TreeNodeDecoration.Error => ", error",
            TreeNodeDecoration.Warning => ", warning",
            TreeNodeDecoration.Information => ", information",
            _ => string.Empty,
        };
        string expansion = row.HasChildren ? (row.IsExpanded ? ", expanded" : ", collapsed") : string.Empty;

        return $"{presentation.Label}{decoration}{expansion}, level {row.Depth + 1}, " +
            $"{position} of {count}";
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a row whose earlier sibling is expanded is given a position that counts that sibling's children
    // Broiler-Human:        PENDING
    private (int Position, int Count) PositionWithinLevel(int rowIndex, int depth)
    {
        // Counted by walking outwards over siblings at the same depth, which
        // stops at the first shallower row in each direction.
        int position = 1;
        for (int i = rowIndex - 1; i >= 0 && _rows[i].Depth >= depth; i--)
        {
            if (_rows[i].Depth == depth)
                position++;
        }

        int count = position;
        for (int i = rowIndex + 1; i < _rows.Count && _rows[i].Depth >= depth; i++)
        {
            if (_rows[i].Depth == depth)
                count++;
        }

        return (position, count);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: an index below zero or at or beyond Rows.Count changes FocusedNode or the selection
    // Broiler-Human:        PENDING
    private bool MoveFocusTo(int index)
    {
        EnsureRows();
        if (index < 0 || index >= _rows.Count)
            return false;
        _focused = _rows[index].Id;
        SetSelection([_focused]);
        EnsureRowVisible(index);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: with VisibleRowCapacity at least 1, the row at index is still before FirstVisibleRow or at or after FirstVisibleRow + VisibleRowCapacity after the call
    // Broiler-Human:        PENDING
    private void EnsureRowVisible(int index)
    {
        if (index < _firstVisibleRow)
            FirstVisibleRow = index;
        else if (index >= _firstVisibleRow + VisibleRowCapacity)
            FirstVisibleRow = index - VisibleRowCapacity + 1;
    }

    private void OnDataChanged(object? sender, TreeDataChangedEventArgs e) => InvalidateRows();

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: InvalidateRows leaves the rows marked valid, so Rows keeps returning the list from before the change
    // Broiler-Human:        PENDING
    private void InvalidateRows()
    {
        _rowsValid = false;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
            UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: after the expanded set shrinks, FirstVisibleRow stays at or beyond Rows.Count while Rows is non-empty
    // Broiler-Human:        PENDING
    private void EnsureRows()
    {
        if (_rowsValid)
            return;

        _rows.Clear();
        if (_dataSource is not null)
            AppendChildren(_dataSource.Root, depth: 0);
        _rowsValid = true;
        _firstVisibleRow = Math.Clamp(_firstVisibleRow, 0, Math.Max(0, _rows.Count - 1));
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a child of a node that is not expanded, or whose CanExpand is false, is added to Rows
    // Broiler-Human:        PENDING
    private void AppendChildren(TreeNodeId parent, int depth)
    {
        if (_dataSource is null)
            return;

        int count = _dataSource.GetChildCount(parent);
        for (int i = 0; i < count; i++)
        {
            TreeNodeId child = _dataSource.GetChild(parent, i);
            bool canExpand = _dataSource.CanExpand(child);
            bool expanded = canExpand && _expanded.Contains(child);
            _rows.Add(new TreeRow(child, depth, canExpand, expanded));

            // Only expanded subtrees are walked. A collapsed folder is one row
            // whatever it contains, which is what keeps opening a workspace
            // proportional to what is on screen.
            if (expanded)
                AppendChildren(child, depth + 1);
        }
    }
}
