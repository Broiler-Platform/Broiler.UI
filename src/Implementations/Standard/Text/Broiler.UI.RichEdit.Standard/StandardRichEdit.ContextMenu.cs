using System;
using System.Collections.Generic;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>The intrinsic editing menu, rendered above the document and sibling controls.</summary>
public sealed partial class StandardRichEdit
{
    private const double ContextMenuPaddingX = 10;
    private const double ContextMenuPaddingY = 4;
    private const double ContextMenuShortcutGap = 28;
    private const double ContextMenuSeparatorHeight = StandardMenuSeparator.Height;
    private const double ContextMenuMinWidth = 168;

    private readonly List<StandardRichEditContextMenuItem> _contextMenuItems = [];
    private BPoint _contextMenuAnchor;
    private UiElement? _contextMenuInputOwner;
    private bool _isContextMenuOpen;
    private int _contextMenuHighlightedIndex = -1;

    public BColor ContextMenuBackground { get; set; } = StandardControlPaint.Surface;

    public BColor ContextMenuForeground { get; set; } = StandardControlPaint.Text;

    public BColor ContextMenuDisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor ContextMenuHighlight { get; set; } = StandardControlPaint.AccentSoft;

    /// <summary>
    /// The color of the highlighted row's text and shortcut, drawn on <see cref="ContextMenuHighlight"/>. Until
    /// it is set it is <see cref="ContextMenuForeground"/>, with the shortcut in
    /// <see cref="ContextMenuDisabledForeground"/> as on the other rows. <see cref="ApplyTheme"/> sets it to the
    /// theme's <see cref="StandardThemeTokens.SelectionText"/> when that differs from the theme's text color.
    /// </summary>
    public BColor ContextMenuHighlightForeground
    {
        get => _contextMenuHighlightForeground ?? ContextMenuForeground;
        set => _contextMenuHighlightForeground = value;
    }

    private BColor? _contextMenuHighlightForeground;

    public BColor ContextMenuBorderColor { get; set; } = StandardControlPaint.Border;

    public double ContextMenuItemHeight { get; set; } = 26;

    public bool IsContextMenuOpen => _isContextMenuOpen;

    /// <summary>
    /// The rows of the open menu, with the enablement they were built with.
    /// Empty while the menu is closed.
    /// </summary>
    public IReadOnlyList<StandardRichEditContextMenuItem> ContextMenuItems => _contextMenuItems;

    /// <summary>The highlighted row, or -1 when no row is highlighted.</summary>
    public int ContextMenuHighlightedIndex => _contextMenuHighlightedIndex;

    /// <summary>The popup rectangle in session coordinates, empty while closed.</summary>
    public BRect ContextMenuBounds => _isContextMenuOpen ? GetContextMenuBounds() : BRect.Empty;

    /// <summary>
    /// Opens the menu with its top-left corner at <paramref name="position"/>,
    /// flipping and clamping it to stay inside the viewport. Enablement is a
    /// snapshot of the control's state taken here, which is also when the
    /// clipboard is read to decide whether Paste can run.
    /// </summary>
    public bool OpenContextMenu(BPoint position) => OpenContextMenu(position, this);

    /// <summary>
    /// Opens the editing menu for an alternate surface in the same session. The surface keeps
    /// focus and capture, forwards input to this editor while the menu is open, and queues the
    /// overlay after painting its content.
    /// </summary>
    public bool OpenContextMenu(BPoint position, UiElement inputOwner)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(inputOwner);
        if (!IsEnabled || (inputOwner != this && (Session is null || inputOwner.Session != Session)))
            return false;

