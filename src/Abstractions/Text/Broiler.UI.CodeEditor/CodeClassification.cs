using System;

namespace Broiler.UI.CodeEditor;

/// <summary>
/// The control-facing classification vocabulary. It is deliberately
/// language-neutral: the editor maps these kinds to theme tokens and must never
/// learn C# token kinds, or a second language would require changing the
/// control.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public enum CodeClassificationKind : byte
{
    /// <summary>Default foreground. Never emitted as a span.</summary>
    None = 0,
    Comment,
    DocumentationComment,
    Keyword,
    ControlKeyword,
    PreprocessorKeyword,
    PreprocessorText,
    StringLiteral,
    EscapeSequence,
    CharacterLiteral,
    NumericLiteral,
    Operator,
    Punctuation,
}

/// <summary>
/// One classified range. <paramref name="Start"/> is relative to the start of
/// its line, not to the document: line-relative offsets survive an edit on
/// another line unchanged, so unaffected lines are reused by reference instead
/// of being rewritten with shifted positions.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public readonly record struct CodeClassificationSpan(
    int Start,
    int Length,
    CodeClassificationKind Kind);

/// <summary>
/// Classifications for one exact snapshot.
///
/// This is an abstract class rather than an interface so
/// <see cref="GetLineSpans"/> can return a span over the producer's own storage
/// — the control reads spans on every paint of every visible line, and an
/// allocating accessor there would undo the point of virtualizing.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: GetLineSpans returns offsets relative to the document rather than to the start of the requested line
// Broiler-Human:        PENDING
public abstract class CodeClassificationResult
{
    /// <summary>
    /// The exact snapshot these classifications describe. The control compares
    /// this by reference before painting; a version number alone is not enough,
    /// because it is not comparable across documents.
    /// </summary>
    public abstract ICodeTextSnapshot Snapshot { get; }

    public abstract int LineCount { get; }

    /// <summary>
    /// Spans for one line, with line-relative offsets, in ascending order and
    /// non-overlapping. Returning an empty span means "paint this line in the
    /// default foreground", which is also what identifiers get.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the spans returned for one line overlap each other or are not in ascending Start order
    // Broiler-Human:        PENDING
    public abstract ReadOnlySpan<CodeClassificationSpan> GetLineSpans(int line);

    /// <summary>An empty result, used before the first classification lands.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the empty result reports a Snapshot that is not the same reference as the snapshot passed in, so the control never accepts it
    // Broiler-Human:        PENDING
    public static CodeClassificationResult Empty(ICodeTextSnapshot snapshot) =>
        new EmptyResult(snapshot);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: LineCount differs from the LineCount of the snapshot the empty result was created for
    // Broiler-Human:        PENDING
    private sealed class EmptyResult(ICodeTextSnapshot snapshot) : CodeClassificationResult
    {
        public override ICodeTextSnapshot Snapshot { get; } = snapshot;

        public override int LineCount => Snapshot.LineCount;

        public override ReadOnlySpan<CodeClassificationSpan> GetLineSpans(int line) => default;
    }
}

/// <summary>
/// Produces classifications for a snapshot. Implementations run off the UI
/// thread and must observe cancellation: a superseded run keeps executing until
/// it next checks its token, so two runs overlap routinely and an implementation
/// holding cross-call state corrupts both.
/// </summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: two overlapping Classify runs on successive snapshots return spans computed from each other's text because an implementation keeps state across calls
// Broiler-Human:        PENDING
public interface ICodeClassifier
{
    // Broiler-AI:           Origin=AI; IP=Low; Security=High; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a run whose token is cancelled partway keeps lexing to the end of a multi-megabyte snapshot instead of stopping at its next token check
    // Broiler-Human:        PENDING
    CodeClassificationResult Classify(
        ICodeTextSnapshot snapshot,
        CodeClassificationResult? previous,
        CodeTextChange? change,
        System.Threading.CancellationToken cancellationToken);
}

/// <summary>What changed between two snapshots, so a classifier can reuse work.</summary>
// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: NewEnd is computed from OldLength or OldEnd from NewLength, so a replacement reports the ends of the wrong range
// Broiler-Human:        PENDING
public readonly record struct CodeTextChange(int Start, int OldLength, int NewLength)
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an insertion of three characters at Start 10 reports a NewEnd other than 13
    // Broiler-Human:        PENDING
    public int NewEnd => Start + NewLength;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a deletion of three characters at Start 10 reports an OldEnd other than 13
    // Broiler-Human:        PENDING
    public int OldEnd => Start + OldLength;
}
