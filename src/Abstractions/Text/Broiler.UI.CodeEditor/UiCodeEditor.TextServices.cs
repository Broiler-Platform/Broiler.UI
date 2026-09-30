using System;

namespace Broiler.UI.CodeEditor;

/// <summary>
/// The editor's two bounded text surfaces: the platform text-service contract
/// used by IMEs, and the virtualized accessibility contract.
///
/// Neither can return the whole document, and both clamp rather than throw. An
/// IME that asks for more context than exists still composes correctly; one
/// that gets an exception does not.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
// Broiler-Falsified-If: a text-service or accessibility read hands back more than MaxBoundedReadLength characters in one call
// Broiler-Human:        PENDING
public abstract partial class UiCodeEditor
{
    /// <summary>
    /// How much text either contract will hand over in one call. An IME needs
    /// tens of characters and an accessibility client reads a screenful; a
    /// caller wanting more asks again with a new start.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a single GetTextEditorRange or GetTextRange call returns more characters than this constant
    // Broiler-Human:        PENDING
    public const int MaxBoundedReadLength = 8192;

    // ----- IUiTextEditor: the platform text-service surface. -----

    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: with no active composition the metrics report a composing start other than -1
    // Broiler-Human:        PENDING
    public UiTextEditorMetrics GetTextEditorMetrics()
    {
        ICodeTextSnapshot snapshot = Snapshot;
        return new UiTextEditorMetrics(
            snapshot.Length,
            Selection.Start,
            Selection.End,
            HasComposition ? CompositionStart : -1,
            HasComposition ? CompositionEnd : -1);
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a request with a negative start or a start past the end of the document throws instead of returning clamped text
    // Broiler-Human:        PENDING
    public string GetTextEditorRange(int start, int maxLength)
    {
        ICodeTextSnapshot snapshot = Snapshot;
        (int clampedStart, int length) = UiTextEditorRange.Clamp(
            snapshot.Length, start, Math.Min(maxLength, MaxBoundedReadLength));
        return length == 0 ? string.Empty : snapshot.GetText(clampedStart, length);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: with the caret after the first character, a request to delete int.MaxValue characters after it deletes nothing instead of everything to the end of the document
    // Broiler-Human:        PENDING
    public bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        ICodeTextSnapshot snapshot = Snapshot;
        int start = Math.Max(0, Selection.Start - Math.Max(0, beforeLength));
        int end = Math.Min(snapshot.Length, Selection.End + Math.Max(0, afterLength));
        return end > start && Replace(start, end - start, string.Empty, "delete-surrounding");
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a disabled editor accepts a selection from a text service and reports true
    // Broiler-Human:        PENDING
    public bool SetEditorSelection(int start, int end)
    {
        if (!IsEnabled)
            return false;
        Selection = new CodeSelection(start, end);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a composing region that ends past the end of the document is stored unclamped and reported by GetTextEditorMetrics
    // Broiler-Human:        PENDING
    public bool SetComposingRegion(int start, int end)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        int length = Snapshot.Length;
        int clampedStart = Math.Clamp(start, 0, length);
        int clampedEnd = Math.Clamp(end, clampedStart, length);
        SetComposition(clampedStart, clampedEnd);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a Next, Previous or Search action inserts a line into the document
    // Broiler-Human:        PENDING
    public bool PerformEditorAction(UiTextEditorAction action)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        // A source editor has no "done" or "next field"; the only action with a
        // meaning here is inserting a line.
        return action switch
        {
            UiTextEditorAction.Done or UiTextEditorAction.Go or UiTextEditorAction.Send => InsertNewLine(),
            _ => false,
        };
    }

    // ----- IUiVirtualizedTextProvider: the accessibility surface. -----

    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a disabled or read-only editor reports IsEditable as true
    // Broiler-Human:        PENDING
    public UiTextDocumentMetrics GetTextMetrics()
    {
        ICodeTextSnapshot snapshot = Snapshot;
        return new UiTextDocumentMetrics(
            snapshot.Version,
            snapshot.Length,
            snapshot.LineCount,
            Selection.Start,
            Selection.End,
            HasComposition ? CompositionStart : -1,
            HasComposition ? CompositionEnd : -1,
            !IsReadOnly && IsEnabled);
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a request carrying a non-negative version older than the snapshot's returns document text instead of the Stale result
    // Broiler-Human:        PENDING
    public UiTextRangeResult GetTextRange(int start, int maxLength, int expectedVersion)
    {
        ICodeTextSnapshot snapshot = Snapshot;
        if (!IsCurrent(snapshot, expectedVersion))
            return UiTextRangeResult.Stale;

        (int clampedStart, int length) = UiTextEditorRange.Clamp(
            snapshot.Length, start, Math.Min(maxLength, MaxBoundedReadLength));
        return new UiTextRangeResult(
            true, clampedStart, length == 0 ? string.Empty : snapshot.GetText(clampedStart, length));
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a line index equal to LineCount returns a valid line info instead of the Stale result
    // Broiler-Human:        PENDING
    public UiTextLineInfo GetLineInfo(int line, int expectedVersion)
    {
        ICodeTextSnapshot snapshot = Snapshot;
        if (!IsCurrent(snapshot, expectedVersion))
            return UiTextLineInfo.Stale;
        if (line < 0 || line >= snapshot.LineCount)
            return UiTextLineInfo.Stale;

        return new UiTextLineInfo(true, line, snapshot.GetLineStart(line), snapshot.GetLineLength(line));
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a selection request carrying a stale non-negative version moves the selection
    // Broiler-Human:        PENDING
    public bool TrySetSelection(int start, int end, int expectedVersion)
    {
        if (!IsCurrent(Snapshot, expectedVersion) || !IsEnabled)
            return false;
        Selection = new CodeSelection(start, end);
        return true;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an accessibility edit is applied while IsEnabled is false, although GetTextMetrics reports the editor as not editable
    // Broiler-Human:        PENDING
    public bool TryReplaceRange(int start, int length, string text, int expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IsCurrent(Snapshot, expectedVersion))
            return false;
        return Replace(start, length, text, "accessibility-edit");
    }

    /// <summary>
    /// A negative expected version means "whatever is current" — the first call
    /// a client makes has nothing to compare against. Any other value must match
    /// exactly, so a client holding a range across an edit is refused rather
    /// than served text from a document that has moved.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0022; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a non-negative expected version one below the snapshot's version is treated as current
    // Broiler-Human:        PENDING
    private static bool IsCurrent(ICodeTextSnapshot snapshot, int expectedVersion) =>
        expectedVersion < 0 || expectedVersion == snapshot.Version;
}
