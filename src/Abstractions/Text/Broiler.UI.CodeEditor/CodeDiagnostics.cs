using System;
using System.Collections.Generic;

namespace Broiler.UI.CodeEditor;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: Error is not the largest value, so the greater-than comparison in GetLineSeverity lets a Warning outrank an Error
// Broiler-Human:        PENDING
public enum CodeDiagnosticSeverity
{
    Hidden = 0,
    Information,
    Warning,
    Error,
}

/// <summary>
/// One diagnostic as the control sees it: a span, a severity, and an accessible
/// description.
///
/// The control never parses <paramref name="Description"/> and never derives
/// behavior from it. Identity belongs to the producer's diagnostic model, where
/// it is the rule code, document, and span — never the message, which is
/// localized.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: a one-character diagnostic reports End equal to Start instead of the position after its character
// Broiler-Human:        PENDING
public sealed record CodeDiagnosticAdornment(
    int Start,
    int Length,
    CodeDiagnosticSeverity Severity,
    string Description,
    string? Code = null)
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a one-character diagnostic reports End equal to Start instead of the position after its character
    // Broiler-Human:        PENDING
    public int End => Start + Length;
}

/// <summary>
/// Diagnostics for one exact snapshot, indexed by line so the renderer can ask
/// for the visible range without scanning the whole set.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0021; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
// Broiler-Falsified-If: GetLineSeverity reports a severity for a line that no diagnostic covers, because Create indexed a diagnostic under the line holding its exclusive end
// Broiler-Human:        PENDING
public sealed class CodeDiagnosticSet
{
    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static readonly CodeDiagnosticAdornment[] None = [];

    private readonly Dictionary<int, CodeDiagnosticAdornment[]> _byLine;

    private CodeDiagnosticSet(
        ICodeTextSnapshot snapshot,
        Dictionary<int, CodeDiagnosticAdornment[]> byLine,
        int count)
    {
        Snapshot = snapshot;
        _byLine = byLine;
        Count = count;
    }

    public ICodeTextSnapshot Snapshot { get; }

    public int Count { get; }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the empty set reports a Snapshot that is not the same reference as the snapshot passed in, so the control never accepts it
    // Broiler-Human:        PENDING
    public static CodeDiagnosticSet Empty(ICodeTextSnapshot snapshot) =>
        new(snapshot, [], 0);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a diagnostic whose exclusive end falls exactly on the start of the next line is also indexed under that next line, which then shows a gutter marker and a one-column squiggle
    // Broiler-Human:        PENDING
    public static CodeDiagnosticSet Create(
        ICodeTextSnapshot snapshot,
        IEnumerable<CodeDiagnosticAdornment> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var grouped = new Dictionary<int, List<CodeDiagnosticAdornment>>();
        int count = 0;
        foreach (CodeDiagnosticAdornment diagnostic in diagnostics)
        {
            count++;

            // A diagnostic can span lines; it is indexed under every line it
            // touches so a visible-range query finds it even when its start is
            // scrolled off the top.
            int first = snapshot.GetLineFromPosition(Math.Clamp(diagnostic.Start, 0, snapshot.Length));
            int last = snapshot.GetLineFromPosition(Math.Clamp(diagnostic.End, 0, snapshot.Length));
            for (int line = first; line <= last; line++)
            {
                if (!grouped.TryGetValue(line, out List<CodeDiagnosticAdornment>? bucket))
                    grouped[line] = bucket = [];
                bucket.Add(diagnostic);
            }
        }

        var byLine = new Dictionary<int, CodeDiagnosticAdornment[]>(grouped.Count);
        foreach ((int line, List<CodeDiagnosticAdornment> bucket) in grouped)
        {
            bucket.Sort(static (left, right) => left.Start.CompareTo(right.Start));
            byLine[line] = [.. bucket];
        }

        return new CodeDiagnosticSet(snapshot, byLine, count);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a line with no indexed diagnostics returns a non-empty span
    // Broiler-Human:        PENDING
    public ReadOnlySpan<CodeDiagnosticAdornment> GetLineDiagnostics(int line) =>
        _byLine.TryGetValue(line, out CodeDiagnosticAdornment[]? bucket) ? bucket : None;

    /// <summary>The most severe diagnostic on a line, for the gutter marker.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: a line holding an Error followed by a Warning reports Warning as its most severe diagnostic
    // Broiler-Human:        PENDING
    public CodeDiagnosticSeverity GetLineSeverity(int line)
    {
        CodeDiagnosticSeverity worst = CodeDiagnosticSeverity.Hidden;
        foreach (CodeDiagnosticAdornment diagnostic in GetLineDiagnostics(line))
        {
            if (diagnostic.Severity > worst)
                worst = diagnostic.Severity;
        }

        return worst;
    }
}
