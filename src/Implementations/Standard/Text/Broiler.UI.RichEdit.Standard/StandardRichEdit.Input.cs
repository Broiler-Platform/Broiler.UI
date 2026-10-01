using System;
using System.Globalization;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.Input.Touch;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// Input: pointer, wheel, touch, keyboard, text, and IME composition, and the
/// caret navigation they drive. The scroll state they move lives in the
/// <see cref="RichEditScroller"/>; what stays here is deciding what each event
/// means for the document and the selection.
/// </summary>
public sealed partial class StandardRichEdit
{
    // --- Pointer, wheel, and touch -----------------------------------------

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton == MouseButton.Right)
            return HandleContextMenuPointerRequest(input);

        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            Session?.CaptureInput(this);
            if (TryBeginScrollbarInteraction(input.Position))
                return true;

            RichTextPosition position = PositionFromPoint(input.Position);
            if (Session is not null && _clicks.IsDoubleClick(Session.Clock.Now, input.Position))
            {
                Selection = RichEditWordNavigation.WordAt(Document, position);
                EnsureCaretVisible();
            }
            else
            {
                Selection = RichTextRange.Caret(position);
                EnsureCaretVisible();
            }

            _clicks.Record(Session?.Clock.Now ?? default, input.Position);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            _scroller.EndThumbDrag();
            Session?.ReleaseInputCapture(this);
            return true;
        }

        return false;
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (Session?.CapturedElement != this)
            return false;

        if (_scroller.IsDraggingThumb)
        {
            double before = _scroller.Offset;
            _scroller.DragThumb(input.Position.Y, ScrollMetrics);
            InvalidateIfScrolled(before);
            return true;
        }

        if (_scroller.IsDraggingHorizontalThumb)
        {
            double before = _scroller.OffsetX;
            _scroller.DragHorizontalThumb(input.Position.X, HorizontalScrollMetrics);
            if (_scroller.OffsetX != before)
                Invalidate(UiInvalidationKind.Render);
            return true;
        }

        RichTextPosition position = PositionFromPoint(input.Position);
        Selection = new RichTextRange(Selection.Anchor, position);
        EnsureCaretVisible();
        return true;
    }

    private bool HandleWheel(UiInputEvent input)
    {
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);
        if (shift && HorizontalScrollPolicy != RichEditScrollPolicy.Never)
        {
            double delta = input.WheelDeltaNotches * DefaultLineHeight * 3;
            if (_scroller.ScrollToX(_scroller.OffsetX - delta, HorizontalScrollMetrics))
            {
                Invalidate(UiInvalidationKind.Render);
                return true;
            }
            return false;
        }

        if (VerticalScrollPolicy == RichEditScrollPolicy.Never)
            return false;

        if (!_scroller.ScrollTo(_scroller.Offset - input.WheelDeltaNotches * DefaultLineHeight * 3, ScrollMetrics))
            return false;

        Invalidate(UiInvalidationKind.Render);
        return true;
    }

    private bool HandleTouch(UiInputEvent input)
    {
        if (input.TouchContactState is not TouchContactState state)
            return false;

        double before = _scroller.Offset;
        bool handled = _scroller.Touch(input.ContactId, state, input.Position, ScrollMetrics);
        InvalidateIfScrolled(before);
        return handled;
    }

    private bool TryBeginScrollbarInteraction(BPoint position)
    {
        double before = _scroller.Offset;
        if (_scroller.TryPressScrollbar(position, ScrollMetrics))
        {
            InvalidateIfScrolled(before);
            return true;
        }

        double beforeX = _scroller.OffsetX;
        if (_scroller.TryPressHorizontalScrollbar(position, HorizontalScrollMetrics))
        {
            if (_scroller.OffsetX != beforeX)
                Invalidate(UiInvalidationKind.Render);
            return true;
        }

        return false;
    }

    private void InvalidateIfScrolled(double before)
    {
        if (_scroller.Offset != before)
            Invalidate(UiInvalidationKind.Render);
    }

    // --- Text and IME ------------------------------------------------------

    private bool HandleTextInput(UiInputEvent input)
    {
        if (IsReadOnly)
            return false;
        return InsertCommittedText(input.Text ?? string.Empty);
    }

    private bool HandleTextComposition(UiInputEvent input)
    {
        if (IsReadOnly)
        {
            _compositionText = string.Empty;
            return false;
        }

        TextCompositionState state = input.CompositionState ?? TextCompositionState.Updated;
        if (state is TextCompositionState.Started or TextCompositionState.Updated)
        {
            _compositionText = input.Text ?? string.Empty;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            return true;
        }

        if (state == TextCompositionState.Committed)
        {
            _compositionText = string.Empty;
            return InsertCommittedText(input.Text ?? string.Empty);
        }

        _compositionText = string.Empty;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    private bool InsertCommittedText(string text)
    {
        if (IsReadOnly)
            return false;

        text = SanitizeCommittedText(text);
        if (text.Length == 0)
            return false;

        bool changed = ExecuteCommand(RichEditCommand.InsertText, text);
        EnsureCaretVisible();
        return changed;
    }

    /// <summary>
    /// Drops the control characters from text a platform commits. Tab is one of
    /// them on purpose: Windows and Android deliver a pressed Tab as a key event
    /// and again as committed text, and <see cref="HandleTab"/> already answered
    /// the key, so keeping it here would type two tabs for one press.
    /// </summary>
    private static string SanitizeCommittedText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (!char.IsControl(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    // --- Keyboard ----------------------------------------------------------

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        bool control = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);

        if (IsContextMenuKey(input, shift))
            return OpenContextMenuAtCaret();

        if (control && HandleControlChord(input))
            return true;

        // Ctrl+Tab and Alt+Tab belong to the application and the desktop, not to
        // the text, so only a plain or shifted Tab is the editor's to answer.
        bool alt = input.KeyModifiers.HasFlag(KeyboardModifierState.Alt);
        if (!control && !alt && IsKey(input, BVirtualKey.Tab, "Tab"))
        {
            if (IsReadOnly)
                return false;
            return HandleTab(shift);
        }

        if (IsKey(input, BVirtualKey.Enter, "Enter"))
        {
            if (IsReadOnly)
            {
                if (!AcceptsReturn)
                    Submit();
                return false;
            }

            if (shift)
                RunCommand(RichEditCommand.InsertLineBreak);
            else if (AcceptsReturn)
                RunCommand(RichEditCommand.InsertParagraphBreak);
            else
                Submit();
            return true;
        }
        if (IsKey(input, BVirtualKey.Back, "Backspace"))
        {
            if (IsReadOnly)
                return false;
            if (DeleteBackward())
                EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, 0x2E, "Delete"))
        {
            if (IsReadOnly)
                return false;
            if (DeleteForward())
                EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Left, "Left"))
        {
            MoveFocusTo(control
                ? RichEditWordNavigation.WordLeft(Document, Selection.Focus)
                : Document.PositionLeftOf(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Right, "Right"))
        {
            MoveFocusTo(control
                ? RichEditWordNavigation.WordRight(Document, Selection.Focus)
                : Document.PositionRightOf(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Up, "Up"))
        {
            MoveVertical(-1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Down, "Down"))
        {
            MoveVertical(1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Home, "Home"))
        {
            MoveFocusTo(control ? RichTextDocument.Start : VisualLineStart(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.End, "End"))
        {
            MoveFocusTo(control ? Document.End : VisualLineEnd(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
        {
            PageMove(-1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
        {
            PageMove(1, shift);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles the Tab key. In running text it types a tab, and the text after it
    /// is laid out at the next tab stop. Where a tab sets the level of a paragraph
    /// rather than the position of a word — a list item whose text the caret sits
    /// in front of, or a selection covering more than one paragraph — Tab demotes
    /// and Shift+Tab promotes, which is what Tab does in a word processor's lists.
    /// Shift+Tab in running text takes back the tab in front of the caret, and
    /// outdents the paragraph when there is no tab to take back.
    /// </summary>
    /// <remarks>
    /// The key is answered here rather than through the tab that some platforms
    /// also deliver as text input: <see cref="SanitizeCommittedText"/> drops that
    /// one, so a single press types a single tab on every head.
    /// </remarks>
    private bool HandleTab(bool shift)
    {
        if (TabSetsParagraphLevel())
            return RunCommand(shift ? RichEditCommand.Outdent : RichEditCommand.Indent);

        if (!shift)
            return RunCommand(RichEditCommand.InsertText, "\t");

        if (Selection.IsEmpty && IsTabBeforeCaret())
        {
            if (DeleteBackward())
                EnsureCaretVisible();
            return true;
        }

        if (CaretParagraph.Style.IndentLevel > 0)
            return RunCommand(RichEditCommand.Outdent);

        return true;
    }

    /// <summary>
    /// Whether Tab should change paragraph levels instead of typing a tab: the
    /// selection covers more than one paragraph, or the caret sits in front of the
    /// text of a list item, where a word processor demotes the item.
    /// </summary>
    private bool TabSetsParagraphLevel()
    {
        RichTextRange selection = Selection;
        if (selection.Start.ParagraphIndex != selection.End.ParagraphIndex)
            return true;

        return selection.IsEmpty &&
               CaretParagraph.Style.ListKind != ListKind.None &&
               Document.ClampPosition(selection.Focus).Offset == 0;
    }

    private bool IsTabBeforeCaret()
    {
        RichTextPosition caret = Document.ClampPosition(Selection.Focus);
        return caret.Offset > 0 && CaretParagraph.Text[caret.Offset - 1] == '\t';
    }

    private RichTextParagraph CaretParagraph =>
        Document.Paragraphs[Document.ClampPosition(Selection.Focus).ParagraphIndex];

    /// <summary>
    /// Handles the Ctrl-modified editing, clipboard, history, and inline-format
    /// shortcuts. Ctrl with a navigation key (arrows, Home, End) is left to the
    /// navigation handlers. Returns true when the chord was recognized.
    /// </summary>
    private bool HandleControlChord(UiInputEvent input)
    {
        if (IsKey(input, BVirtualKey.A, "A"))
        {
            SelectAllInternal();
            return true;
        }
        if (IsKey(input, BVirtualKey.C, "C"))
        {
            RunCommand(RichEditCommand.Copy);
            return true;
        }
        if (IsReadOnly)
            return false;

        if (IsKey(input, 0x58, "X"))
        {
            RunCommand(RichEditCommand.Cut);
            return true;
        }
        if (IsKey(input, 0x56, "V"))
        {
            RunCommand(RichEditCommand.Paste);
            return true;
        }
        if (IsKey(input, 0x5A, "Z"))
        {
            RunCommand(RichEditCommand.Undo);
            return true;
        }
        if (IsKey(input, 0x59, "Y"))
        {
            RunCommand(RichEditCommand.Redo);
            return true;
        }
        if (IsKey(input, 0x42, "B"))
        {
            RunCommand(RichEditCommand.Bold);
            return true;
        }
        if (IsKey(input, 0x49, "I"))
        {
            RunCommand(RichEditCommand.Italic);
            return true;
        }
        if (IsKey(input, 0x55, "U"))
        {
            RunCommand(RichEditCommand.Underline);
            return true;
        }

        return false;
    }

    /// <summary>Runs a command through the shared undo model and keeps the caret in view.</summary>
    private bool RunCommand(RichEditCommand command, object? parameter = null)
    {
        bool changed = ExecuteCommand(command, parameter);
        EnsureCaretVisible();
        return changed;
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    // --- Navigation --------------------------------------------------------

    private void SelectAllInternal()
    {
        Selection = new RichTextRange(RichTextDocument.Start, Document.End);
        EnsureCaretVisible();
    }

    private void MoveFocusTo(RichTextPosition target, bool extend)
    {
        RichTextPosition anchor = extend ? Selection.Anchor : target;
        Selection = new RichTextRange(anchor, target);
        EnsureCaretVisible();
    }

    private void MoveVertical(int direction, bool extend)
    {
        EnsureLayout();
        double contentLeft = View.ContentLeft;
        (VisualLine _, int index) = _layout.LineForPosition(Selection.Focus);
        double caretX = _layout.CaretX(Selection.Focus, contentLeft);
        int target = index + direction;
        if (target < 0)
        {
            MoveFocusTo(RichTextDocument.Start, extend);
            return;
        }
        if (target >= _layout.Lines.Count)
        {
            MoveFocusTo(Document.End, extend);
            return;
        }

        MoveFocusTo(_layout.PositionInLineAtX(_layout.Lines[target], caretX, contentLeft), extend);
    }

    private void PageMove(int direction, bool extend)
    {
        EnsureLayout();
        double contentLeft = View.ContentLeft;
        int linesPerPage = Math.Max(1, (int)(InnerBounds.Height / DefaultLineHeight));
        (VisualLine _, int index) = _layout.LineForPosition(Selection.Focus);
        double caretX = _layout.CaretX(Selection.Focus, contentLeft);
        int target = Math.Clamp(index + (direction * linesPerPage), 0, _layout.Lines.Count - 1);
        MoveFocusTo(_layout.PositionInLineAtX(_layout.Lines[target], caretX, contentLeft), extend);
    }

    private RichTextPosition VisualLineStart(RichTextPosition position)
    {
        EnsureLayout();
        VisualLine line = _layout.LineForPosition(position).Line;
        return new RichTextPosition(line.ParagraphIndex, line.Start);
    }

    private RichTextPosition VisualLineEnd(RichTextPosition position)
    {
        EnsureLayout();
        VisualLine line = _layout.LineForPosition(position).Line;
        return new RichTextPosition(line.ParagraphIndex, line.End);
    }

    private void EnsureCaretVisible()
    {
        EnsureLayout();
        double viewport = InnerBounds.Height;
        bool changed = false;
        if (viewport > 0)
        {
            VisualLine line = _layout.LineForPosition(Selection.Focus).Line;
            double newScroll = _scroller.Offset;
            if (line.Top < newScroll)
                newScroll = line.Top;
            else if (line.Top + line.Height > newScroll + viewport)
                newScroll = line.Top + line.Height - viewport;

            if (_scroller.ScrollTo(newScroll, ScrollMetrics))
                changed = true;
        }

        double viewportWidth = InnerBounds.Width;
        if (viewportWidth > 0 && HorizontalScrollPolicy != RichEditScrollPolicy.Never)
        {
            double caretContentX = _layout.CaretX(Selection.Focus, 0);
            double newScrollX = _scroller.OffsetX;
            if (caretContentX < newScrollX)
                newScrollX = caretContentX;
            else if (caretContentX > newScrollX + viewportWidth - 20)
                newScrollX = caretContentX - viewportWidth + 20;

            if (_scroller.ScrollToX(newScrollX, HorizontalScrollMetrics))
                changed = true;
        }

        if (changed)
            Invalidate(UiInvalidationKind.Render);
    }
}
