using System;

namespace Broiler.UI.CodeEditor;

/// <summary>An anchor/focus pair over the document. Focus is where the caret is.</summary>
// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: a selection whose Focus precedes its Anchor reports a negative Length
// Broiler-Human:        PENDING
public readonly record struct CodeSelection(int Anchor, int Focus)
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a selection whose Focus precedes its Anchor reports Start equal to Anchor
    // Broiler-Human:        PENDING
    public int Start => Math.Min(Anchor, Focus);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a selection whose Focus precedes its Anchor reports End equal to Focus
    // Broiler-Human:        PENDING
    public int End => Math.Max(Anchor, Focus);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a selection whose Focus precedes its Anchor reports a negative Length
    // Broiler-Human:        PENDING
    public int Length => End - Start;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a selection with equal Anchor and Focus reports IsEmpty false
    // Broiler-Human:        PENDING
    public bool IsEmpty => Anchor == Focus;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: the selection created for a caret position has an Anchor different from its Focus
    // Broiler-Human:        PENDING
    public static CodeSelection Caret(int position) => new(position, position);
}

/// <summary>The visible window, in lines and pixels, that the renderer paints.</summary>
// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: a viewport with VisibleLineCount 0 reports a LastVisibleLine before FirstVisibleLine
// Broiler-Human:        PENDING
public readonly record struct CodeViewport(int FirstVisibleLine, int VisibleLineCount, double HorizontalOffset)
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a viewport with VisibleLineCount 0 reports a LastVisibleLine before FirstVisibleLine
    // Broiler-Human:        PENDING
    public int LastVisibleLine => FirstVisibleLine + Math.Max(0, VisibleLineCount - 1);
}

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public enum CodeCaretMovement
{
    Left,
    Right,
    Up,
    Down,
    LineStart,
    LineEnd,
    DocumentStart,
    DocumentEnd,
    PageUp,
    PageDown,
    WordLeft,
    WordRight,
}

/// <summary>
/// How the editor reports what it can currently do. A host without the complete
/// semantic stack shows classification-only mode rather than pretending the
/// absence of diagnostics means the code is correct.
/// </summary>
// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public enum CodeAnalysisMode
{
    /// <summary>Syntax colouring only; no semantic diagnostics are available.</summary>
    ClassificationOnly = 0,

    /// <summary>Diagnostics come from the last authoritative build.</summary>
    BuildDiagnostics,

    /// <summary>A live semantic service is attached.</summary>
    LiveSemantic,
}

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class CodeSelectionChangedEventArgs(CodeSelection selection) : EventArgs
{
    public CodeSelection Selection { get; } = selection;
}

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class CodeSnapshotChangedEventArgs(ICodeTextSnapshot snapshot) : EventArgs
{
    public ICodeTextSnapshot Snapshot { get; } = snapshot;
}

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class CodeViewportChangedEventArgs(CodeViewport viewport) : EventArgs
{
    public CodeViewport Viewport { get; } = viewport;
}

/// <summary>
/// Raised when an intent is refused, so a host can surface why rather than
/// leaving the user with a keystroke that silently did nothing.
/// </summary>
// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class CodeEditRejectedEventArgs(CodeEditIntent intent, CodeEditRejection reason) : EventArgs
{
    public CodeEditIntent Intent { get; } = intent;

    public CodeEditRejection Reason { get; } = reason;
}

/// <summary>Tab width and whether Tab inserts spaces.</summary>
// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=2; Fingerprint=TBF
// Broiler-Falsified-If: a TabSize of zero or less with InsertSpaces yields an empty indent unit, so Tab inserts nothing
// Broiler-Human:        PENDING
public readonly record struct CodeIndentPolicy(int TabSize, bool InsertSpaces)
{
    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public static CodeIndentPolicy Default => new(4, true);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: a TabSize of zero or less with InsertSpaces yields an empty indent unit, so Tab inserts nothing
    // Broiler-Human:        PENDING
    public string GetIndentUnit() => InsertSpaces ? new string(' ', Math.Max(1, TabSize)) : "\t";
}