        if (_contextMenuInputOwner is { } previousOwner && previousOwner != inputOwner)
            CloseContextMenu();
        bool opening = !IsContextMenuOpen;
        BuildContextMenuItems();
        _contextMenuAnchor = position;
        _contextMenuInputOwner = inputOwner;
        _isContextMenuOpen = true;
        _contextMenuHighlightedIndex = -1;
        Session?.SetFocus(inputOwner);
        Session?.CaptureInput(inputOwner);
        if (opening && Session is { } session)
            session.SemanticChanged += OnContextMenuFocusChanged;
        inputOwner.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public bool CloseContextMenu()
    {
        if (!_isContextMenuOpen)
            return false;

        UiElement owner = _contextMenuInputOwner ?? this;
        _contextMenuInputOwner = null;
        _isContextMenuOpen = false;
        _contextMenuHighlightedIndex = -1;
        _contextMenuItems.Clear();
        if (Session is { } session)
        {
            session.SemanticChanged -= OnContextMenuFocusChanged;
            session.ReleaseInputCapture(owner);
        }
        owner.Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    private void OnContextMenuFocusChanged(object? sender, UiSemanticChangedEventArgs e)
    {
        if (e.Change == UiSemanticChangeKind.FocusChanged &&
            sender is UiSession session && session.FocusedElement != _contextMenuInputOwner)
            CloseContextMenu();
    }

    /// <summary>
    /// Runs a context-menu command against the control. Public so a host that
    /// shows a platform menu can drive the same behavior as the drawn one.
    /// </summary>
    public bool InvokeContextMenuCommand(RichEditCommand command)
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return false;

        return command is RichEditCommand.Cut or RichEditCommand.Copy or RichEditCommand.Paste or RichEditCommand.SelectAll
            && RunCommand(command);
    }

    private void BuildContextMenuItems()
    {
        _contextMenuItems.Clear();
        AddContextMenuItem(RichEditCommand.Cut, "Cut", "Ctrl+X");
        AddContextMenuItem(RichEditCommand.Copy, "Copy", "Ctrl+C");
        AddContextMenuItem(RichEditCommand.Paste, "Paste", "Ctrl+V");
        _contextMenuItems.Add(StandardRichEditContextMenuItem.Separator);
        AddContextMenuItem(RichEditCommand.SelectAll, "Select All", "Ctrl+A");
    }

    private void AddContextMenuItem(RichEditCommand command, string text, string shortcut)
    {
        bool enabled = GetCommandState(command).IsEnabled;
        if (enabled && command == RichEditCommand.Paste)
            enabled = Session?.Host is IUiClipboardHost clipboard && (!clipboard.CanQueryTextSynchronously || (clipboard.TryGetText(out string value) && !string.IsNullOrEmpty(value)));
        _contextMenuItems.Add(new StandardRichEditContextMenuItem(command, text, shortcut, enabled));
    }

    private bool HandleContextMenuPointerRequest(UiInputEvent input)
    {
        if (input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        Session?.SetFocus(this);
        if (!IsPointInSelection(input.Position))
            Selection = RichTextRange.Caret(PositionFromPoint(input.Position));

        return OpenContextMenu(input.Position);
    }

    private bool HandleContextMenuInput(UiInputEvent input)
    {
        switch (input.Kind)
        {
            case UiInputEventKind.PointerMove:
                if (TryGetContextMenuItemAt(input.Position, out int hovered) && IsSelectable(_contextMenuItems[hovered]))
                    SetContextMenuHighlight(hovered);
                return true;

            case UiInputEventKind.PointerButton:
                return HandleContextMenuPointerButton(input);

            case UiInputEventKind.KeyboardKey:
                return HandleContextMenuKeyboard(input);

            case UiInputEventKind.TextInput:
                HandleContextMenuAccessKey(input.Text);
                return true;

            case UiInputEventKind.PointerWheel:
            case UiInputEventKind.TouchContact:
            case UiInputEventKind.TextComposition:
                return true;

            default:
                return false;
        }
    }

    private bool HandleContextMenuPointerButton(UiInputEvent input)
    {
        // The release that follows the right-click which opened the menu, and the
        // release of a click on a row, both belong to the menu rather than to the
        // text field underneath it.
        if (input.MouseButtonTransition != MouseButtonTransition.Down)
            return true;

        if (input.MouseButton != MouseButton.Left)
            return true;

        if (!TryGetContextMenuItemAt(input.Position, out int index))
        {
            CloseContextMenu();
            return true;
        }

        StandardRichEditContextMenuItem item = _contextMenuItems[index];
        if (!IsSelectable(item) || item.Command is not RichEditCommand command)
            return true;

        CloseContextMenu();
        InvokeContextMenuCommand(command);
        return true;
    }

    private bool HandleContextMenuKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return true;

        if (IsKey(input, BVirtualKey.Escape, "Escape"))
        {
            CloseContextMenu();
            return true;
        }
        if (IsKey(input, BVirtualKey.Down, "Down"))
        {
            MoveContextMenuHighlight(1);
            return true;
        }
        if (IsKey(input, BVirtualKey.Up, "Up"))
        {
            MoveContextMenuHighlight(-1);
            return true;
        }
        if (IsKey(input, BVirtualKey.Home, "Home"))
        {
            _contextMenuHighlightedIndex = -1;
            MoveContextMenuHighlight(1);
            return true;
        }
        if (IsKey(input, BVirtualKey.End, "End"))
        {
            _contextMenuHighlightedIndex = -1;
            MoveContextMenuHighlight(-1);
            return true;
        }
        if (IsKey(input, BVirtualKey.Enter, "Enter") || IsKey(input, BVirtualKey.Space, "Space"))
        {
            InvokeHighlightedContextMenuCommand();
            return true;
        }

        // Anything else is swallowed: an open menu owns the keyboard, so a
        // shortcut cannot edit the text behind it.
        return true;
    }

