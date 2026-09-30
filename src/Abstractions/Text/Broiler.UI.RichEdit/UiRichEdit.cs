using System;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit;

/// <summary>
/// The platform-neutral abstraction for a formatted, multi-paragraph text editor
/// (ADR 0013). It owns the rendering-independent document state (the Phase 1
/// kernel), exposes a declarative command surface (ADR 0015), and publishes
/// neutral accessibility metadata under <see cref="UiSemanticRole.RichEdit"/>
/// (ADR 0017). Layout, drawing, hit-testing, and input are added by the standard
/// implementation in a later phase; this type deliberately carries no renderer.
/// </summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: an edit through ExecuteCommand, InsertDocument, ReplaceTextRange or a delete primitive changes Document while IsReadOnly is true or IsEnabled is false
// Broiler-Human:        PENDING
public abstract class UiRichEdit : UiElement
{
    private readonly RichTextEditor _editor = new();
    private bool _isEnabled = true;
    private bool _isReadOnly;
    private bool _acceptsReturn = true;
    private string _placeholderText = string.Empty;
    private BSize _preferredSize = new(320, 160);
    private RichEditScrollPolicy _verticalScrollPolicy = RichEditScrollPolicy.Auto;
    private RichTextRange? _secondarySelection;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<RichEditDocumentChangedEventArgs>? DocumentChanged;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<RichEditSelectionChangedEventArgs>? SelectionChanged;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<RichEditCommandExecutedEventArgs>? CommandExecuted;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<RichEditSubmittedEventArgs>? Submitted;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after a new Document is assigned, Undo brings back content from the document that was loaded before it
    // Broiler-Human:        PENDING
    public RichTextDocument Document
    {
        get => _editor.Document;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            _editor.LoadDocument(value);
            _secondarySelection = null;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            DocumentChanged?.Invoke(this, new RichEditDocumentChangedEventArgs(_editor.Document));
            SelectionChanged?.Invoke(this, new RichEditSelectionChangedEventArgs(_editor.Selection));
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a selection whose paragraph index or offset lies past the document end is stored unclamped
    // Broiler-Human:        PENDING
    public RichTextRange Selection
    {
        get => _editor.Selection;
        set
        {
            ThrowIfDisposed();
            RichTextRange old = _editor.Selection;
            _editor.SetSelection(value);
            if (old != _editor.Selection)
            {
                Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
                SelectionChanged?.Invoke(this, new RichEditSelectionChangedEventArgs(_editor.Selection));
            }
        }
    }

    /// <summary>
    /// Optional non-editing highlight used by synchronized inspectors. It does
    /// not change the editor selection, caret, document, or undo history.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a range whose paragraph index or offset lies past the document end is stored unclamped
    // Broiler-Human:        PENDING
    public RichTextRange? SecondarySelection
    {
        get => _secondarySelection;
        set
        {
            ThrowIfDisposed();
            RichTextRange? resolved = value is RichTextRange range
                ? new RichTextRange(
                    _editor.Document.ClampPosition(range.Anchor),
                    _editor.Document.ClampPosition(range.Focus))
                : null;
            if (_secondarySelection == resolved)
                return;
            _secondarySelection = resolved;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after IsEnabled is set to false, ExecuteCommand with Paste or InsertText still changes Document
    // Broiler-Human:        PENDING
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetFlag(ref _isEnabled, value, UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after IsReadOnly is set to true, ExecuteCommand with InsertText or Cut still changes Document
    // Broiler-Human:        PENDING
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => SetFlag(ref _isReadOnly, value, UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>
    /// When true (default) Enter inserts a paragraph break; when false a host may
    /// route Enter to <see cref="Submit"/> instead. This flag does not affect the
    /// explicit <see cref="RichEditCommand.InsertParagraphBreak"/> command.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool AcceptsReturn
    {
        get => _acceptsReturn;
        set
        {
            ThrowIfDisposed();
            _acceptsReturn = value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning null stores null rather than an empty string, giving an empty document a null semantic name
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
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public RichEditScrollPolicy VerticalScrollPolicy
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

    /// <summary>The inline style the next typed character would take (pending style applied).</summary>
    public InlineStyle CaretInlineStyle => _editor.CaretInlineStyle;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public string GetPlainText() => _editor.Document.PlainText;

    /// <summary>Replaces the document with plain text, resetting selection and history.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: after SetPlainText, Undo brings back the document that was loaded before it
    // Broiler-Human:        PENDING
    public void SetPlainText(string? text)
    {
        ThrowIfDisposed();
        _editor.LoadPlainText(text);
        _secondarySelection = null;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        DocumentChanged?.Invoke(this, new RichEditDocumentChangedEventArgs(_editor.Document));
        SelectionChanged?.Invoke(this, new RichEditSelectionChangedEventArgs(_editor.Selection));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: Submit on a rich edit whose IsEnabled is false raises Submitted
    // Broiler-Human:        PENDING
    public void Submit()
    {
        ThrowIfDisposed();
        if (!_isEnabled)
            return;

        Submitted?.Invoke(this, new RichEditSubmittedEventArgs(GetPlainText()));
    }

    /// <summary>Queries the toolbar-facing state of a command for the current selection.</summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0015; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: GetCommandState reports Cut, Paste or InsertText enabled on a rich edit whose IsReadOnly is true
    // Broiler-Human:        PENDING
    public RichEditCommandState GetCommandState(RichEditCommand command)
    {
        ThrowIfDisposed();
        bool editable = _isEnabled && !_isReadOnly;
        bool hasSelection = !_editor.Selection.IsEmpty;
        bool clipboard = HasClipboard;
        InlineStyle inline = CurrentInlineStyle;
        ParagraphStyle paragraph = CurrentParagraphStyle;

        return command switch
        {
            RichEditCommand.Undo => RichEditCommandState.For(editable && _editor.CanUndo),
            RichEditCommand.Redo => RichEditCommandState.For(editable && _editor.CanRedo),
            RichEditCommand.Cut => RichEditCommandState.For(editable && hasSelection && clipboard),
            RichEditCommand.Copy => RichEditCommandState.For(_isEnabled && hasSelection && clipboard),
            RichEditCommand.Paste => RichEditCommandState.For(editable && clipboard),
            RichEditCommand.SelectAll => RichEditCommandState.For(_isEnabled && _editor.Document.PlainText.Length > 0),
            RichEditCommand.InsertText or RichEditCommand.InsertParagraphBreak or RichEditCommand.InsertLineBreak =>
                RichEditCommandState.For(editable),
            RichEditCommand.Bold => RichEditCommandState.For(editable, inline.Bold),
            RichEditCommand.Italic => RichEditCommandState.For(editable, inline.Italic),
            RichEditCommand.Underline => RichEditCommandState.For(editable, inline.Underline),
            RichEditCommand.Strikethrough => RichEditCommandState.For(editable, inline.Strikethrough),
            RichEditCommand.AllCaps => RichEditCommandState.For(editable, inline.Capitalization == TextCapitalization.AllCaps),
            RichEditCommand.SmallCaps => RichEditCommandState.For(editable, inline.Capitalization == TextCapitalization.SmallCaps),
            RichEditCommand.SetForeground or RichEditCommand.SetBackground or
            RichEditCommand.SetFontFamily or RichEditCommand.SetFontSize or RichEditCommand.SetFont or
            RichEditCommand.ClearFormatting =>
                RichEditCommandState.For(editable),
            RichEditCommand.AlignLeft => RichEditCommandState.For(editable, paragraph.Alignment == TextAlignment.Left),
            RichEditCommand.AlignCenter => RichEditCommandState.For(editable, paragraph.Alignment == TextAlignment.Center),
            RichEditCommand.AlignRight => RichEditCommandState.For(editable, paragraph.Alignment == TextAlignment.Right),
            RichEditCommand.AlignJustify => RichEditCommandState.For(editable, paragraph.Alignment == TextAlignment.Justify),
            RichEditCommand.BulletList => RichEditCommandState.For(editable, paragraph.ListKind == ListKind.Bullet),
            RichEditCommand.NumberedList => RichEditCommandState.For(editable, paragraph.ListKind == ListKind.Numbered),
            RichEditCommand.Indent or RichEditCommand.Outdent => RichEditCommandState.For(editable),
            _ => RichEditCommandState.Disabled,
        };
    }

    /// <summary>
    /// Executes a command. Disabled commands are no-ops that return false. On
    /// success, <see cref="DocumentChanged"/>/<see cref="SelectionChanged"/> fire
    /// first (with invalidation), then <see cref="CommandExecuted"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0015; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: ExecuteCommand runs a command that GetCommandState reports disabled and changes Document or Selection
    // Broiler-Human:        PENDING
    public bool ExecuteCommand(RichEditCommand command, object? parameter = null)
    {
        ThrowIfDisposed();
        if (!GetCommandState(command).IsEnabled)
            return false;

        RichTextDocument oldDocument = _editor.Document;
        RichTextRange oldSelection = _editor.Selection;
        bool changed = Dispatch(command, parameter);
        RaiseStateChanges(oldDocument, oldSelection);
        CommandExecuted?.Invoke(this, new RichEditCommandExecutedEventArgs(command, changed));
        return changed;
    }

    /// <summary>
    /// Inserts a whole document's rich content at the caret, replacing the current
    /// selection, as one undo unit. This is the rich-paste primitive used by the
    /// optional <c>Broiler.UI.RichEdit.Documents</c> adapter (and any host that has
    /// a <see cref="RichTextDocument"/> to insert); the core control does not itself
    /// reference any document-format codec. Fires the same change events and honours
    /// <see cref="IsEnabled"/>/<see cref="IsReadOnly"/> as the editing commands.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: InsertDocument changes Document on a rich edit whose IsReadOnly is true or IsEnabled is false
    // Broiler-Human:        PENDING
    public bool InsertDocument(RichTextDocument content)
    {
        ThrowIfDisposed();
        if (content is null)
            return false;

        return RunEditorEdit(editor => editor.InsertDocument(content));
    }

    /// <summary>Replaces an explicit source range as one undo transaction.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: ReplaceTextRange changes Document on a rich edit whose IsReadOnly is true or IsEnabled is false
    // Broiler-Human:        PENDING
    public bool ReplaceTextRange(
        RichTextRange range,
        string text,
        RichTextRange? afterSelection = null)
    {
        ThrowIfDisposed();
        return RunEditorEdit(editor => editor.ReplaceText(range, text, afterSelection));
    }

    /// <summary>Applies an exact inline delta to an explicit source range.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: ApplyInlineStyleRange changes Document on a rich edit whose IsReadOnly is true or IsEnabled is false
    // Broiler-Human:        PENDING
    public bool ApplyInlineStyleRange(
        RichTextRange range,
        InlineStyleDelta delta,
        RichTextRange? afterSelection = null)
    {
        ThrowIfDisposed();
        return RunEditorEdit(editor => editor.ApplyInlineStyle(range, delta, afterSelection));
    }

    /// <summary>Applies an exact paragraph delta to an explicit source range.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: ApplyParagraphStyleRange changes Document on a rich edit whose IsReadOnly is true or IsEnabled is false
    // Broiler-Human:        PENDING
    public bool ApplyParagraphStyleRange(
        RichTextRange range,
        ParagraphStyleDelta delta,
        RichTextRange? afterSelection = null)
    {
        ThrowIfDisposed();
        return RunEditorEdit(editor => editor.ApplyParagraphStyle(range, delta, afterSelection));
    }

    /// <summary>
    /// Deletes backward from the caret (the Backspace key): the current selection
    /// if any, otherwise the character or paragraph break before the caret. This is
    /// the keyboard editing primitive that has no toolbar command in the ADR 0015
    /// set; it shares the editor's single undo model with
    /// <see cref="ExecuteCommand"/> and raises the same change events.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: DeleteBackward removes text from a rich edit whose IsReadOnly is true
    // Broiler-Human:        PENDING
    protected bool DeleteBackward() => RunEditorEdit(static editor => editor.Backspace());

    /// <summary>
    /// Deletes forward from the caret (the Delete key): the current selection if
    /// any, otherwise the character or paragraph break after the caret.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: DeleteForward removes text from a rich edit whose IsReadOnly is true
    // Broiler-Human:        PENDING
    protected bool DeleteForward() => RunEditorEdit(static editor => editor.Delete());

    /// <summary>
    /// True while an IME composition is in progress. The abstraction has no input
    /// pipeline, so it never composes; the standard implementation overrides this
    /// so composition state reaches the semantic text projection.
    /// </summary>
    protected virtual bool IsCompositionActive => false;

    // Broiler-AI:           Origin=AI; Spec=ADR-0017; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the RichEdit node is built without text info, so assistive technology receives no caret or selection
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.RichEdit,
            GetSemanticName(),
            Bounds,
            CreateSemanticState(),
            [],
            CreateSemanticTextInfo());

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: the operation runs and changes Document on a rich edit whose IsReadOnly is true or IsEnabled is false
    // Broiler-Human:        PENDING
    private bool RunEditorEdit(Func<RichTextEditor, bool> operation)
    {
        if (!_isEnabled || _isReadOnly)
            return false;

        RichTextDocument oldDocument = _editor.Document;
        RichTextRange oldSelection = _editor.Selection;
        bool changed = operation(_editor);
        RaiseStateChanges(oldDocument, oldSelection);
        return changed;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private bool HasClipboard => Session?.Host is IUiClipboardHost;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a focus paragraph index past the last paragraph reaches the Paragraphs indexer and throws
    // Broiler-Human:        PENDING
    private ParagraphStyle CurrentParagraphStyle =>
        _editor.Document.Paragraphs[_editor.Selection.Focus.ParagraphIndex].Style;

    // Broiler-AI:           Origin=AI; Spec=ADR-0015; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a SetForeground or SetBackground command whose parameter is not a BColor changes the document
    // Broiler-Human:        PENDING
    private bool Dispatch(RichEditCommand command, object? parameter) => command switch
    {
        RichEditCommand.Undo => _editor.Undo(),
        RichEditCommand.Redo => _editor.Redo(),
        RichEditCommand.Cut => Cut(),
        RichEditCommand.Copy => Copy(),
        RichEditCommand.Paste => Paste(),
        RichEditCommand.SelectAll => SelectAllCommand(),
        RichEditCommand.InsertText => _editor.InsertText(parameter as string ?? string.Empty),
        RichEditCommand.InsertParagraphBreak => _editor.SplitParagraph(),
        RichEditCommand.InsertLineBreak => _editor.InsertLineBreak(),
        RichEditCommand.Bold => _editor.ApplyInlineStyle(InlineStyleDelta.ToggleBold(!CurrentInlineStyle.Bold)),
        RichEditCommand.Italic => _editor.ApplyInlineStyle(InlineStyleDelta.ToggleItalic(!CurrentInlineStyle.Italic)),
        RichEditCommand.Underline => _editor.ApplyInlineStyle(InlineStyleDelta.ToggleUnderline(!CurrentInlineStyle.Underline)),
        RichEditCommand.Strikethrough => _editor.ApplyInlineStyle(InlineStyleDelta.ToggleStrikethrough(!CurrentInlineStyle.Strikethrough)),
        RichEditCommand.AllCaps => _editor.ApplyInlineStyle(ToggleCapitalization(TextCapitalization.AllCaps)),
        RichEditCommand.SmallCaps => _editor.ApplyInlineStyle(ToggleCapitalization(TextCapitalization.SmallCaps)),
        RichEditCommand.SetForeground => parameter is BColor foreground && _editor.ApplyInlineStyle(InlineStyleDelta.WithForeground(foreground)),
        RichEditCommand.SetBackground => parameter is BColor background && _editor.ApplyInlineStyle(InlineStyleDelta.WithBackground(background)),
        RichEditCommand.SetFontFamily => _editor.ApplyInlineStyle(InlineStyleDelta.WithFontFamily(NormalizeFontFamily(parameter as string))),
        RichEditCommand.SetFontSize => TryGetFontSize(parameter, out float? size) && _editor.ApplyInlineStyle(InlineStyleDelta.WithFontSize(size)),
        RichEditCommand.SetFont => parameter is BFontStyle font && _editor.ApplyInlineStyle(FontStyleDelta(font)),
        RichEditCommand.ClearFormatting => _editor.ClearFormatting(),
        RichEditCommand.AlignLeft => _editor.SetAlignment(TextAlignment.Left),
        RichEditCommand.AlignCenter => _editor.SetAlignment(TextAlignment.Center),
        RichEditCommand.AlignRight => _editor.SetAlignment(TextAlignment.Right),
        RichEditCommand.AlignJustify => _editor.SetAlignment(TextAlignment.Justify),
        RichEditCommand.BulletList => ToggleList(ListKind.Bullet),
        RichEditCommand.NumberedList => ToggleList(ListKind.Numbered),
        RichEditCommand.Indent => _editor.Indent(),
        RichEditCommand.Outdent => _editor.Outdent(),
        _ => false,
    };

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private bool SelectAllCommand()
    {
        _editor.SelectAll();
        return true;
    }

    /// <summary>Deletes the current selection through the shared document undo model.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: DeleteCurrentSelection with an empty selection deletes the character after the caret
    // Broiler-Human:        PENDING
    protected bool DeleteCurrentSelection() => RunEditorEdit(static editor => editor.Delete());

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: with a non-empty selection inside one paragraph, the reported style is read from the character after the selection instead of the last selected one
    // Broiler-Human:        PENDING
    private InlineStyle CurrentInlineStyle => _editor.Selection.IsEmpty
        ? _editor.CaretInlineStyle
        : _editor.Document.InlineStyleAt(_editor.Selection.End);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: BulletList on a paragraph that is already a bullet list leaves it a bullet list instead of clearing the list kind
    // Broiler-Human:        PENDING
    private bool ToggleList(ListKind kind)
    {
        ListKind target = CurrentParagraphStyle.ListKind == kind ? ListKind.None : kind;
        return _editor.SetListKind(target);
    }

    /// <summary>
    /// Turns <paramref name="kind"/> on, or back off when it is already the
    /// current capitalization. The two kinds are exclusive, so switching between
    /// them replaces rather than combines.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: AllCaps on text that is already SmallCaps turns capitalization off instead of switching to AllCaps
    // Broiler-Human:        PENDING
    private InlineStyleDelta ToggleCapitalization(TextCapitalization kind) =>
        InlineStyleDelta.WithCapitalization(
            CurrentInlineStyle.Capitalization == kind ? TextCapitalization.None : kind);

    /// <summary>
    /// The model change a chosen font makes.
    /// </summary>
    /// <remarks>
    /// The other half of the conversion the rich edit does when it draws: a font
    /// picked in this control is in the control's device-independent pixels, and
    /// the model records type in points. Writing the pixel number into the model
    /// would make a document say a size nobody chose, and it would say it
    /// permanently — the file keeps it.
    /// </remarks>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a BFontStyle whose Size is NaN writes a NaN font size into the document model
    // Broiler-Human:        PENDING
    private static InlineStyleDelta FontStyleDelta(BFontStyle font) => new()
    {
        SetFontFamily = true,
        FontFamily = NormalizeFontFamily(font.FamilyName),
        SetFontSize = true,
        FontSize = NormalizeFontSize(BFontStyle.PixelsToPoints(font.Size)),
        Bold = font.Weight >= BFontWeight.Bold,
        Italic = font.Slant != BFontSlant.Normal,
    };

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a whitespace-only family name is applied as a font family instead of clearing it
    // Broiler-Human:        PENDING
    private static string? NormalizeFontFamily(string? family)
    {
        family = family?.Trim();
        return string.IsNullOrWhiteSpace(family) ? null : family;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a string parameter of NaN or Infinity, or a zero or negative number, is applied as a font size instead of being refused
    // Broiler-Human:        PENDING
    private static bool TryGetFontSize(object? parameter, out float? size)
    {
        size = null;
        if (parameter is null)
            return true;

        double value = parameter switch
        {
            float single => single,
            double dbl => dbl,
            int integer => integer,
            decimal dec => (double)dec,
            string text when double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => double.NaN,
        };

        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return false;

        size = NormalizeFontSize(value);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a finite size above 512 or below 1 point is returned without being clamped into that range
    // Broiler-Human:        PENDING
    private static float NormalizeFontSize(double size) =>
        (float)Math.Clamp(size, 1.0, 512.0);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: for a selection spanning paragraphs, the text put on the clipboard differs from the selected range of PlainText
    // Broiler-Human:        PENDING
    private bool Copy()
    {
        if (_editor.Selection.IsEmpty || Session?.Host is not IUiClipboardHost clipboard)
            return false;

        clipboard.SetText(GetSelectedPlainText());
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: Cut on a rich edit whose IsReadOnly is true copies or deletes the selection
    // Broiler-Human:        PENDING
    private bool Cut() => !_isReadOnly && Copy() && _editor.Delete();

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: Paste inserts clipboard text into a rich edit whose IsReadOnly is true
    // Broiler-Human:        PENDING
    private bool Paste()
    {
        if (_isReadOnly || Session?.Host is not IUiClipboardHost clipboard || !clipboard.TryGetText(out string text))
            return false;

        return _editor.InsertText(text);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a selection ending in a later paragraph yields a substring shifted by the paragraph separators before it
    // Broiler-Human:        PENDING
    private string GetSelectedPlainText()
    {
        RichTextRange selection = _editor.Selection;
        if (selection.IsEmpty)
            return string.Empty;

        string plain = _editor.Document.PlainText;
        int start = FlatIndex(selection.Start);
        int end = FlatIndex(selection.End);
        start = Math.Clamp(start, 0, plain.Length);
        end = Math.Clamp(end, start, plain.Length);
        return plain.Substring(start, end - start);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a position in paragraph n maps to an index that does not count one separator for each of the n preceding paragraphs, so it disagrees with PlainText
    // Broiler-Human:        PENDING
    private int FlatIndex(RichTextPosition position)
    {
        RichTextDocument document = _editor.Document;
        position = document.ClampPosition(position);
        int flat = 0;
        for (int i = 0; i < position.ParagraphIndex; i++)
            flat += document.Paragraphs[i].Length + 1; // +1 for the paragraph separator
        return flat + position.Offset;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an edit that replaces Document raises no DocumentChanged event or leaves Measure uninvalidated
    // Broiler-Human:        PENDING
    private void RaiseStateChanges(RichTextDocument oldDocument, RichTextRange oldSelection)
    {
        bool documentChanged = !ReferenceEquals(oldDocument, _editor.Document);
        bool selectionChanged = oldSelection != _editor.Selection;

        if (documentChanged)
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        else if (selectionChanged)
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);

        if (documentChanged)
            DocumentChanged?.Invoke(this, new RichEditDocumentChangedEventArgs(_editor.Document));
        if (selectionChanged)
            SelectionChanged?.Invoke(this, new RichEditSelectionChangedEventArgs(_editor.Selection));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private void SetFlag(ref bool field, bool value, UiInvalidationKind invalidation)
    {
        ThrowIfDisposed();
        if (field == value)
            return;

        field = value;
        Invalidate(invalidation);
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0017; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an empty document with a PlaceholderText produces an empty semantic name instead of the placeholder
    // Broiler-Human:        PENDING
    private string GetSemanticName()
    {
        string plain = _editor.Document.PlainText;
        return plain.Length > 0 ? plain : _placeholderText;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0017; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a rich edit whose IsReadOnly is true reports no ReadOnly semantic state
    // Broiler-Human:        PENDING
    private UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (_isEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (_isReadOnly)
            state |= UiSemanticState.ReadOnly;
        if (!_editor.Selection.IsEmpty)
            state |= UiSemanticState.Selected;
        return state;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0017; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: the caret or selection start published for a position in a later paragraph differs from that position's index in the published text value
    // Broiler-Human:        PENDING
    private UiSemanticTextInfo CreateSemanticTextInfo()
    {
        RichTextRange selection = _editor.Selection;
        int caret = FlatIndex(selection.Focus);
        int start = FlatIndex(selection.Start);
        int length = FlatIndex(selection.End) - start;
        return new UiSemanticTextInfo(
            _editor.Document.PlainText,
            caret,
            start,
            length,
            _isEnabled && !_isReadOnly,
            IsPassword: false,
            IsCompositionActive);
    }
}
