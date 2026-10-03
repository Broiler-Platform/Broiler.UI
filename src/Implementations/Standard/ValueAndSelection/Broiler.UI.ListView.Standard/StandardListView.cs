using System;
using System.Collections.Generic;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.ListView.Standard;

public sealed class StandardListView : UiListView, IStandardThemedControl
{
    private BRect _contentBounds = BRect.Empty;
    private BRect _scrollbarTrackBounds = BRect.Empty;
    private double _scrollbarThickness = 12;
    private double _minimumScrollbarThumbLength = 18;
    private bool _showScrollbar = true;
    private bool _isDraggingScrollbar;
    private double _dragPointerOffsetWithinThumb;
    private bool _itemHeightExplicit;
    private bool _isHighContrast;
    private static readonly TimeSpan DoubleClickWindow = TimeSpan.FromMilliseconds(400);
    private UiTimestamp _lastClickTime;
    private string? _lastClickItemId;

    public StandardListView()
    {
        ItemPresenter = DefaultListItemPresenter.Instance;
    }

    public void ApplyTheme(StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        // Follow the theme's body font, including a text-scaled theme, unless the application set its own.
        BFontStyle themeFont = StandardThemeFonts.For(theme, StandardTextStyle.Body);
        BFontStyle followed = StandardThemeFonts.Follow(Font, _themeFont, themeFont);
        _themeFont = themeFont;
        if (followed != Font)
        {
            Font = followed;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
        Background = theme.Surface;
        Foreground = theme.Text;
        SecondaryForeground = theme.TextMuted;
        SelectedBackground = theme.AccentSoft;
        FocusRing = theme.FocusRing;
        BorderColor = theme.Border;
        Accent = theme.Accent;
        ScrollbarTrack = theme.SurfaceDisabled;
        ScrollbarThumb = theme.BorderStrong;
        _isHighContrast = Math.Abs(Luminance(theme.Surface) - Luminance(theme.Text)) > 0.9;
        if (!_itemHeightExplicit && ItemPresenter is not null)
        {
            _itemHeight = ItemPresenter.GetItemHeight(null, Density, _contentBounds.Width, Font);
        }
    }

    private static double Luminance(BColor c) =>
        ((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B)) / 255.0;

    private BColor _background = StandardControlPaint.Surface;
    private BColor _foreground = StandardControlPaint.Text;
    private BColor _secondaryForeground = StandardControlPaint.TextMuted;
    private BColor _selectedBackground = StandardControlPaint.AccentSoft;
    private BColor _focusRing = StandardControlPaint.Focus;
    private BColor _borderColor = StandardControlPaint.Border;
    private BColor _accent = StandardControlPaint.Accent;
    private BColor _scrollbarTrack = StandardControlPaint.SurfaceDisabled;
    private BColor _scrollbarThumb = StandardControlPaint.BorderStrong;
    private BFontStyle _font = StandardControlPaint.Theme.FontBody;
    private BFontStyle _themeFont = StandardControlPaint.Theme.FontBody;
    private double _itemHeight = 28;
    private double _cornerRadius = StandardControlPaint.ControlRadius;

    public BColor SecondaryForeground
    {
        get => _secondaryForeground;
        set
        {
            if (_secondaryForeground == value) return;
            _secondaryForeground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor Accent
    {
        get => _accent;
        set
        {
            if (_accent == value) return;
            _accent = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public double EffectiveItemHeight =>
        _itemHeightExplicit
            ? _itemHeight
            : (ItemPresenter?.GetItemHeight(null, Density, _contentBounds.Width, Font) ?? _itemHeight);

    public BColor Background
    {
        get => _background;
        set
        {
            if (_background == value) return;
            _background = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor Foreground
    {
        get => _foreground;
        set
        {
            if (_foreground == value) return;
            _foreground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor SelectedBackground
    {
        get => _selectedBackground;
        set
        {
            if (_selectedBackground == value) return;
            _selectedBackground = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor FocusRing
    {
        get => _focusRing;
        set
        {
            if (_focusRing == value) return;
            _focusRing = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor == value) return;
            _borderColor = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor ScrollbarTrack
    {
        get => _scrollbarTrack;
        set
        {
            if (_scrollbarTrack == value) return;
            _scrollbarTrack = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BColor ScrollbarThumb
    {
        get => _scrollbarThumb;
        set
        {
            if (_scrollbarThumb == value) return;
            _scrollbarThumb = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public BFontStyle Font
    {
        get => _font;
        set
        {
            if (_font == value) return;
            _font = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double ItemHeight
    {
        get => EffectiveItemHeight;
        set
        {
            if (_itemHeight == value && _itemHeightExplicit) return;
            _itemHeight = value;
            _itemHeightExplicit = true;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    protected override void OnDensityChanged()
    {
        base.OnDensityChanged();
        if (!_itemHeightExplicit && ItemPresenter is not null)
        {
            _itemHeight = ItemPresenter.GetItemHeight(null, Density, _contentBounds.Width, Font);
        }
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (_cornerRadius == value) return;
            _cornerRadius = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public double WheelScrollItems { get; set; } = 3;

    public double ScrollbarThickness
    {
        get => _scrollbarThickness;
        set
        {
            ThrowIfDisposed();
            ValidateNonNegativeFinite(value, nameof(value));
            if (_scrollbarThickness == value)
                return;

            _scrollbarThickness = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public double MinimumScrollbarThumbLength
    {
        get => _minimumScrollbarThumbLength;
        set
        {
            ThrowIfDisposed();
            ValidateNonNegativeFinite(value, nameof(value));
            if (_minimumScrollbarThumbLength == value)
                return;

            _minimumScrollbarThumbLength = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    public bool ShowScrollbar
    {
        get => _showScrollbar;
        set
        {
            ThrowIfDisposed();
            if (_showScrollbar == value)
                return;

            _showScrollbar = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BRect ContentBounds => _contentBounds;

    public bool HasVerticalScrollbar { get; private set; }

    public int FirstVisibleIndex { get; private set; }

    public int VisibleItemCount { get; private set; }

    protected override BSize MeasureCore(BSize availableSize) =>
        new(
            ClampDesired(PreferredSize.Width, availableSize.Width),
            ClampDesired(PreferredSize.Height, availableSize.Height));

    protected override void ArrangeCore(BRect finalRect)
    {
        HasVerticalScrollbar = ShouldShowVerticalScrollbar(finalRect.Height);
        double thickness = HasVerticalScrollbar ? Math.Min(ScrollbarThickness, Math.Max(0, finalRect.Width)) : 0;
        _contentBounds = new BRect(finalRect.Left, finalRect.Top, Math.Max(0, finalRect.Width - thickness), finalRect.Height);
        _scrollbarTrackBounds = HasVerticalScrollbar
            ? new BRect(_contentBounds.Right, finalRect.Top, thickness, finalRect.Height)
            : BRect.Empty;

        CoerceOffset();
        UpdateVisibleRange();
    }

    protected override void RenderCore(UiRenderContext context)
    {
        UpdateVisibleRange();
        StandardControlPaint.FillRounded(context.RenderList, Bounds, Background, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, Bounds, BorderColor, CornerRadius, 1);
        context.RenderList.PushClip(_contentBounds);

        IUiListItemPresenter presenter = ItemPresenter ?? DefaultListItemPresenter.Instance;
        int lastIndex = Math.Min(Items.Count, FirstVisibleIndex + VisibleItemCount);
        for (int index = FirstVisibleIndex; index < lastIndex; index++)
        {
            BRect row = GetItemBounds(index);
            UiListItem item = Items[index];
            var itemState = new UiListItemState(
                IsSelected(item.Id),
                Session?.FocusedElement == this && SelectedIndex == index,
                item.IsRead,
                index,
                Density);

            var renderContext = new UiListItemRenderContext
            {
                RenderList = context.RenderList,
                Bounds = row,
                Item = item,
                State = itemState,
                Font = Font,
                Foreground = Foreground,
                SecondaryForeground = SecondaryForeground,
                Background = Background,
                SelectedBackground = SelectedBackground,
                FocusRing = FocusRing,
                Accent = Accent,
                IsHighContrast = _isHighContrast,
            };

            presenter.Render(renderContext);
        }

        context.RenderList.PopClip();
        RenderScrollbar(context);

        if (Session?.FocusedElement == this)
            StandardControlPaint.StrokeRounded(context.RenderList, StandardControlPaint.Inset(Bounds, 2), FocusRing, Math.Max(0, CornerRadius - 2), 1);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        return input.Kind switch
        {
            UiInputEventKind.PointerMove => HandlePointerMove(input),
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerWheel => HandleWheel(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    protected override IReadOnlyList<UiSemanticNode> CreateVisibleSemanticNodes()
    {
        UpdateVisibleRange();
        IUiListItemPresenter presenter = ItemPresenter ?? DefaultListItemPresenter.Instance;
        var nodes = new List<UiSemanticNode>(VisibleItemCount);
        int lastIndex = Math.Min(Items.Count, FirstVisibleIndex + VisibleItemCount);
        for (int index = FirstVisibleIndex; index < lastIndex; index++)
        {
            UiListItem item = Items[index];
            var itemState = new UiListItemState(
                IsSelected(item.Id),
                Session?.FocusedElement == this && SelectedIndex == index,
                item.IsRead,
                index,
                Density);

            var semanticContext = new UiListItemSemanticContext
            {
                Item = item,
                State = itemState,
                Bounds = GetItemBounds(index),
                Index = index,
            };

            nodes.Add(presenter.CreateSemanticNode(semanticContext));
        }

        return nodes;
    }

    public override void ScrollIntoView(string itemId)
    {
        int index = IndexOf(itemId);
        if (index < 0)
            return;

        double top = index * EffectiveItemHeight;
        double bottom = top + EffectiveItemHeight;
        if (top < VerticalOffset)
            SetVerticalOffset(top);
        else if (bottom > VerticalOffset + ViewportHeight)
            SetVerticalOffset(bottom - ViewportHeight);

        CoerceOffset();
        UpdateVisibleRange();
    }

    public override bool MakeVisible(BRect targetRect)
    {
        if (_contentBounds.IsEmpty || targetRect.IsEmpty)
            return false;

        double delta = 0;
        if (targetRect.Top < _contentBounds.Top)
            delta = targetRect.Top - _contentBounds.Top;
        else if (targetRect.Bottom > _contentBounds.Bottom)
            delta = targetRect.Bottom - _contentBounds.Bottom;

        if (Math.Abs(delta) < 0.5)
            return false;

        SetVerticalOffset(VerticalOffset + delta);
        CoerceOffset();
        UpdateVisibleRange();
        return true;
    }

    protected override double GetItemHeightForAnchoring() => EffectiveItemHeight;
    protected override int GetFirstVisibleIndexForAnchoring() => FirstVisibleIndex;
    protected override BRect GetItemBoundsForAccessibility(int index) => GetItemBounds(index);
    protected override BRect ContentBoundsForAccessibility => _contentBounds.IsEmpty ? Bounds : _contentBounds;

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
            return HandlePointerDown(input.Position, input.KeyModifiers, input.Header.Timestamp);

        if (input.MouseButtonTransition == MouseButtonTransition.Up && _isDraggingScrollbar)
        {
            EndScrollbarDrag();
            return true;
        }

        return false;
    }

    private bool HandlePointerDown(BPoint position, KeyboardModifierState modifiers, InputTimestamp timestamp)
    {
        Session?.SetFocus(this);
        if (HasVerticalScrollbar && TryHandleScrollbarPointerDown(position))
            return true;

        if (!_contentBounds.Contains(position))
            return true;

        int index = (int)Math.Floor((VerticalOffset + position.Y - _contentBounds.Top) / Math.Max(1, EffectiveItemHeight));
        if ((uint)index < (uint)Items.Count)
        {
            string itemId = Items[index].Id;
            ApplyClick(itemId, modifiers);
            ScrollIntoView(itemId);

            var now = new UiTimestamp(TimeSpan.FromTicks(timestamp.Ticks));
            if (string.Equals(_lastClickItemId, itemId, StringComparison.Ordinal) &&
                (now.Elapsed - _lastClickTime.Elapsed) <= DoubleClickWindow)
            {
                ActivateItem(itemId);
                _lastClickItemId = null;
            }
            else
            {
                _lastClickItemId = itemId;
                _lastClickTime = now;
            }
        }

        return true;
    }

    private bool TryHandleScrollbarPointerDown(BPoint position)
    {
        if (_scrollbarTrackBounds.IsEmpty || !_scrollbarTrackBounds.Contains(position))
            return false;

        BRect thumb = GetScrollbarThumbBounds();
        if (thumb.Contains(position))
        {
            BeginScrollbarDrag(position, thumb);
            return true;
        }

        double pageDelta = ViewportHeight * 0.85;
        if (position.Y < thumb.Top)
            pageDelta = -pageDelta;

        SetVerticalOffset(VerticalOffset + pageDelta);
        CoerceOffset();
        return true;
    }

    private void BeginScrollbarDrag(BPoint position, BRect thumb)
    {
        _isDraggingScrollbar = true;
        _dragPointerOffsetWithinThumb = position.Y - thumb.Top;
        Session?.CaptureInput(this);
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (!_isDraggingScrollbar)
            return false;

        DragScrollbar(input.Position);
        return true;
    }

    private void DragScrollbar(BPoint position)
    {
        BRect thumb = GetScrollbarThumbBounds();
        double scrollableTrack = Math.Max(0, _scrollbarTrackBounds.Height - thumb.Height);
        if (scrollableTrack <= 0)
            return;

        double thumbTop = position.Y - _dragPointerOffsetWithinThumb;
        double normalized = (thumbTop - _scrollbarTrackBounds.Top) / scrollableTrack;
        SetVerticalOffset(Math.Clamp(normalized, 0, 1) * MaxVerticalOffset);
        CoerceOffset();
    }

    private void EndScrollbarDrag()
    {
        _isDraggingScrollbar = false;
        _dragPointerOffsetWithinThumb = 0;
        Session?.ReleaseInputCapture(this);
    }

    private bool HandleWheel(UiInputEvent input)
    {
        if (input.WheelAxis != MouseWheelAxis.Vertical)
            return false;

        double before = VerticalOffset;
        SetVerticalOffset(VerticalOffset - input.WheelDeltaNotches * EffectiveItemHeight * WheelScrollItems);
        CoerceOffset();
        return VerticalOffset != before;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        // Shift with an arrow extends the range, matching the pointer's Shift-click.
        bool extend = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);
        int index = SelectedIndex >= 0 ? SelectedIndex : 0;

        if (IsKey(input, BVirtualKey.Down, "Down"))
            return SelectAndReveal(SelectedIndex >= 0 ? Math.Min(Items.Count - 1, SelectedIndex + 1) : 0, extend);
        if (IsKey(input, BVirtualKey.Up, "Up"))
            return SelectAndReveal(SelectedIndex >= 0 ? Math.Max(0, SelectedIndex - 1) : 0, extend);
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return SelectAndReveal(0, extend);
        if (IsKey(input, BVirtualKey.End, "End"))
            return SelectAndReveal(Items.Count - 1, extend);
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
            return SelectAndReveal(Math.Min(Items.Count - 1, index + Math.Max(1, VisibleItemCount - 1)), extend);
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
            return SelectAndReveal(Math.Max(0, index - Math.Max(1, VisibleItemCount - 1)), extend);

        // Enter key activates the selected item
        if (IsKey(input, BVirtualKey.Enter, "Enter") || input.NativeKeyCode == 13)
        {
            if (SelectedIndex >= 0 && SelectedIndex < Items.Count)
            {
                ActivateItem(Items[SelectedIndex].Id);
                return true;
            }
        }

        // Space toggles without moving, which is the only way to build a
        // discontiguous selection from the keyboard alone.
        if (SelectionMode == UiListSelectionMode.Multiple &&
            IsKey(input, BVirtualKey.Space, "Space") &&
            SelectedIndex >= 0)
        {
            return ToggleItem(Items[SelectedIndex].Id);
        }

        // Type-ahead prefix jump
        if (!string.IsNullOrEmpty(input.KeyName) && input.KeyName.Length == 1 && !char.IsControl(input.KeyName[0]))
        {
            if (TypeAhead(input.KeyName))
                return true;
        }

        return false;
    }

    private bool SelectAndReveal(int index, bool extend = false)
    {
        if ((uint)index >= (uint)Items.Count)
            return false;

        Session?.SetFocus(this);
        string itemId = Items[index].Id;
        if (extend && SelectionMode == UiListSelectionMode.Multiple)
            SelectRangeTo(itemId);
        else
            SelectItem(itemId);

        ScrollIntoView(itemId);
        return true;
    }

    /// <summary>
    /// Applies a click: a single-selection list replaces its selection; a
    /// multi-selection list extends the range on Shift and otherwise toggles the row.
    /// </summary>
    /// <remarks>
    /// An unmodified click toggles rather than replacing, which is a deliberate
    /// departure from the desktop convention of replace-unless-Ctrl. A touch contact
    /// arrives as a synthesized pointer press and can never carry a modifier, so
    /// requiring Ctrl to accumulate would leave multi-selection unreachable by touch
    /// entirely. Ctrl-click therefore also toggles — the same result the convention
    /// gives it — and Shift-click is the range, so muscle memory still works.
    /// </remarks>
    private void ApplyClick(string itemId, KeyboardModifierState modifiers)
    {
        if (SelectionMode != UiListSelectionMode.Multiple)
        {
            SelectItem(itemId);
            return;
        }

        if (modifiers.HasFlag(KeyboardModifierState.Shift))
            SelectRangeTo(itemId);
        else
            ToggleItem(itemId);
    }

    private void CoerceOffset()
    {
        if (VerticalOffset > MaxVerticalOffset)
            SetVerticalOffset(MaxVerticalOffset);
    }

    private void UpdateVisibleRange()
    {
        double itemHeight = Math.Max(1, EffectiveItemHeight);
        FirstVisibleIndex = Math.Clamp((int)Math.Floor(VerticalOffset / itemHeight), 0, Math.Max(0, Items.Count));
        VisibleItemCount = Math.Min(Math.Max(0, Items.Count - FirstVisibleIndex), Math.Max(0, (int)Math.Ceiling(ViewportHeight / itemHeight) + 1));
    }

    private BRect GetItemBounds(int index) =>
        new(_contentBounds.Left, _contentBounds.Top + index * EffectiveItemHeight - VerticalOffset, _contentBounds.Width, EffectiveItemHeight);

    private void RenderScrollbar(UiRenderContext context)
    {
        if (!HasVerticalScrollbar)
            return;

        StandardControlPaint.FillRounded(context.RenderList, _scrollbarTrackBounds, ScrollbarTrack, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(context.RenderList, GetScrollbarThumbBounds(), ScrollbarThumb, StandardControlPaint.PillRadius);
    }

    private BRect GetScrollbarThumbBounds()
    {
        if (_scrollbarTrackBounds.IsEmpty)
            return BRect.Empty;

        double rawThumbHeight = _scrollbarTrackBounds.Height * (ViewportHeight / Math.Max(ViewportHeight, ExtentHeight));
        double minThumbHeight = Math.Min(Math.Max(0, MinimumScrollbarThumbLength), _scrollbarTrackBounds.Height);
        double thumbHeight = Math.Clamp(rawThumbHeight, minThumbHeight, _scrollbarTrackBounds.Height);
        double scrollableTrack = Math.Max(0, _scrollbarTrackBounds.Height - thumbHeight);
        double thumbTop = _scrollbarTrackBounds.Top + (MaxVerticalOffset <= 0 ? 0 : scrollableTrack * (VerticalOffset / MaxVerticalOffset));
        return new BRect(_scrollbarTrackBounds.Left, thumbTop, _scrollbarTrackBounds.Width, thumbHeight);
    }

    private bool ShouldShowVerticalScrollbar(double viewportHeight) =>
        ShowScrollbar && ScrollbarThickness > 0 && viewportHeight > 0 && ExtentHeight > viewportHeight;

    private double ExtentHeight => Items.Count * Math.Max(1, EffectiveItemHeight);

    private double ViewportHeight => _contentBounds.IsEmpty ? Bounds.Height : _contentBounds.Height;

    private double MaxVerticalOffset => Math.Max(0, ExtentHeight - ViewportHeight);

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    private static void ValidateNonNegativeFinite(double value, string parameterName)
    {
        if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Scrollbar metrics must be finite non-negative values.");
    }
}
