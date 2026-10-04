using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.ComboBox;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: SelectIndex stores an index below -1 or at least Items.Count as SelectedIndex
// Broiler-Human:        PENDING
public abstract class UiComboBox : UiElement, IUiExpandable
{
    private IReadOnlyList<UiComboBoxItem> _items = [];
    private int _selectedIndex = -1;
    private bool _isDropDownOpen;
    private bool _isEnabled = true;
    private BSize _preferredSize = new(180, 32);
    private int _maxDropDownItems = 8;

    protected UiComboBox()
    {
        Focusable = true;
    }

    public override bool CanFocus => base.CanFocus && IsEnabled;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiComboBoxSelectionChangedEventArgs>? SelectionChanged;

    public IReadOnlyList<UiComboBoxItem> Items => _items;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning SelectedIndex a value equal to Items.Count stores it as the selection
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
    // Broiler-Falsified-If: a SelectedIndex of -1 or one past the end reaches the Items indexer instead of returning null
    // Broiler-Human:        PENDING
    public UiComboBoxItem? SelectedItem =>
        (uint)SelectedIndex < (uint)Items.Count ? Items[SelectedIndex] : null;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool IsDropDownOpen
    {
        get => _isDropDownOpen;
        protected set
        {
            if (_isDropDownOpen == value)
                return;

            _isDropDownOpen = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: disabling the combo box while the drop-down is open leaves IsDropDownOpen true
    // Broiler-Human:        PENDING
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
                CloseDropDown();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
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
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred combo box size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a zero or negative value is stored as the maximum drop-down item count
    // Broiler-Human:        PENDING
    public int MaxDropDownItems
    {
        get => _maxDropDownItems;
        set
        {
            ThrowIfDisposed();
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum drop-down items must be positive.");
            _maxDropDownItems = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: after SetItems shrinks the list to fewer than SelectedIndex + 1 items, SelectedIndex still points past the new last item
    // Broiler-Human:        PENDING
    public void SetItems(IEnumerable<UiComboBoxItem> items)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(items);
        UiComboBoxItem[] copy = items.ToArray();
        if (copy.Any(static item => string.IsNullOrWhiteSpace(item.Id)))
            throw new ArgumentException("ComboBox item IDs must be non-empty.", nameof(items));
        if (copy.Select(static item => item.Id).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("ComboBox item IDs must be unique.", nameof(items));

        _items = copy;
        if (_selectedIndex >= copy.Length)
            SelectIndex(copy.Length == 0 ? -1 : copy.Length - 1);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: SelectIndex(Items.Count) returns true and stores an index one past the last item
    // Broiler-Human:        PENDING
    public bool SelectIndex(int index)
    {
        ThrowIfDisposed();
        if (index < -1 || index >= Items.Count)
            return false;
        if (_selectedIndex == index)
            return false;

        int oldIndex = _selectedIndex;
        _selectedIndex = index;
        SelectionChanged?.Invoke(this, new UiComboBoxSelectionChangedEventArgs(oldIndex, _selectedIndex));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: OpenDropDown returns true on a combo box whose IsEnabled is false or that has no items
    // Broiler-Human:        PENDING
    public bool OpenDropDown()
    {
        ThrowIfDisposed();
        if (!IsEnabled || IsDropDownOpen || Items.Count == 0)
            return false;

        IsDropDownOpen = true;
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: CloseDropDown returns true when the drop-down was not open
    // Broiler-Human:        PENDING
    public bool CloseDropDown()
    {
        ThrowIfDisposed();
        if (!IsDropDownOpen)
            return false;

        IsDropDownOpen = false;
        return true;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the ComboBox node's name is something other than the selected item's Text, or is not empty when nothing is selected
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ComboBox,
            SelectedItem?.Text ?? string.Empty,
            Bounds,
            CreateSemanticState(),
            []);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an open combo box's semantic state lacks Expanded
    // Broiler-Human:        PENDING
    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        state |= IsDropDownOpen ? UiSemanticState.Expanded : UiSemanticState.Collapsed;
        return state;
    }

    // The drop-down is the content a combo box shows and hides.
    bool IUiExpandable.IsExpanded => IsDropDownOpen;

    bool IUiExpandable.Expand() => OpenDropDown();

    bool IUiExpandable.Collapse() => CloseDropDown();
}
