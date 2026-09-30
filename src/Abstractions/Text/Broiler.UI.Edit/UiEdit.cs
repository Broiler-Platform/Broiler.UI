using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.Edit;

// Broiler-AI:           Origin=AI; Spec=ADR-0009; IP=Low; Security=High; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: a field whose IsPassword is true exposes its plain Text as the semantic node's name or as the text info Value
// Broiler-Human:        PENDING
public abstract class UiEdit : UiElement
{
    private string _text = string.Empty;
    private string _placeholderText = string.Empty;
    private bool _isEnabled = true;
    private bool _isReadOnly;
    private bool _isPassword;
    private int _caretIndex;
    private int _selectionStart;
    private int _selectionLength;
    private int _selectionAnchor;
    private int _maxLength = int.MaxValue;
    private BSize _preferredSize = new(240, 32);
    private UiEditTextDirection _direction;

    protected UiEdit()
    {
        Focusable = true;
    }

    public override bool CanFocus => base.CanFocus && IsEnabled;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiEditTextChangedEventArgs>? TextChanged;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiEditSubmittedEventArgs>? Submitted;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: assigning null stores null rather than an empty string, so the next edit dereferences a null Text
    // Broiler-Human:        PENDING
    public string Text
    {
        get => _text;
        set => SetText(value ?? string.Empty);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning null stores null rather than an empty string, giving a password field's semantic node a null name
    // Broiler-Human:        PENDING
    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_placeholderText, value))
                return;

            _placeholderText = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after IsEnabled is set to false, ReplaceSelection or DeleteRange still changes Text or Submit still raises Submitted
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
    // Broiler-Falsified-If: after IsReadOnly is set to true, ReplaceSelection or DeleteRange still changes Text
    // Broiler-Human:        PENDING
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            ThrowIfDisposed();
            if (_isReadOnly == value)
                return;

            _isReadOnly = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0009; IP=Low; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: setting IsPassword to true on a field that holds text raises no Semantic invalidation, so the host keeps a snapshot whose value is the plain Text
    // Broiler-Human:        PENDING
    public bool IsPassword
    {
        get => _isPassword;
        set
        {
            ThrowIfDisposed();
            if (_isPassword == value)
                return;

            _isPassword = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a caret index past the current Text length is stored unclamped, so a following ReplaceSelection passes it to String.Remove and throws
    // Broiler-Human:        PENDING
    public int CaretIndex
    {
        get => _caretIndex;
        private set => _caretIndex = Math.Clamp(value, 0, Text.Length);
    }

    public int SelectionStart => _selectionStart;

    public int SelectionLength => _selectionLength;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: SelectionEnd reports a position past Text.Length after the text has been shortened
    // Broiler-Human:        PENDING
    public int SelectionEnd => SelectionStart + SelectionLength;

    /// <summary>
    /// The fixed end of the selection — where marking began. Extending the
    /// selection moves the caret and leaves this in place, so a selection made
    /// backwards (a drag or Shift+Left that runs right to left) keeps its caret
    /// at <see cref="SelectionStart"/> rather than flipping to the other end.
    /// </summary>
    public int SelectionAnchor => _selectionAnchor;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool HasSelection => SelectionLength > 0;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: lowering MaxLength below the current Text length leaves Text longer than MaxLength
    // Broiler-Human:        PENDING
    public int MaxLength
    {
        get => _maxLength;
        set
        {
            ThrowIfDisposed();
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "MaxLength must be positive.");
            if (_maxLength == value)
                return;

            _maxLength = value;
            if (Text.Length > _maxLength)
                SetText(Text[.._maxLength]);
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
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred edit size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an undefined UiEditTextDirection value is stored instead of being refused
    // Broiler-Human:        PENDING
    public UiEditTextDirection Direction
    {
        get => _direction;
        set
        {
            ThrowIfDisposed();
            if (_direction == value)
                return;

            _direction = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a start past Text.Length or a length reaching past the end of Text is stored unclamped as the selection
    // Broiler-Human:        PENDING
    public void SetSelection(int start, int length)
    {
        ThrowIfDisposed();
        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        SetSelectionCore(start, length, start + length, start);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: SelectAll on a non-empty field leaves SelectionLength shorter than Text.Length
    // Broiler-Human:        PENDING
    public void SelectAll() => SetSelection(0, Text.Length);

    /// <summary>
    /// Marks the text between a fixed <paramref name="anchor"/> and a moving
    /// <paramref name="caret"/>, which is what a mouse drag and a Shift-extended
    /// key both do. The caret ends up at <paramref name="caret"/> whichever side
    /// of the anchor that is.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a caret before the anchor leaves CaretIndex at SelectionEnd instead of SelectionStart
    // Broiler-Human:        PENDING
    protected void SetSelectionFromAnchor(int anchor, int caret)
    {
        ThrowIfDisposed();
        anchor = Math.Clamp(anchor, 0, Text.Length);
        caret = Math.Clamp(caret, 0, Text.Length);
        SetSelectionCore(Math.Min(anchor, caret), Math.Abs(caret - anchor), caret, anchor);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Submit on a field whose IsEnabled is false raises Submitted
    // Broiler-Human:        PENDING
    public void Submit()
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return;

        Submitted?.Invoke(this, new UiEditSubmittedEventArgs(Text));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an insertion cut to fit MaxLength between the two halves of a surrogate pair leaves a lone high surrogate at the end of Text
    // Broiler-Human:        PENDING
    protected bool ReplaceSelection(string insertedText)
    {
        ThrowIfDisposed();
        if (IsReadOnly || !IsEnabled)
            return false;

        insertedText ??= string.Empty;
        int start = HasSelection ? SelectionStart : CaretIndex;
        int deleteLength = HasSelection ? SelectionLength : 0;
        int allowed = Math.Max(0, MaxLength - (Text.Length - deleteLength));
        if (insertedText.Length > allowed)
            insertedText = insertedText[..allowed];

        string newText = Text.Remove(start, deleteLength).Insert(start, insertedText);
        SetTextCore(newText, start + insertedText.Length);
        return insertedText.Length > 0 || deleteLength > 0;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: DeleteRange removes text from a field whose IsReadOnly is true or whose IsEnabled is false
    // Broiler-Human:        PENDING
    protected bool DeleteRange(int start, int length)
    {
        ThrowIfDisposed();
        if (IsReadOnly || !IsEnabled || length <= 0)
            return false;

        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        if (length == 0)
            return false;

        SetTextCore(Text.Remove(start, length), start);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a Shift extension from a collapsed caret anchors at a SelectionAnchor left by an earlier selection instead of at the current caret
    // Broiler-Human:        PENDING
    protected void MoveCaret(int index, bool extendSelection)
    {
        ThrowIfDisposed();
        index = Math.Clamp(index, 0, Text.Length);
        if (extendSelection)
            SetSelectionFromAnchor(HasSelection ? SelectionAnchor : CaretIndex, index);
        else
            SetSelection(index, 0);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an index past Text.Length leaves the caret beyond the end of Text
    // Broiler-Human:        PENDING
    protected void SetCaretIndex(int index) => SetSelection(Math.Clamp(index, 0, Text.Length), 0);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the Edit node built for a field whose IsPassword is true carries its plain Text as the node name or as the text info Value
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Edit,
            GetSemanticName(),
            Bounds,
            CreateSemanticState(),
            [],
            CreateSemanticTextInfo());

    protected virtual bool IsCompositionActive => false;

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an edit whose IsReadOnly is true reports no ReadOnly semantic state
    // Broiler-Human:        PENDING
    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (IsReadOnly)
            state |= UiSemanticState.ReadOnly;
        return state;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a selection change that moves only the caret returns early without a Render invalidation, leaving the caret drawn at its old position
    // Broiler-Human:        PENDING
    private void SetSelectionCore(int start, int length, int caret, int anchor)
    {
        // The anchor is not drawn, so it is recorded even when nothing else
        // moved: a click that lands on the caret still restarts marking there.
        _selectionAnchor = anchor;
        if (_selectionStart == start && _selectionLength == length && _caretIndex == caret)
            return;

        _selectionStart = start;
        _selectionLength = length;
        CaretIndex = caret;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a string longer than MaxLength is stored untruncated and reported whole in TextChanged
    // Broiler-Human:        PENDING
    private void SetText(string text)
    {
        ThrowIfDisposed();
        if (text.Length > MaxLength)
            text = text[..MaxLength];

        SetTextCore(text, Math.Min(text.Length, CaretIndex));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a changed text keeps the previous SelectionStart and SelectionLength, so a later ReplaceSelection removes a range past the end of the shorter text
    // Broiler-Human:        PENDING
    private void SetTextCore(string text, int caretIndex)
    {
        string oldText = _text;
        if (StringComparer.Ordinal.Equals(oldText, text))
        {
            SetSelection(Math.Clamp(caretIndex, 0, text.Length), 0);
            return;
        }

        _text = text;
        _selectionStart = 0;
        _selectionLength = 0;
        CaretIndex = caretIndex;
        _selectionAnchor = _caretIndex;
        TextChanged?.Invoke(this, new UiEditTextChangedEventArgs(oldText, text));
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=High; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a field whose IsPassword is true, with non-empty Text and an empty PlaceholderText, is named by its Text instead of a generic label
    // Broiler-Human:        PENDING
    private string GetSemanticName()
    {
        if (IsPassword)
            return string.IsNullOrEmpty(PlaceholderText) ? "Password field" : PlaceholderText;
        if (!string.IsNullOrEmpty(Text))
            return Text;
        return PlaceholderText;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=High; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a field whose IsPassword is true yields text info whose Value is its Text rather than null
    // Broiler-Human:        PENDING
    private UiSemanticTextInfo CreateSemanticTextInfo() =>
        new(
            IsPassword ? null : Text,
            CaretIndex,
            SelectionStart,
            SelectionLength,
            IsEnabled && !IsReadOnly,
            IsPassword,
            IsCompositionActive);
}
