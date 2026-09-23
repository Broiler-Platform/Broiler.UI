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
    private const double ContextMenuSeparatorHeight = 7;
    private const double ContextMenuMinWidth = 168;

    private readonly List<StandardRichEditContextMenuItem> _contextMenuItems = [];
    private BPoint _contextMenuAnchor;
    private bool _isContextMenuOpen;
    private int _contextMenuHighlightedIndex = -1;

    public BColor ContextMenuBackground { get; set; } = StandardControlPaint.Surface;

    public BColor ContextMenuForeground { get; set; } = StandardControlPaint.Text;

    public BColor ContextMenuDisabledForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor ContextMenuHighlight { get; set; } = StandardControlPaint.AccentSoft;

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
    public bool OpenContextMenu(BPoint position)
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return false;

        BuildContextMenuItems();
        _contextMenuAnchor = position;
        _isContextMenuOpen = true;
        _contextMenuHighlightedIndex = -1;
        Session?.SetFocus(this);
        Session?.CaptureInput(this);
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public bool CloseContextMenu()
    {
        if (!_isContextMenuOpen)
            return false;

        _isContextMenuOpen = false;
        _contextMenuHighlightedIndex = -1;
        _contextMenuItems.Clear();
        Session?.ReleaseInputCapture(this);
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
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
            enabled = Session?.Host is IUiClipboardHost clipboard && clipboard.TryGetText(out string value) && !string.IsNullOrEmpty(value);
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
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private bool OpenContextMenuAtCaret()
    {
        EnsureCaretVisible();
        BRect caret = CaretRect(Selection.Focus);
        return OpenContextMenu(new BPoint(caret.Left, caret.Bottom + 2));
    }

    /// <summary>
    /// Whether a point falls inside the current selection, which decides whether
    /// a right-click keeps the selection or moves the caret to the click.
    /// </summary>
    private bool IsPointInSelection(BPoint point)
    {
        if (Selection.IsEmpty || !InnerBounds.Contains(point))
            return false;

        EnsureLayout();
        foreach (VisualLine line in _lines)
        {
            double top = ContentTop + line.Top - _scrollY;
            if (point.Y < top || point.Y >= top + line.Height)
                continue;

            var start = new RichTextPosition(line.ParagraphIndex, line.Start);
            var end = new RichTextPosition(line.ParagraphIndex, line.End);
            if (Selection.End < start || Selection.Start > end)
                continue;

            int from = Selection.Start.ParagraphIndex == line.ParagraphIndex
                ? Math.Clamp(Selection.Start.Offset, line.Start, line.End) : line.Start;
            int to = Selection.End.ParagraphIndex == line.ParagraphIndex
                ? Math.Clamp(Selection.End.Offset, line.Start, line.End) : line.End;
            RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
            double left = LineLeft(line) + AdvanceInLine(line, paragraph, from);
            double right = LineLeft(line) + AdvanceInLine(line, paragraph, to);
            if (right <= left && Selection.Start <= start && Selection.End >= end)
                right = left + BTextMeasurer.MeasureAdvance(" ", ZoomedFont);
            if (point.X >= left && point.X <= right)
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

    private void RenderContextMenu(UiRenderContext context)
    {
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
                double middle = top + Math.Floor(ContextMenuSeparatorHeight / 2);
                context.RenderList.FillRect(new BRect(menu.Left + ContextMenuPaddingX, middle, Math.Max(0, menu.Width - (ContextMenuPaddingX * 2)), 1), ContextMenuBorderColor);
                top += ContextMenuSeparatorHeight;
                continue;
            }

            var row = new BRect(menu.Left, top, menu.Width, ContextMenuItemHeight);
            if (index == _contextMenuHighlightedIndex)
                StandardControlPaint.FillRounded(context.RenderList, StandardControlPaint.Inset(row, 2), ContextMenuHighlight, StandardControlPaint.SmallRadius);

            BColor foreground = item.IsEnabled ? ContextMenuForeground : ContextMenuDisabledForeground;
            double baseline = row.Top + Math.Max(0, (row.Height - lineHeight) / 2);
            context.RenderList.DrawText(new BTextRun(item.Text, Font, foreground), new BPoint(row.Left + ContextMenuPaddingX, baseline));
            if (item.Shortcut.Length > 0)
            {
                double shortcutWidth = BTextMeasurer.MeasureAdvance(item.Shortcut, Font);
                context.RenderList.DrawText(new BTextRun(item.Shortcut, Font, ContextMenuDisabledForeground), new BPoint(row.Right - ContextMenuPaddingX - shortcutWidth, baseline));
            }

            top += ContextMenuItemHeight;
        }

        context.RenderList.PopClip();
    }

    private UiSemanticNode CreateContextMenuSemanticNode() =>
        new(
            UiSemanticRole.Menu,
            "Edit commands",
            GetContextMenuBounds(),
            UiSemanticState.Visible | UiSemanticState.Enabled | UiSemanticState.Expanded,
            []);

    private static bool IsSelectable(StandardRichEditContextMenuItem item) => !item.IsSeparator && item.IsEnabled;

    private static bool IsContextMenuKey(UiInputEvent input, bool shift) =>
        IsKey(input, 0x5D, "ContextMenu") ||
        string.Equals(input.KeyName, "Apps", StringComparison.OrdinalIgnoreCase) ||
        (shift && IsKey(input, 0x79, "F10"));
}
