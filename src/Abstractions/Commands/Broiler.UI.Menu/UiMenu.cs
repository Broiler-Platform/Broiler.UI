using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.Menu;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: InvokeSelected raises ItemInvoked for a selected item that is a separator, is disabled, or has children
// Broiler-Human:        PENDING
public abstract class UiMenu : UiElement, IUiExpandable
{
    private IReadOnlyList<UiMenuItem> _items = [];
    private IReadOnlyList<int> _selectedPath = [];
    private bool _isOpen;
    private UiMenuPresentationMode _presentationMode;
    private int _maxDepth = 4;
    private BSize _preferredSize = new(320, 28);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiMenuItemInvokedEventArgs>? ItemInvoked;

    public IReadOnlyList<UiMenuItem> Items => _items;

    public IReadOnlyList<int> SelectedPath => _selectedPath;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool IsOpen
    {
        get => _isOpen;
        protected set
        {
            if (_isOpen == value)
                return;

            _isOpen = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an undefined UiMenuPresentationMode value is stored instead of being refused
    // Broiler-Human:        PENDING
    public UiMenuPresentationMode PresentationMode
    {
        get => _presentationMode;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_presentationMode == value)
                return;

            _presentationMode = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: after MaxDepth is lowered below the length of SelectedPath, SelectedPath still holds more than MaxDepth indices
    // Broiler-Human:        PENDING
    public int MaxDepth
    {
        get => _maxDepth;
        set
        {
            ThrowIfDisposed();
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum menu depth must be positive.");
            _maxDepth = value;
            if (_selectedPath.Count > _maxDepth)
                SetSelectedPath(_selectedPath.Take(_maxDepth));
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
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred menu size must be non-negative.");
            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a SelectedPath chosen against the previous items survives SetItems and indexes into the new list
    // Broiler-Human:        PENDING
    public void SetItems(IEnumerable<UiMenuItem> items)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
        _selectedPath = [];
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: Open on a menu with no items sets IsOpen to true
    // Broiler-Human:        PENDING
    public bool Open()
    {
        ThrowIfDisposed();
        if (Items.Count == 0)
            return false;
        if (SelectedPath.Count == 0)
            SetSelectedPath([0]);
        IsOpen = true;
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Close returns true on a menu that was not open
    // Broiler-Human:        PENDING
    public bool Close()
    {
        ThrowIfDisposed();
        if (!IsOpen)
            return false;
        IsOpen = false;
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a path whose index at some depth is out of range for that level is stored as SelectedPath
    // Broiler-Human:        PENDING
    public bool SetSelectedPath(IEnumerable<int> path)
    {
        ThrowIfDisposed();
        int[] copy = path.Take(MaxDepth).ToArray();
        if (copy.Length == 0 || GetItem(copy) is null)
            return false;
        if (_selectedPath.SequenceEqual(copy))
            return false;

        _selectedPath = copy;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a negative or past-the-end index at any depth of the path reaches the item indexer instead of returning null
    // Broiler-Human:        PENDING
    public UiMenuItem? GetItem(IReadOnlyList<int> path)
    {
        IReadOnlyList<UiMenuItem> current = Items;
        UiMenuItem? item = null;
        for (int depth = 0; depth < path.Count; depth++)
        {
            int index = path[depth];
            if ((uint)index >= (uint)current.Count)
                return null;

            item = current[index];
            current = item.Children.ToArray();
        }

        return item;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an enabled leaf whose parent item is disabled raises ItemInvoked
    // Broiler-Human:        PENDING
    protected bool InvokeSelected()
    {
        UiMenuItem? item = GetItem(SelectedPath);
        if (item is null || item.IsSeparator || !item.IsEnabled || item.Children.Count > 0)
            return false;

        if (item.IsCheckable)
            item.IsChecked = !item.IsChecked;
        ItemInvoked?.Invoke(this, new UiMenuItemInvokedEventArgs(item, SelectedPath.ToArray()));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the menu's semantic node carries no child node for its items, so assistive technology sees an empty menu
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Menu,
            PresentationMode.ToString(),
            Bounds,
            CreateSemanticState(),
            []);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an open menu's semantic state lacks Expanded
    // Broiler-Human:        PENDING
    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        state |= IsOpen ? UiSemanticState.Expanded : UiSemanticState.Collapsed;
        return state;
    }

    bool IUiExpandable.IsExpanded => IsOpen;

    // Open reports success on a menu that is already open; the interface reports a change.
    bool IUiExpandable.Expand() => !IsOpen && Open();

    bool IUiExpandable.Collapse() => Close();
}
