using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;

namespace Broiler.UI.Toolbar;

public abstract class UiToolbar : UiElement
{
    private readonly Dictionary<UiElement, UiToolbarBreak> _breakBefore = [];
    private string _title = string.Empty;
    private UiToolbarOrientation _orientation;
    private UiToolbarOverflow _overflow;
    private bool _isOverflowOpen;
    private bool _isEnabled = true;
    private double _spacing = 6;
    private double _padding = 6;
    private BSize _preferredSize = new(0, 42);

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
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    public UiToolbarOrientation Orientation
    {
        get => _orientation;
        set
        {
            ThrowIfDisposed();
            if (_orientation == value)
                return;

            _orientation = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// What becomes of the items that do not fit. The default is
    /// <see cref="UiToolbarOverflow.Menu"/>: they move into a drop-down opened
    /// from a chevron at the end of the bar, so a narrow bar hides no command.
    /// </summary>
    public UiToolbarOverflow Overflow
    {
        get => _overflow;
        set
        {
            ThrowIfDisposed();
            if (_overflow == value)
                return;

            _overflow = value;
            if (value != UiToolbarOverflow.Menu)
                CloseOverflow();
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>Whether the overflow drop-down is showing.</summary>
    public bool IsOverflowOpen
    {
        get => _isOverflowOpen;
        protected set
        {
            if (_isOverflowOpen == value)
                return;

            _isOverflowOpen = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// Shows the overflow drop-down. False when the bar is not in
    /// <see cref="UiToolbarOverflow.Menu"/> mode, or is already showing it. An
    /// implementation refuses as well when nothing has overflowed, which it is
    /// the one that knows.
    /// </summary>
    public virtual bool OpenOverflow()
    {
        ThrowIfDisposed();
        if (!IsEnabled || _overflow != UiToolbarOverflow.Menu || _isOverflowOpen)
            return false;

        IsOverflowOpen = true;
        return true;
    }

    public bool CloseOverflow()
    {
        ThrowIfDisposed();
        if (!_isOverflowOpen)
            return false;

        IsOverflowOpen = false;
        return true;
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            ThrowIfDisposed();
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            if (!value)
                CloseOverflow();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public double Spacing
    {
        get => _spacing;
        set
        {
            ThrowIfDisposed();
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Toolbar spacing must be non-negative.");
            if (_spacing.Equals(value))
                return;

            _spacing = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double Padding
    {
        get => _padding;
        set
        {
            ThrowIfDisposed();
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Toolbar padding must be non-negative.");
            if (_padding.Equals(value))
                return;

            _padding = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred toolbar size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>
    /// Starts a group in front of <paramref name="child"/>. <see cref="UiToolbarBreak.Gap"/> opens
    /// the space without drawing a rule in it, which is usually enough to group a bar and adds no
    /// ink to it.
    /// </summary>
    public void SetBreakBefore(UiElement child, UiToolbarBreak kind)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        if (!Children.Contains(child))
            throw new InvalidOperationException("Toolbar separator metadata can only be assigned to a child of this toolbar.");

        UiToolbarBreak current = GetBreakBefore(child);
        if (current == kind)
            return;

        if (kind == UiToolbarBreak.None)
            _breakBefore.Remove(child);
        else
            _breakBefore[child] = kind;

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>What the bar puts in front of <paramref name="child"/>, if anything.</summary>
    public UiToolbarBreak GetBreakBefore(UiElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return _breakBefore.TryGetValue(child, out UiToolbarBreak kind) ? kind : UiToolbarBreak.None;
    }

    /// <summary>
    /// Starts a ruled group in front of <paramref name="child"/>. Shorthand for
    /// <see cref="SetBreakBefore"/> with <see cref="UiToolbarBreak.Separator"/>.
    /// </summary>
    public void SetSeparatorBefore(UiElement child, bool hasSeparator) =>
        SetBreakBefore(child, hasSeparator ? UiToolbarBreak.Separator : UiToolbarBreak.None);

    /// <summary>Whether a rule is drawn in front of <paramref name="child"/>.</summary>
    public bool GetSeparatorBefore(UiElement child) =>
        GetBreakBefore(child) == UiToolbarBreak.Separator;

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Toolbar,
            string.IsNullOrWhiteSpace(Title) ? GetType().Name : Title,
            Bounds,
            CreateSemanticState(),
            CreateChildSemanticNodes());

    protected override void OnChildRemoved(UiElement child)
    {
        _breakBefore.Remove(child);
    }

    private UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
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
}