    /// <summary>
    /// Moves the highlight to the next row whose first letter matches, cycling
    /// so repeated presses walk the rows that share a letter (Cut and Copy).
    /// </summary>
    private bool HandleContextMenuAccessKey(string? text)
    {
        int count = _contextMenuItems.Count;
        if (string.IsNullOrEmpty(text) || count == 0)
            return false;

        char key = char.ToUpperInvariant(text[0]);
        int start = _contextMenuHighlightedIndex < 0 ? count - 1 : _contextMenuHighlightedIndex;
        for (int offset = 1; offset <= count; offset++)
        {
            int index = (start + offset) % count;
            StandardRichEditContextMenuItem item = _contextMenuItems[index];
            if (IsSelectable(item) && item.Text.Length > 0 && char.ToUpperInvariant(item.Text[0]) == key)
            {
                SetContextMenuHighlight(index);
                return true;
            }
        }

        return false;
    }

    private bool InvokeHighlightedContextMenuCommand()
    {
        if ((uint)_contextMenuHighlightedIndex >= (uint)_contextMenuItems.Count)
            return false;

        StandardRichEditContextMenuItem item = _contextMenuItems[_contextMenuHighlightedIndex];
        if (!IsSelectable(item) || item.Command is not RichEditCommand command)
            return false;

        CloseContextMenu();
        return InvokeContextMenuCommand(command);
    }

    private bool MoveContextMenuHighlight(int delta)
    {
        int count = _contextMenuItems.Count;
        if (count == 0)
            return false;

        int index = _contextMenuHighlightedIndex;
        for (int step = 0; step < count; step++)
        {
            index = index < 0
                ? (delta > 0 ? 0 : count - 1)
                : (index + delta + count) % count;
            if (IsSelectable(_contextMenuItems[index]))
            {
                SetContextMenuHighlight(index);
                return true;
            }
        }

        return false;
    }

