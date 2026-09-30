using System;
using System.Linq;
using Broiler.Documents.FormatCodes;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.FormatCodeView;

/// <summary>
/// Platform-neutral state and commands for a token-aware Formatting Codes view.
/// Rendering and input are supplied by the Standard implementation. Canonical
/// bracket text is never reparsed for interaction; typed projector mappings are
/// authoritative.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0020; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: a selection covering only part of a code token such as [Bold ON] is turned into a ReplaceFormatCodeTextIntent and raised through EditRequested
// Broiler-Human:        PENDING
public abstract class UiFormatCodeView : UiElement
{
    private FormatCodeProjection? _projection;
    private bool _isEnabled = true;
    private bool _isEditable;
    private BSize _preferredSize = new(320, 160);
    private FormatCodeViewWrapping _wrapping = FormatCodeViewWrapping.Wrap;
    private FormatCodeViewScrollPolicy _verticalScrollPolicy = FormatCodeViewScrollPolicy.Auto;
    private FormatCodeViewScrollPolicy _horizontalScrollPolicy = FormatCodeViewScrollPolicy.Auto;
    private int _selectionAnchor;
    private int _selectionFocus;
    private string _searchQuery = string.Empty;
    private bool _searchMatchCase;

    protected UiFormatCodeView()
    {
        Focusable = true;
    }

