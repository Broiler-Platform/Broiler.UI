using System;

namespace Broiler.UI.CodeEditor;

/// <summary>
/// The control's view of one document version.
///
/// This is an interface rather than a class because the control must work over
/// a product workspace's buffer without knowing that type exists, and over a
/// trivial in-memory buffer in tests. Every member takes a range: the control
/// renders a visible window and must never be able to pull a multi-megabyte
/// document into a string.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: the same snapshot returns different text for one range before and after its document accepts a later edit
// Broiler-Human:        PENDING
public interface ICodeTextSnapshot
{
    /// <summary>
    /// Monotonic per-document version. Used for staleness checks and for edit
    /// intents; identity comparison of the snapshot itself is what decides
    /// whether an analysis result may paint.
    /// </summary>
    int Version { get; }

    int Length { get; }

    /// <summary>At least one. A trailing terminator produces a final empty line.</summary>
    int LineCount { get; }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: for a document ending in a line terminator, the start of the final line is not equal to Length
    // Broiler-Human:        PENDING
    int GetLineStart(int line);

    /// <summary>Length excluding the line terminator.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a CRLF-terminated line reports a length that includes its carriage return
    // Broiler-Human:        PENDING
    int GetLineLength(int line);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a position equal to Length is refused or mapped to a line other than LineCount - 1, though CodeDiagnosticSet.Create passes exactly that
    // Broiler-Human:        PENDING
    int GetLineFromPosition(int position);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a short range is answered by materializing the whole document into a string, allocating in proportion to Length rather than to the requested length
    // Broiler-Human:        PENDING
    string GetText(int start, int length);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: CopyTo writes different characters than GetText returns for the same start and length
    // Broiler-Human:        PENDING
    void CopyTo(int start, int length, Span<char> destination);

    /// <summary>
    /// The next caret stop, respecting surrogate pairs and combining marks. A
    /// caret that advances by one char splits an emoji.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: from a position before an emoji surrogate pair or a base letter with combining marks, the next caret stop lands inside the pair or before a combining mark
    // Broiler-Human:        PENDING
    int GetNextCaretPosition(int position);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: from a position just after an emoji surrogate pair, the previous caret stop lands between its high and low surrogate
    // Broiler-Human:        PENDING
    int GetPreviousCaretPosition(int position);
}

/// <summary>
/// A versioned edit intent. The control composes these; it never applies them.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public readonly record struct CodeEditIntent(
    int BaseVersion,
    int Start,
    int Length,
    string Text,
    string Name);

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public enum CodeEditRejection
{
    None = 0,

    /// <summary>The intent named a version the document has already replaced.</summary>
    StaleVersion,

    OutOfRange,

    ReadOnly,
}

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public readonly record struct CodeEditOutcome(
    bool Accepted,
    ICodeTextSnapshot Snapshot,
    CodeEditRejection Reason);

/// <summary>
/// The document the control edits. Transactions, versions, and undo history
/// belong to the implementation — the control owns caret, selection, scroll and
/// composition state, submits intents, and renders whatever snapshot comes
/// back.
///
/// An intent naming a superseded version is rejected, not rebased. A silent
/// rebase turns a race into a wrong edit at a plausible-looking position.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
// Broiler-Falsified-If: an intent whose Start plus Length runs past the snapshot Length is accepted instead of being rejected with OutOfRange
// Broiler-Human:        PENDING
public interface ICodeDocument
{
    ICodeTextSnapshot Snapshot { get; }

    bool IsReadOnly { get; }

    /// <summary>The terminator to insert for a new line.</summary>
    string LineEnding { get; }

    bool CanUndo { get; }

    bool CanRedo { get; }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an accepted Submit, Undo or Redo replaces Snapshot without raising SnapshotChanged, leaving the control painting the old snapshot
    // Broiler-Human:        PENDING
    event Action<ICodeTextSnapshot>? SnapshotChanged;

    // Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: an intent whose BaseVersion is older than the current Snapshot.Version is applied at its original offsets instead of returning StaleVersion
    // Broiler-Human:        PENDING
    CodeEditOutcome Submit(CodeEditIntent intent);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: Undo after one accepted edit returns a snapshot whose text differs from the text before that edit
    // Broiler-Human:        PENDING
    bool Undo();

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: Redo after an Undo returns a snapshot whose text differs from the one that Undo reverted
    // Broiler-Human:        PENDING
    bool Redo();

    /// <summary>
    /// Ends the current undo group. The control calls this when the caret moves
    /// or focus changes; the document cannot see those events, and without them
    /// an editing session collapses into a single undo step.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: two edits separated by a BreakUndoGroup call are both reverted by a single Undo
    // Broiler-Human:        PENDING
    void BreakUndoGroup();
}
