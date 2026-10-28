using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.Menu.Standard;

public sealed class StandardMenu : UiMenu, IStandardThemedControl
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        // Follow the theme's body font, including a text-scaled theme, unless the application set its own.
        BFontStyle themeFont = StandardThemeFonts.For(theme, StandardTextStyle.Body);
        BFontStyle followed = StandardThemeFonts.Follow(Font, _themeFont, themeFont);
        _themeFont = themeFont;
        if (followed != Font)
        {
            Font = followed;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
        PopupBackground = theme.Surface;
        Foreground = theme.Text;
        DisabledForeground = theme.TextDisabled;
        SelectedBackground = theme.AccentSoft;
        // The open item keeps following Foreground unless the theme gives the selection a text color of its own.
        _selectedForeground = theme.SelectionText == theme.Text ? null : theme.SelectionText;
        BorderColor = theme.Border;
    }

    private readonly List<BRect> _topLevelBounds = [];
    private UiElement? _focusBeforeOpen;
    private BColor? _selectedForeground;

    public BColor Background { get; set; } = BColor.Transparent;

    public BColor PopupBackground { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor DisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor SelectedBackground { get; set; } = StandardControlPaint.AccentSoft;

    /// <summary>
    /// The color of the open or highlighted item's text, drawn on <see cref="SelectedBackground"/>. Until it is
    /// set it is <see cref="Foreground"/>. <see cref="ApplyTheme"/> sets it to the theme's
    /// <see cref="StandardThemeTokens.SelectionText"/> when that differs from the theme's text color, and
    /// otherwise lets it follow <see cref="Foreground"/> again. Disabled items keep
    /// <see cref="DisabledForeground"/>.
    /// </summary>
    public BColor SelectedForeground
    {
        get => _selectedForeground ?? Foreground;
        set => _selectedForeground = value;
    }

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BFontStyle Font { get; set; } = StandardControlPaint.Theme.FontBody;

    private BFontStyle _themeFont = StandardControlPaint.Theme.FontBody;

    public double MenuBarHeight { get; set; } = 28;

    public double ItemHeight { get; set; } = 26;

    public double PopupWidth { get; set; } = 180;

    /// <summary>Optional trigger bounds used to place an overflowing popup above its trigger.</summary>
    public BRect? PopupAnchorBounds { get; set; }

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public StandardCommandDispatcher? CommandDispatcher { get; set; }

    public IReadOnlyList<BRect> TopLevelBounds => _topLevelBounds;

    /// <summary>
    /// The glyph or text drawn on the trigger button in <see cref="UiMenuPresentationMode.Overflow"/> mode.
    /// Defaults to the vertical ellipsis "⋮".
    /// </summary>
    public string OverflowGlyph { get; set; } = "⋮";

    public override BRect OverlayBounds
    {
        get
        {
            if (!IsOpen)
                return BRect.Empty;

            if (PresentationMode == UiMenuPresentationMode.BottomSheet)
            {
                BSize viewport = Session?.Host.ViewportSize ?? Bounds.Size;
                return new BRect(0, 0, viewport.Width, viewport.Height);
            }

            if (!TryGetFirstPopupLevel(out IReadOnlyList<UiMenuItem> level, out BPoint origin, out _))
                return BRect.Empty;

            return GetPopupBounds(origin, level.Count);
        }
    }

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticNode node = base.GetSemanticNodeCore();
        if (!IsOpen || PresentationMode is not (UiMenuPresentationMode.ContextMenu or UiMenuPresentationMode.Overflow))
            return node;

        // Popup rows are virtual children, with the same bounds and selection as the painted rows.
        var children = new List<UiSemanticNode>();
        if (TryGetFirstPopupLevel(out IReadOnlyList<UiMenuItem> level, out BPoint origin, out int depth))
        {
            for (; level.Count > 0 && depth < MaxDepth; depth++)
            {
                BRect popup = GetPopupBounds(origin, level.Count);
                int selected = GetSelectedIndexAtDepth(depth, level.Count);
                for (int i = 0; i < level.Count; i++)
                {
                    UiMenuItem item = level[i];
                    if (item.IsSeparator) continue;
                    UiSemanticState state = UiSemanticState.Visible;
                    if (item.IsEnabled) state |= UiSemanticState.Enabled;
                    if (i == selected) state |= UiSemanticState.Selected;
                    if (item.IsCheckable && item.IsChecked) state |= UiSemanticState.Checked;
                    children.Add(new UiSemanticNode(UiSemanticRole.MenuItem, item.Text,
                        new BRect(popup.Left, popup.Top + i * ItemHeight, popup.Width, ItemHeight), state, []));
                }
                if (selected < 0 || level[selected].Children.Count == 0) break;
                origin = new BPoint(popup.Right - 2, popup.Top + selected * ItemHeight);
                level = level[selected].Children.ToArray();
            }
        }
        return node with { Bounds = OverlayBounds, Children = children };
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double height = PresentationMode switch
        {
            UiMenuPresentationMode.MenuBar => MenuBarHeight,
            UiMenuPresentationMode.Overflow => MenuBarHeight,
            UiMenuPresentationMode.TabBar => Math.Max(MenuBarHeight, 48),
            UiMenuPresentationMode.Drawer => Math.Max(ItemHeight, Items.Count * ItemHeight),
            _ => ItemHeight,
        };
        double width = PresentationMode switch
        {
            UiMenuPresentationMode.Overflow => Math.Max(MenuBarHeight, 32),
            UiMenuPresentationMode.Drawer => PopupWidth,
            _ => PreferredSize.Width,
        };
        return new BSize(ClampDesired(width, availableSize.Width), ClampDesired(height, availableSize.Height));
    }

    protected override void RenderCore(UiRenderContext context)
    {
        _topLevelBounds.Clear();
        if (!Background.IsEmpty && Background.A > 0)
            context.RenderList.FillRect(Bounds, Background);

        switch (PresentationMode)
        {
            case UiMenuPresentationMode.MenuBar:
                RenderTopLevel(context);
                break;
            case UiMenuPresentationMode.Overflow:
                RenderOverflowTrigger(context);
                break;
            case UiMenuPresentationMode.Drawer:
                RenderDrawer(context);
                break;
            case UiMenuPresentationMode.TabBar:
                RenderTabBar(context);
                break;
            case UiMenuPresentationMode.ContextMenu:
            case UiMenuPresentationMode.BottomSheet:
                break;
        }

        if (IsOpen)
            context.Defer(RenderOpenPopups);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointer(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            UiInputEventKind.TextInput => HandleText(input.Text),
            _ => false,
        };
    }

    private bool HandlePointer(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        for (int index = 0; index < _topLevelBounds.Count; index++)
        {
            if (_topLevelBounds[index].Contains(input.Position))
            {
                if (PresentationMode == UiMenuPresentationMode.Overflow)
                {
                    if (IsOpen)
                        CloseManaged(restoreFocus: true);
                    else
                        OpenManaged([0]);
                    return true;
                }

                UiMenuItem item = Items[index];
                if (!item.IsEnabled)
                    return true;

                if (item.Children.Count == 0 && PresentationMode is UiMenuPresentationMode.TabBar or UiMenuPresentationMode.Drawer)
                {
                    SetSelectedPath([index]);
                    InvokeAndClose();
                    return true;
                }

                if (PresentationMode == UiMenuPresentationMode.MenuBar && IsOpen && SelectedPath.Count > 0 && SelectedPath[0] == index)
                    CloseManaged(restoreFocus: true);
                else
                    OpenManaged([index]);
                return true;
            }
        }

        if (IsOpen)
        {
            IReadOnlyList<int>? popupPath = HitTestPopup(input.Position);
            if (popupPath is not null)
            {
                SetSelectedPath(popupPath);
                UiMenuItem? item = GetItem(popupPath);
                if (item?.Children.Count > 0)
                    return true;

                InvokeAndClose();
                return true;
            }

            CloseManaged(restoreFocus: true);
            return true;
        }

        return false;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down || Items.Count == 0)
            return false;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
        {
            CloseManaged(restoreFocus: true);
            return true;
        }

        if (!IsOpen)
        {
            if (IsKey(input, BVirtualKey.Down, "Down") || IsKey(input, BVirtualKey.Enter, "Enter") || IsKey(input, BVirtualKey.Space, "Space"))
            {
                OpenManaged([Math.Max(0, SelectedPath.Count > 0 ? SelectedPath[0] : 0)]);
                return true;
            }

            return false;
        }

        if (IsKey(input, BVirtualKey.Right, "Right"))
            return MoveTopLevel(1);
        if (IsKey(input, BVirtualKey.Left, "Left"))
            return MoveTopLevel(-1);
        if (IsKey(input, BVirtualKey.Down, "Down"))
            return MoveWithinCurrentLevel(1);
        if (IsKey(input, BVirtualKey.Up, "Up"))
            return MoveWithinCurrentLevel(-1);
        if (IsKey(input, BVirtualKey.Enter, "Enter") || IsKey(input, BVirtualKey.Space, "Space"))
        {
            UiMenuItem? item = GetItem(SelectedPath);
            if (item?.Children.Count > 0 && SelectedPath.Count < MaxDepth)
                return SetSelectedPath(SelectedPath.Concat([0]));

            InvokeAndClose();
            return true;
        }

        return false;
    }

    private bool HandleText(string? text)
    {
        if (!IsOpen || string.IsNullOrEmpty(text))
            return false;

        char key = char.ToUpperInvariant(text[0]);
        IReadOnlyList<UiMenuItem> level = GetCurrentLevel(out int selectedInLevel);
        for (int offset = 1; offset <= level.Count; offset++)
        {
            int index = (selectedInLevel + offset) % level.Count;
            UiMenuItem item = level[index];
            char? accessKey = item.AccessKey.HasValue ? char.ToUpperInvariant(item.AccessKey.Value) : null;
            if (accessKey == key || (!string.IsNullOrEmpty(item.Text) && char.ToUpperInvariant(item.Text[0]) == key))
                return SetSelectedPath(ReplaceLastPathIndex(index));
        }

        return false;
    }

    /// <summary>Opens a popup with input capture, keyboard focus and focus restoration on dismissal.</summary>
    public bool OpenPopup()
    {
        ThrowIfDisposed();
        if (IsOpen || Items.Count == 0) return false;
        OpenManaged([0]);
        return true;
    }

    /// <summary>Closes a popup and releases its input capture.</summary>
    public bool ClosePopup()
    {
        ThrowIfDisposed();
        if (!IsOpen) return false;
        CloseManaged(restoreFocus: true);
        return true;
    }

    private void OpenManaged(IEnumerable<int> path)
    {
        _focusBeforeOpen = Session?.FocusedElement;
        Open();
        SetSelectedPath(path);
        Session?.SetFocus(this);
        Session?.CaptureInput(this);
    }

    private void CloseManaged(bool restoreFocus)
    {
        Close();
        Session?.ReleaseInputCapture(this);
        if (restoreFocus && _focusBeforeOpen is not null && _focusBeforeOpen.Session == Session)
            Session?.SetFocus(_focusBeforeOpen);
        else
            Session?.SetFocus(this);
    }

    private void InvokeAndClose()
    {
        UiMenuItem? item = GetItem(SelectedPath);
        if (item is null || !item.IsEnabled || item.IsSeparator || item.Children.Count > 0)
            return;
        if (item is not null && !string.IsNullOrWhiteSpace(item.CommandName))
            CommandDispatcher?.TryExecute(item.CommandName);
        InvokeSelected();
        CloseManaged(restoreFocus: true);
    }

    private bool MoveTopLevel(int delta)
    {
        int index = SelectedPath.Count > 0 ? SelectedPath[0] : 0;
        index = (index + delta + Items.Count) % Items.Count;
        return SetSelectedPath([index]);
    }

    private bool MoveWithinCurrentLevel(int delta)
    {
        IReadOnlyList<UiMenuItem> level = GetCurrentLevel(out int selectedInLevel);
        if (level.Count == 0)
            return false;

        int index = PresentationMode == UiMenuPresentationMode.MenuBar && SelectedPath.Count == 1
            ? (delta > 0 ? 0 : level.Count - 1)
            : (selectedInLevel + delta + level.Count) % level.Count;
        return SetSelectedPath(ReplaceLastPathIndex(index));
    }

    private IReadOnlyList<UiMenuItem> GetCurrentLevel(out int selectedInLevel)
    {
        selectedInLevel = SelectedPath.Count == 0 ? 0 : SelectedPath[^1];
        if (SelectedPath.Count == 0)
            return Items;

        if (PresentationMode == UiMenuPresentationMode.MenuBar && SelectedPath.Count == 1)
        {
            selectedInLevel = -1;
            UiMenuItem? topLevel = GetItem(SelectedPath);
            return topLevel?.Children.ToArray() ?? [];
        }

        if (SelectedPath.Count == 1)
            return Items;

        UiMenuItem? parent = GetItem(SelectedPath.Take(SelectedPath.Count - 1).ToArray());
        return parent?.Children.ToArray() ?? [];
    }

    private IReadOnlyList<int> ReplaceLastPathIndex(int index)
    {
        int[] path = SelectedPath.ToArray();
        if (path.Length == 0)
            return [index];
        if (PresentationMode == UiMenuPresentationMode.MenuBar && path.Length == 1)
            return [path[0], index];
        path[^1] = index;
        return path;
    }

    private void RenderTopLevel(UiRenderContext context)
    {
        double x = Bounds.Left;
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            double width = Math.Max(54, BTextMeasurer.MeasureAdvance(item.Text, Font) + 24);
            BRect rect = new(x, Bounds.Top, width, MenuBarHeight);
            _topLevelBounds.Add(rect);
            bool selected = SelectedPath.Count > 0 && SelectedPath[0] == index && IsOpen;
            if (selected)
                StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(rect, 2), SelectedBackground, CornerRadius);
            BColor foreground = !item.IsEnabled ? DisabledForeground : selected ? SelectedForeground : Foreground;
            context.RenderList.DrawText(new BTextRun(item.Text, Font, foreground), new BPoint(rect.Left + 10, rect.Top + Math.Max(0, (rect.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
            x = rect.Right;
        }
    }

    private void RenderOverflowTrigger(UiRenderContext context)
    {
        BRect rect = Bounds;
        _topLevelBounds.Add(rect);
        bool selected = IsOpen;
        if (selected)
            StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(rect, 2), SelectedBackground, CornerRadius);

        BColor foreground = selected ? SelectedForeground : Foreground;
        string glyph = !string.IsNullOrEmpty(OverflowGlyph) ? OverflowGlyph : "⋮";
        BSize glyphSize = BTextMeasurer.Measure(glyph, Font).Size;
        double x = rect.Left + Math.Max(0, (rect.Width - glyphSize.Width) / 2);
        double y = rect.Top + Math.Max(0, (rect.Height - BTextMeasurer.GetLineHeight(Font)) / 2);
        context.RenderList.DrawText(new BTextRun(glyph, Font, foreground), new BPoint(x, y));
    }

    private void RenderDrawer(UiRenderContext context)
    {
        double y = Bounds.Top;
        double rowHeight = Math.Max(ItemHeight, 36);
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            BRect rect = new(Bounds.Left, y, Bounds.Width, rowHeight);
            _topLevelBounds.Add(rect);
            bool selected = SelectedPath.Count > 0 && SelectedPath[0] == index && IsOpen;
            if (selected)
                StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(rect, 2), SelectedBackground, CornerRadius);

            string suffix = item.Children.Count > 0 ? " >" : string.Empty;
            BColor foreground = !item.IsEnabled ? DisabledForeground : selected ? SelectedForeground : Foreground;
            context.RenderList.DrawText(new BTextRun(item.Text + suffix, Font, foreground), new BPoint(rect.Left + 12, rect.Top + Math.Max(0, (rect.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
            y = rect.Bottom;
        }
    }

    private void RenderTabBar(UiRenderContext context)
    {
        if (Items.Count == 0)
            return;

        double itemWidth = Bounds.Width / Items.Count;
        for (int index = 0; index < Items.Count; index++)
        {
            UiMenuItem item = Items[index];
            BRect rect = new(Bounds.Left + (index * itemWidth), Bounds.Top, itemWidth, Bounds.Height);
            _topLevelBounds.Add(rect);
            bool selected = SelectedPath.Count > 0 && SelectedPath[0] == index && IsOpen;
            if (selected)
            {
                StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(rect, 2), SelectedBackground, CornerRadius);
                context.RenderList.FillRect(new BRect(rect.Left + 4, rect.Bottom - 3, Math.Max(0, rect.Width - 8), 3), SelectedForeground);
            }

            BColor foreground = !item.IsEnabled ? DisabledForeground : selected ? SelectedForeground : Foreground;
            BSize textSize = BTextMeasurer.Measure(item.Text, Font).Size;
            double x = rect.Left + Math.Max(0, (rect.Width - textSize.Width) / 2);
            double y = rect.Top + Math.Max(0, (rect.Height - BTextMeasurer.GetLineHeight(Font)) / 2);
            context.RenderList.DrawText(new BTextRun(item.Text, Font, foreground), new BPoint(x, y));
        }
    }

    private void RenderOpenPopups(UiRenderContext context)
    {
        if (!TryGetFirstPopupLevel(out IReadOnlyList<UiMenuItem> level, out BPoint origin, out int pathDepth))
            return;

        if (PresentationMode == UiMenuPresentationMode.BottomSheet)
        {
            RenderBottomSheet(context, level);
            return;
        }

        for (; level.Count > 0 && pathDepth < MaxDepth; pathDepth++)
        {
            BRect popup = GetPopupBounds(origin, level.Count);
            int selectedIndex = GetSelectedIndexAtDepth(pathDepth, level.Count);
            StandardControlPaint.FillRounded(context.RenderList, popup, PopupBackground, CornerRadius);
            StandardControlPaint.StrokeRounded(context.RenderList, popup, BorderColor, CornerRadius, 1);
            for (int index = 0; index < level.Count; index++)
            {
                UiMenuItem item = level[index];
                BRect row = new(popup.Left, popup.Top + index * ItemHeight, popup.Width, ItemHeight);
                if (selectedIndex == index)
                    StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(row, 2), SelectedBackground, CornerRadius);
                string prefix = item.IsCheckable ? (item.IsChecked ? "x " : "  ") : string.Empty;
                string suffix = item.Children.Count > 0 ? " >" : string.Empty;
                BColor foreground = !item.IsEnabled ? DisabledForeground : selectedIndex == index ? SelectedForeground : Foreground;
                context.RenderList.DrawText(new BTextRun(prefix + item.Text + suffix, Font, foreground), new BPoint(row.Left + 8, row.Top + Math.Max(0, (row.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
            }

            if (selectedIndex < 0)
                break;

            UiMenuItem selected = level[selectedIndex];
            if (selected.Children.Count == 0)
                break;
            origin = new BPoint(popup.Right - 2, popup.Top + selectedIndex * ItemHeight);
            level = selected.Children.ToArray();
        }
    }

    private void RenderBottomSheet(UiRenderContext context, IReadOnlyList<UiMenuItem> level)
    {
        BSize viewport = Session?.Host.ViewportSize ?? new BSize(Bounds.Right, Bounds.Bottom);
        context.RenderList.FillRect(new BRect(0, 0, viewport.Width, viewport.Height), new BColor(0, 0, 0, 96));

        BRect sheet = GetPopupBounds(new BPoint(0, viewport.Height), level.Count);
        double rowHeight = Math.Max(ItemHeight, 44);
        double handleHeight = 24;

        StandardControlPaint.FillRounded(context.RenderList, sheet, PopupBackground, 16);
        StandardControlPaint.StrokeRounded(context.RenderList, sheet, BorderColor, 16, 1);

        double handleWidth = 40;
        BRect handle = new((viewport.Width - handleWidth) / 2, sheet.Top + 10, handleWidth, 4);
        StandardControlPaint.FillRounded(context.RenderList, handle, BorderColor, 2);

        int selectedIndex = GetSelectedIndexAtDepth(0, level.Count);
        for (int index = 0; index < level.Count; index++)
        {
            UiMenuItem item = level[index];
            BRect row = new(sheet.Left, sheet.Top + handleHeight + (index * rowHeight), sheet.Width, rowHeight);
            if (selectedIndex == index)
                StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(row, 2), SelectedBackground, CornerRadius);

            string prefix = item.IsCheckable ? (item.IsChecked ? "x " : "  ") : string.Empty;
            string suffix = item.Children.Count > 0 ? " >" : string.Empty;
            BColor foreground = !item.IsEnabled ? DisabledForeground : selectedIndex == index ? SelectedForeground : Foreground;
            context.RenderList.DrawText(new BTextRun(prefix + item.Text + suffix, Font, foreground), new BPoint(row.Left + 20, row.Top + Math.Max(0, (row.Height - BTextMeasurer.GetLineHeight(Font)) / 2)));
        }
    }

    private IReadOnlyList<int>? HitTestPopup(BPoint point)
    {
        if (!TryGetFirstPopupLevel(out IReadOnlyList<UiMenuItem> level, out BPoint origin, out int pathDepth))
            return null;

        if (PresentationMode == UiMenuPresentationMode.BottomSheet)
        {
            BRect sheet = GetPopupBounds(origin, level.Count);
            if (!sheet.Contains(point))
                return null;

            double rowHeight = Math.Max(ItemHeight, 44);
            double handleHeight = 24;
            int index = (int)Math.Floor((point.Y - (sheet.Top + handleHeight)) / rowHeight);
            if ((uint)index < (uint)level.Count)
                return [index];

            return null;
        }

        var path = new List<int>();
        if (pathDepth > 0 && SelectedPath.Count > 0)
            path.Add(SelectedPath[0]);

        for (; level.Count > 0 && pathDepth < MaxDepth; pathDepth++)
        {
            BRect popup = GetPopupBounds(origin, level.Count);
            if (popup.Contains(point))
            {
                int index = (int)Math.Floor((point.Y - popup.Top) / Math.Max(1, ItemHeight));
                if ((uint)index < (uint)level.Count)
                {
                    path.Add(index);
                    return path;
                }
            }

            int selectedIndex = GetSelectedIndexAtDepth(pathDepth, level.Count);
            if (selectedIndex < 0)
                break;

            UiMenuItem selected = level[selectedIndex];
            path.Add(selectedIndex);
            if (selected.Children.Count == 0)
                break;
            origin = new BPoint(popup.Right - 2, popup.Top + selectedIndex * ItemHeight);
            level = selected.Children.ToArray();
        }

        return null;
    }

    private bool TryGetFirstPopupLevel(out IReadOnlyList<UiMenuItem> level, out BPoint origin, out int pathDepth)
    {
        if (PresentationMode is UiMenuPresentationMode.ContextMenu or UiMenuPresentationMode.Overflow or UiMenuPresentationMode.BottomSheet)
        {
            level = Items;
            origin = GetPopupOrigin(0, 0);
            pathDepth = 0;
            return level.Count > 0;
        }

        if (SelectedPath.Count == 0 || (uint)SelectedPath[0] >= (uint)Items.Count)
        {
            level = [];
            origin = Bounds.Location;
            pathDepth = 0;
            return false;
        }

        UiMenuItem topLevel = Items[SelectedPath[0]];
        level = topLevel.Children.ToArray();
        origin = GetPopupOrigin(SelectedPath[0], 0);
        pathDepth = 1;
        return level.Count > 0;
    }

    private int GetSelectedIndexAtDepth(int pathDepth, int levelCount)
    {
        if (SelectedPath.Count <= pathDepth)
            return -1;

        int index = SelectedPath[pathDepth];
        return (uint)index < (uint)levelCount ? index : -1;
    }

    private BPoint GetPopupOrigin(int topIndex, int depth)
    {
        if (PresentationMode == UiMenuPresentationMode.ContextMenu)
            return Bounds.Location;

        if (PresentationMode == UiMenuPresentationMode.Overflow)
            return new BPoint(Bounds.Left, Bounds.Bottom);

        if (PresentationMode == UiMenuPresentationMode.BottomSheet)
            return new BPoint(0, Bounds.Bottom);

        if (_topLevelBounds.Count == 0)
            return new BPoint(Bounds.Left, Bounds.Bottom);

        int index = SelectedPath.Count > 0 ? Math.Clamp(SelectedPath[0], 0, _topLevelBounds.Count - 1) : topIndex;
        if (PresentationMode == UiMenuPresentationMode.Drawer)
            return new BPoint(_topLevelBounds[index].Right - 2, _topLevelBounds[index].Top);

        return new BPoint(_topLevelBounds[index].Left, _topLevelBounds[index].Bottom);
    }

    private BRect GetPopupBounds(BPoint origin, int itemCount)
    {
        BSize viewport = Session?.Host.ViewportSize ?? new BSize(origin.X + PopupWidth, origin.Y + itemCount * ItemHeight);

        if (PresentationMode == UiMenuPresentationMode.BottomSheet)
        {
            double rowHeight = Math.Max(ItemHeight, 44);
            double handleHeight = 24;
            double sheetHeight = Math.Min(viewport.Height * 0.8, (itemCount * rowHeight) + handleHeight + 16);
            return new BRect(0, viewport.Height - sheetHeight, viewport.Width, sheetHeight);
        }

        double height = Math.Max(ItemHeight, itemCount * ItemHeight);
        double width = Math.Min(PopupWidth, viewport.Width);
        double left = Math.Clamp(origin.X, 0, Math.Max(0, viewport.Width - width));
        double top = origin.Y;
        if (top + height > viewport.Height)
            top = PopupAnchorBounds is BRect anchor && anchor.Top >= height
                ? anchor.Top - height : Math.Max(0, viewport.Height - height);
        return new BRect(left, Math.Max(0, top), width, Math.Min(height, viewport.Height));
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