    public override bool CanFocus => base.CanFocus && IsEnabled;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<FormatCodeViewSelectionChangedEventArgs>? SelectionChanged;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<FormatCodeNavigationRequestedEventArgs>? NavigationRequested;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<FormatCodeEditRequestedEventArgs>? EditRequested;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? UndoRequested;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? RedoRequested;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? ExitRequested;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? SearchRequested;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after Projection is replaced by one with shorter Text, SelectionAnchor or SelectionFocus is left greater than the new Text.Length
    // Broiler-Human:        PENDING
    public FormatCodeProjection? Projection
    {
        get => _projection;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_projection, value))
                return;

            _projection = value;
            int length = Text.Length;
            int anchor = Math.Clamp(_selectionAnchor, 0, length);
            int focus = Math.Clamp(_selectionFocus, 0, length);
            bool selectionChanged = anchor != _selectionAnchor || focus != _selectionFocus;
            _selectionAnchor = anchor;
            _selectionFocus = focus;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange |
                UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            if (selectionChanged)
                SelectionChanged?.Invoke(this, new FormatCodeViewSelectionChangedEventArgs(anchor, focus));
        }
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: Text returns null rather than an empty string while Projection is null
    // Broiler-Human:        PENDING
    public string Text => Projection?.Text ?? string.Empty;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after IsEnabled is set to false, RequestTextReplacement or RequestTokenRemoval still raises EditRequested
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
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after IsEditable is set to false, Paste or RequestTextReplacement still raises EditRequested
    // Broiler-Human:        PENDING
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            ThrowIfDisposed();
            if (_isEditable == value)
                return;
            _isEditable = value;
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
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_preferredSize == value)
                return;
            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public FormatCodeViewWrapping Wrapping
    {
        get => _wrapping;
        set
        {
            ThrowIfDisposed();
            if (_wrapping == value)
                return;
            _wrapping = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public FormatCodeViewScrollPolicy VerticalScrollPolicy
    {
        get => _verticalScrollPolicy;
        set
        {
            ThrowIfDisposed();
            if (_verticalScrollPolicy == value)
                return;
            _verticalScrollPolicy = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public FormatCodeViewScrollPolicy HorizontalScrollPolicy
    {
        get => _horizontalScrollPolicy;
        set
        {
            ThrowIfDisposed();
            if (_horizontalScrollPolicy == value)
                return;
            _horizontalScrollPolicy = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public int SelectionAnchor => _selectionAnchor;

    public int SelectionFocus => _selectionFocus;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: SelectionStart returns the anchor when the focus precedes it
    // Broiler-Human:        PENDING
    public int SelectionStart => Math.Min(_selectionAnchor, _selectionFocus);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public int SelectionEnd => Math.Max(_selectionAnchor, _selectionFocus);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: SelectionLength is negative for a selection whose focus precedes its anchor
    // Broiler-Human:        PENDING
    public int SelectionLength => SelectionEnd - SelectionStart;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool HasSelection => SelectionLength > 0;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public int CaretOffset => _selectionFocus;

    public string SearchQuery => _searchQuery;

    public bool SearchMatchCase => _searchMatchCase;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public FormatCodeToken? CurrentToken => TokenAt(CaretOffset);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an anchor or focus greater than Text.Length is stored unclamped, so GetSelectedText throws ArgumentOutOfRangeException
    // Broiler-Human:        PENDING
    public void SetSelection(int anchor, int focus)
    {
        ThrowIfDisposed();
        int length = Text.Length;
        anchor = Math.Clamp(anchor, 0, length);
        focus = Math.Clamp(focus, 0, length);
        if (_selectionAnchor == anchor && _selectionFocus == focus)
            return;

        _selectionAnchor = anchor;
        _selectionFocus = focus;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        SelectionChanged?.Invoke(this, new FormatCodeViewSelectionChangedEventArgs(anchor, focus));
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after SelectAll the selection ends short of Text.Length, so a following CutSelection leaves trailing text in place
    // Broiler-Human:        PENDING
    public void SelectAll() => SetSelection(0, Text.Length);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a selection whose focus precedes its anchor returns text starting at the anchor rather than at SelectionStart
    // Broiler-Human:        PENDING
    public string GetSelectedText() => HasSelection
        ? Text.Substring(SelectionStart, SelectionLength)
        : string.Empty;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: CopySelection on a view whose IsEnabled is false writes the selected text to the clipboard
    // Broiler-Human:        PENDING
    public bool CopySelection()
    {
        ThrowIfDisposed();
        if (!IsEnabled || !HasSelection || Session?.Host is not IUiClipboardHost clipboard)
            return false;
        clipboard.SetText(GetSelectedText());
        return true;
    }

    /// <summary>
    /// Requests replacement of the current projected selection. Only ordinary
    /// text spans and whole escape tokens are accepted; code tokens stay atomic.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: on a selection covering only part of a code token, RequestTextReplacement returns true and raises EditRequested
    // Broiler-Human:        PENDING
    public bool RequestTextReplacement(string text)
    {
        ThrowIfDisposed();
        text ??= string.Empty;
        if (!IsEnabled || !IsEditable || Projection is null ||
            !TryMapEditableTextRange(SelectionStart, SelectionEnd, out var range))
        {
            return false;
        }

        EditRequested?.Invoke(this, new FormatCodeEditRequestedEventArgs(
            new ReplaceFormatCodeTextIntent(range, text)));
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: CutSelection writes the selection to the clipboard after RequestTextReplacement refused it for a read-only view or a partly covered code token
    // Broiler-Human:        PENDING
    public bool CutSelection()
    {
        ThrowIfDisposed();
        if (!HasSelection || Session?.Host is not IUiClipboardHost clipboard)
            return false;
        string selected = GetSelectedText();
        if (!RequestTextReplacement(string.Empty))
            return false;
        clipboard.SetText(selected);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: Paste on a view whose IsEditable is false raises EditRequested carrying the clipboard text
    // Broiler-Human:        PENDING
    public bool Paste()
    {
        ThrowIfDisposed();
        return Session?.Host is IUiClipboardHost clipboard &&
            clipboard.TryGetText(out string text) && RequestTextReplacement(text);
    }

    /// <summary>Requests the semantic removal attached to the token at the caret.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: RequestTokenRemoval on a view whose IsEditable is false raises EditRequested with a removal intent
    // Broiler-Human:        PENDING
    public bool RequestTokenRemoval(bool backward = false)
    {
        ThrowIfDisposed();
        if (!IsEnabled || !IsEditable || Projection is null)
            return false;

        FormatCodeToken? token = TokenForRemoval(backward);
        if (token?.EditDescriptor is not FormatCodeTokenEditDescriptor descriptor)
            return false;
        EditRequested?.Invoke(this,
            new FormatCodeEditRequestedEventArgs(descriptor.RemovalIntent, token));
        return true;
    }

    /// <summary>Requests an already typed property edit supplied by host UI.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: RequestEdit raises EditRequested on a view whose IsEditable or IsEnabled is false
    // Broiler-Human:        PENDING
    public bool RequestEdit(FormatCodeEditIntent intent, FormatCodeToken? token = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(intent);
        if (!IsEnabled || !IsEditable || Projection is null)
            return false;
        EditRequested?.Invoke(this, new FormatCodeEditRequestedEventArgs(intent, token));
        return true;
    }

    /// <summary>Requests a typed Insert Code palette action at the mapped selection.</summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0020; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a palette entry that needs a typed value, such as FontSize with a null value, throws out of RequestPaletteEntry instead of returning false
    // Broiler-Human:        PENDING
    public bool RequestPaletteEntry(FormatCodePaletteEntry entry, object? value = null)
    {
        ThrowIfDisposed();
        if (!IsEnabled || !IsEditable || Projection is null)
            return false;
        RichTextPosition anchor = Projection.MapProjectedOffset(SelectionAnchor).DocumentPosition;
        RichTextPosition focus = Projection.MapProjectedOffset(SelectionFocus).DocumentPosition;
        try
        {
            return RequestEdit(FormatCodeInsertPalette.Create(
                entry, new RichTextRange(anchor, focus), value));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: Find with an empty query changes the selection
    // Broiler-Human:        PENDING
    public bool Find(string query, bool matchCase = false, bool wrap = true)
    {
        ThrowIfDisposed();
        query ??= string.Empty;
        _searchQuery = query;
        _searchMatchCase = matchCase;
        if (query.Length == 0 || Text.Length == 0)
            return false;

        return FindFrom(HasSelection ? SelectionEnd : CaretOffset, forward: true, wrap);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: FindNext changes the selection when no query has been set by Find
    // Broiler-Human:        PENDING
    public bool FindNext(bool wrap = true) =>
        _searchQuery.Length > 0 && FindFrom(SelectionEnd, forward: true, wrap);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: FindPrevious from a selection that is itself a match reselects that match while an earlier match exists
    // Broiler-Human:        PENDING
    public bool FindPrevious(bool wrap = true) =>
        _searchQuery.Length > 0 && FindFrom(SelectionStart, forward: false, wrap);

    // Broiler-AI:           Origin=AI; Spec=ADR-0020; IP=Low; Security=Low; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: the description of a ParagraphCode token lacks the phrase visual rendering pending
    // Broiler-Human:        PENDING
    public string GetAccessibleTokenDescription()
    {
        FormatCodeToken? pending = Projection?.PendingTokens.FirstOrDefault(token => token.ProjectedStart == CaretOffset);
        FormatCodeToken? token = pending ?? CurrentToken;
        if (token is null)
            return "Formatting Codes, empty";

        string category = token.Kind switch
        {
            FormatCodeTokenKind.Text => "document text",
            FormatCodeTokenKind.InlineCode => "inline formatting code",
            FormatCodeTokenKind.ParagraphCode => "paragraph formatting code; engine state; visual rendering pending",
            FormatCodeTokenKind.StructureCode => "document structure code",
            FormatCodeTokenKind.Escape => "escaped document content",
            FormatCodeTokenKind.PendingCode => "pending formatting",
            FormatCodeTokenKind.Diagnostic => "projection diagnostic",
            _ => "formatting code",
        };
        return $"{token.DisplayText}, {category}";
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: MoveCaret with extendSelection true moves SelectionAnchor instead of keeping it
    // Broiler-Human:        PENDING
    protected void MoveCaret(int offset, bool extendSelection)
    {
        int target = Math.Clamp(offset, 0, Text.Length);
        SetSelection(extendSelection ? SelectionAnchor : target, target);
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0020; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a pointer offset greater than Text.Length reaches Projection.MapProjectedOffset unclamped and throws ArgumentOutOfRangeException
    // Broiler-Human:        PENDING
    protected void ActivateAt(int projectedOffset)
    {
        if (!IsEnabled || Projection is null)
            return;
        projectedOffset = Math.Clamp(projectedOffset, 0, Text.Length);
        FormatCodeMappedPosition mapping = Projection.MapProjectedOffset(projectedOffset);
        NavigationRequested?.Invoke(
            this,
            new FormatCodeNavigationRequestedEventArgs(mapping, TokenAt(projectedOffset)));
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected void RequestSearch() => SearchRequested?.Invoke(this, EventArgs.Empty);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected void RequestUndo() => UndoRequested?.Invoke(this, EventArgs.Empty);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected void RequestRedo() => RedoRequested?.Invoke(this, EventArgs.Empty);

    protected virtual bool IsCompositionActive => false;

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a view whose IsEditable is false produces a node without the ReadOnly semantic state
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible
            ? UiSemanticState.Visible
            : UiSemanticState.None;
        if (!IsEditable)
            state |= UiSemanticState.ReadOnly;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (HasSelection)
            state |= UiSemanticState.Selected;

        return new UiSemanticNode(
            UiSemanticRole.FormatCodeView,
            $"Formatting Codes. {GetAccessibleTokenDescription()}",
            Bounds,
            state,
            [],
            new UiSemanticTextInfo(
                Text,
                CaretOffset,
                SelectionStart,
                SelectionLength,
                IsEditable,
                IsPassword: false,
                IsCompositionActive));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: with wrap on, an occurrence that starts before the forward search origin and ends after it is never found (Text xab, query ab, caret at 2 returns false)
    // Broiler-Human:        PENDING
    private bool FindFrom(int origin, bool forward, bool wrap)
    {
        StringComparison comparison = _searchMatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        int index;
        if (forward)
        {
            origin = Math.Clamp(origin, 0, Text.Length);
            index = Text.IndexOf(_searchQuery, origin, comparison);
            if (index < 0 && wrap && origin > 0)
                index = Text.IndexOf(_searchQuery, 0, origin, comparison);
        }
        else
        {
            origin = Math.Clamp(origin - 1, -1, Text.Length - 1);
            index = origin >= 0 ? Text.LastIndexOf(_searchQuery, origin, comparison) : -1;
            if (index < 0 && wrap && Text.Length > 0)
                index = Text.LastIndexOf(_searchQuery, Text.Length - 1, comparison);
        }

        if (index < 0)
            return false;
        SetSelection(index, index + _searchQuery.Length);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an offset equal to a token's ProjectedStart + ProjectedLength returns that token rather than the token starting there
    // Broiler-Human:        PENDING
    private FormatCodeToken? TokenAt(int projectedOffset)
    {
        if (Projection is null || Projection.Tokens.Count == 0)
            return null;
        projectedOffset = Math.Clamp(projectedOffset, 0, Text.Length);
        if (projectedOffset == Text.Length)
            return Projection.Tokens[^1];

        int low = 0;
        int high = Projection.Tokens.Count - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            FormatCodeToken token = Projection.Tokens[middle];
            if (projectedOffset < token.ProjectedStart)
                high = middle - 1;
            else if (projectedOffset >= token.ProjectedStart + token.ProjectedLength)
                low = middle + 1;
            else
                return token;
        }

        return null;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0020; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a range that starts inside an InlineCode token and ends in the following text is mapped and reported as editable
    // Broiler-Human:        PENDING
    private bool TryMapEditableTextRange(int start, int end, out Broiler.Documents.Model.RichTextRange range)
    {
        range = default;
        if (Projection is null)
            return false;
        start = Math.Clamp(start, 0, Text.Length);
        end = Math.Clamp(end, start, Text.Length);

        if (start == end)
        {
            FormatCodeToken? token = EditableTokenAtBoundary(start);
            if (token is null && !IsEmptySourceBoundary(start))
                return false;
        }
        else
        {
            foreach (FormatCodeToken token in Projection.Tokens)
            {
                int tokenStart = token.ProjectedStart;
                int tokenEnd = tokenStart + token.ProjectedLength;
                if (tokenEnd <= start || tokenStart >= end)
                    continue;
                if (token.Kind == FormatCodeTokenKind.Text)
                    continue;
                if (token.Kind == FormatCodeTokenKind.Escape && start <= tokenStart && end >= tokenEnd)
                    continue;
                if (start < tokenStart && end > tokenEnd)
                    continue; // atomic token wholly enclosed by a cross-run text selection
                return false;
            }
        }

        var mappedStart = Projection.MapProjectedOffset(start);
        var mappedEnd = Projection.MapProjectedOffset(end);
        range = new Broiler.Documents.Model.RichTextRange(
            mappedStart.DocumentPosition,
            mappedEnd.DocumentPosition);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a caret at the end of a Text token that is followed by a code token finds no editable token, so typing there is refused
    // Broiler-Human:        PENDING
    private FormatCodeToken? EditableTokenAtBoundary(int offset)
    {
        if (Projection is null)
            return null;
        FormatCodeToken? exact = TokenAt(offset);
        if (exact?.Kind == FormatCodeTokenKind.Text)
            return exact;
        for (int i = Projection.Tokens.Count - 1; i >= 0; i--)
        {
            FormatCodeToken token = Projection.Tokens[i];
            if (token.ProjectedStart + token.ProjectedLength == offset &&
                token.Kind == FormatCodeTokenKind.Text)
            {
                return token;
            }
        }
        return null;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a caret strictly inside the display text of [Empty Paragraph] is accepted as an insertion point
    // Broiler-Human:        PENDING
    private bool IsEmptySourceBoundary(int offset)
    {
        if (Projection is null)
            return false;
        FormatCodeMappedPosition mapping = Projection.MapProjectedOffset(offset);
        FormatCodeToken? token = TokenAt(offset);
        return mapping.AffectedRange is RichTextRange affected && affected.IsEmpty &&
            (offset == 0 || offset == Text.Length || token is not null &&
             (offset == token.ProjectedStart ||
              offset == token.ProjectedStart + token.ProjectedLength));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: with backward false and the caret at Text.Length it returns the last token, which ends at the caret, so Delete removes the code before the caret
    // Broiler-Human:        PENDING
    private FormatCodeToken? TokenForRemoval(bool backward)
    {
        if (Projection is null)
            return null;
        int offset = CaretOffset;
        if (!backward)
            return TokenAt(offset);
        for (int i = Projection.Tokens.Count - 1; i >= 0; i--)
        {
            FormatCodeToken token = Projection.Tokens[i];
            if (token.ProjectedStart + token.ProjectedLength <= offset)
                return token;
        }
        return null;
    }
}