    private void SetContextMenuHighlight(int index)
    {
        if (_contextMenuHighlightedIndex == index)
            return;

        _contextMenuHighlightedIndex = index;
        (_contextMenuInputOwner ?? this).Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private bool OpenContextMenuAtCaret()
    {
        EnsureCaretVisible();
        BRect caret = CaretRect(Selection.Focus, View);
        return OpenContextMenu(new BPoint(caret.Left, caret.Bottom + 2));
    }

    /// <summary>
    /// Whether a point falls inside the current selection, which decides whether
    /// a right-click keeps the selection or moves the caret to the click. The
    /// selection is measured exactly as it is painted, so what a click treats as
    /// selected is what the user sees selected.
    /// </summary>
    private bool IsPointInSelection(BPoint point)
    {
        RichTextRange selection = Selection;
        if (selection.IsEmpty || !InnerBounds.Contains(point))
            return false;

        EnsureLayout();
        RichEditViewport view = View;
        BPoint local = view.ToContent(point);
        foreach (BRect box in _geometry.GetSelectionBounds(RichEditLayout.FlowFragment, selection, view.ToContent(view.Inner)))
        {
            if (local.Y >= box.Top && local.Y < box.Bottom && local.X >= box.Left && local.X <= box.Right)
                return true;
        }

        return false;
    }

    private bool TryGetContextMenuItemAt(BPoint point, out int index)
    {
        index = -1;
        BRect menu = GetContextMenuBounds();
        if (!menu.Contains(point))
            return false;

        double top = menu.Top + ContextMenuPaddingY;
        for (int item = 0; item < _contextMenuItems.Count; item++)
        {
            double height = _contextMenuItems[item].IsSeparator ? ContextMenuSeparatorHeight : ContextMenuItemHeight;
            if (point.Y >= top && point.Y < top + height)
            {
                index = item;
                return true;
            }

            top += height;
        }

        return false;
    }

    private BRect GetContextMenuBounds()
    {
        double width = ContextMenuMinWidth;
        double height = ContextMenuPaddingY * 2;
        foreach (StandardRichEditContextMenuItem item in _contextMenuItems)
        {
            if (item.IsSeparator)
            {
                height += ContextMenuSeparatorHeight;
                continue;
            }

            height += ContextMenuItemHeight;
            double row = BTextMeasurer.MeasureAdvance(item.Text, Font) + (ContextMenuPaddingX * 2);
            if (item.Shortcut.Length > 0)
                row += ContextMenuShortcutGap + BTextMeasurer.MeasureAdvance(item.Shortcut, Font);
            width = Math.Max(width, row);
        }

        BSize viewport = Session?.Host.ViewportSize ?? new BSize(_contextMenuAnchor.X + width, _contextMenuAnchor.Y + height);

        // A menu taller or wider than the viewport is clamped to it rather than
        // hanging off the edge: rendering clips to these bounds and hit testing
        // reads from them, so the two stay in step.
        width = Math.Min(width, Math.Max(0, viewport.Width));
        height = Math.Min(height, Math.Max(0, viewport.Height));
        double left = _contextMenuAnchor.X + width > viewport.Width
            ? Math.Max(0, viewport.Width - width)
            : _contextMenuAnchor.X;

        // Below the anchor when it fits, flipped above it when it does not — the
        // caret or the click stays visible either way.
        double top = _contextMenuAnchor.Y + height > viewport.Height
            ? Math.Max(0, _contextMenuAnchor.Y - height)
            : _contextMenuAnchor.Y;
        return new BRect(Math.Clamp(left, 0, Math.Max(0, viewport.Width - width)), Math.Clamp(top, 0, Math.Max(0, viewport.Height - height)), width, height);
    }

    /// <summary>Queues the open menu above the surface's content and outside its content clip.</summary>
    public void RenderContextMenuOverlay(UiRenderContext context, UiElement inputOwner)
    {
        if (IsContextMenuOpen && ReferenceEquals(_contextMenuInputOwner, inputOwner))
            context.Defer(RenderContextMenu);
    }

    private void RenderContextMenu(UiRenderContext context)
    {
        if (!IsContextMenuOpen)
            return;
        BRect menu = GetContextMenuBounds();
        StandardControlPaint.FillRounded(context.RenderList, menu, ContextMenuBackground, CornerRadius);
        StandardControlPaint.StrokeRounded(context.RenderList, menu, ContextMenuBorderColor, CornerRadius, 1);

        double lineHeight = BTextMeasurer.GetLineHeight(Font);
        double top = menu.Top + ContextMenuPaddingY;
        context.RenderList.PushClip(menu);
        for (int index = 0; index < _contextMenuItems.Count; index++)
        {
            StandardRichEditContextMenuItem item = _contextMenuItems[index];
            if (item.IsSeparator)
            {
                StandardMenuSeparator.Draw(context, new BRect(menu.Left, top, menu.Width, ContextMenuSeparatorHeight), ContextMenuBorderColor);
                top += ContextMenuSeparatorHeight;
                continue;
            }

            var row = new BRect(menu.Left, top, menu.Width, ContextMenuItemHeight);
            bool highlighted = index == _contextMenuHighlightedIndex;
            if (highlighted)
                StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(row, 2), ContextMenuHighlight, StandardControlPaint.SmallRadius);

            // A highlight with a text color of its own takes the shortcut too: the dimmed shortcut color is
            // chosen for the menu background, not for the highlight.
            bool ownHighlightColor = highlighted && ContextMenuHighlightForeground != ContextMenuForeground;
            BColor foreground = !item.IsEnabled ? ContextMenuDisabledForeground : highlighted ? ContextMenuHighlightForeground : ContextMenuForeground;
            double baseline = row.Top + Math.Max(0, (row.Height - lineHeight) / 2);
            StandardControlPaint.DrawText(context.RenderList, item.Text, Font, foreground, new BPoint(row.Left + ContextMenuPaddingX, baseline), context.Host.Scale);
            if (item.Shortcut.Length > 0)
            {
                double shortcutWidth = BTextMeasurer.MeasureAdvance(item.Shortcut, Font);
                BColor shortcut = ownHighlightColor ? ContextMenuHighlightForeground : ContextMenuDisabledForeground;
                StandardControlPaint.DrawText(context.RenderList, item.Shortcut, Font, shortcut, new BPoint(row.Right - ContextMenuPaddingX - shortcutWidth, baseline), context.Host.Scale);
            }

            top += ContextMenuItemHeight;
        }

        context.RenderList.PopClip();
    }

    /// <summary>The open menu's accessibility node, for the surface that presents it.</summary>
    public UiSemanticNode? GetContextMenuSemanticNode(UiElement inputOwner)
    {
        if (!IsContextMenuOpen || _contextMenuInputOwner != inputOwner)
            return null;

        BRect bounds = GetContextMenuBounds();
        var rows = new List<UiSemanticNode>();
        double top = bounds.Top + ContextMenuPaddingY;
        for (int i = 0; i < _contextMenuItems.Count; i++)
        {
            var item = _contextMenuItems[i];
            if (item.IsSeparator)
            {
                top += ContextMenuSeparatorHeight;
                continue;
            }
            var row = new BRect(bounds.Left, top, bounds.Width, ContextMenuItemHeight).Intersect(bounds);
            top += ContextMenuItemHeight;
            if (row.IsEmpty)
                continue;
            UiSemanticState state = UiSemanticState.Visible;
            if (item.IsEnabled) state |= UiSemanticState.Enabled;
            if (i == _contextMenuHighlightedIndex) state |= UiSemanticState.Selected;
            rows.Add(new UiSemanticNode(UiSemanticRole.MenuItem, item.Text, row, state, []));
        }
        return new UiSemanticNode(UiSemanticRole.Menu, "Edit commands", bounds,
            UiSemanticState.Visible | UiSemanticState.Enabled | UiSemanticState.Expanded, rows);
    }

    private static bool IsSelectable(StandardRichEditContextMenuItem item) => !item.IsSeparator && item.IsEnabled;

    /// <summary>Whether this input requests the keyboard context menu.</summary>
    public static bool IsContextMenuRequest(UiInputEvent input) =>
        input.Kind == UiInputEventKind.KeyboardKey && input.KeyTransition == KeyboardKeyTransition.Down &&
        (IsKey(input, 0x5D, "ContextMenu") ||
         string.Equals(input.KeyName, "Apps", StringComparison.OrdinalIgnoreCase) ||
         ((input.KeyModifiers & (KeyboardModifierState.Shift | KeyboardModifierState.LeftShift | KeyboardModifierState.RightShift)) != 0 &&
          IsKey(input, 0x79, "F10")));
}
