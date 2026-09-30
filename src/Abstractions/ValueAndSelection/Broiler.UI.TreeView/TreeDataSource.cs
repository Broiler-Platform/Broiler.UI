using System;

namespace Broiler.UI.TreeView;

/// <summary>
/// A node's identity, supplied by the data source and stable across a refresh.
///
/// Expansion, selection, and scroll are keyed by this rather than by index or by
/// object reference. A Solution Explorer rebuilds its node objects whenever the
/// workspace changes; keying on references would collapse the tree every time
/// the user saved a file.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=None; Security=Low; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: two ids whose Value strings differ only in letter case compare equal, so expansion or selection of one applies to the other
// Broiler-Human:        PENDING
public readonly record struct TreeNodeId(string Value)
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public static TreeNodeId None => new(string.Empty);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an id whose Value is null, as in default(TreeNodeId), reports IsNone false, so Expand and ActivateNode accept it
    // Broiler-Human:        PENDING
    public bool IsNone => string.IsNullOrEmpty(Value);

    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public override string ToString() => Value;
}

/// <summary>Severity or dirty state shown as a decoration, never by colour alone.</summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public enum TreeNodeDecoration
{
    None = 0,
    Dirty,
    Information,
    Warning,
    Error,
}

/// <summary>What the view needs to draw one row.</summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=None; Security=None; Resources=1; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed record TreeNodePresentation(
    TreeNodeId Id,
    string Label,
    string? SecondaryLabel = null,
    string? IconKey = null,
    TreeNodeDecoration Decoration = TreeNodeDecoration.None);

/// <summary>
/// The tree's content, supplied lazily.
///
/// Every member is designed so a large tree never has to be materialized:
/// <see cref="GetChildCount"/> answers without enumerating, and
/// <see cref="CanExpand"/> answers without expanding. A Solution Explorer over a
/// directory with fifty thousand files must be able to say "this folder can be
/// opened" without listing it, and a contract that required a materialized tree
/// would make that impossible.
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
// Broiler-Falsified-If: a node's children include its own id or an expanded ancestor's id, so the tree view's recursive row walk never terminates
// Broiler-Human:        PENDING
public interface ITreeDataSource
{
    /// <summary>The root, which the view does not display.</summary>
    TreeNodeId Root { get; }

    /// <summary>Children of a node, without enumerating them.</summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=None; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: GetChildCount returns more children than GetChild answers indices for, so the tree view's row walk throws at the first missing index
    // Broiler-Human:        PENDING
    int GetChildCount(TreeNodeId node);

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: GetChild returns a different id for the same node and index with no DataChanged in between, so expansion and selection keyed by id are lost
    // Broiler-Human:        PENDING
    TreeNodeId GetChild(TreeNodeId node, int index);

    /// <summary>
    /// Whether the node shows an expander. Answered without expanding, so a
    /// folder can offer one before anything has listed it.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=None; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Falsified-If: CanExpand lists a folder's contents to answer, so a collapsed folder of fifty thousand files is enumerated just to draw its expander
    // Broiler-Human:        PENDING
    bool CanExpand(TreeNodeId node);

    // Broiler-AI:           Origin=AI; Spec=ADR-0023; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: GetPresentation returns a null Label, so the tree view's type-ahead throws a NullReferenceException on that row
    // Broiler-Human:        PENDING
    TreeNodePresentation GetPresentation(TreeNodeId node);
}

/// <summary>Raised when the data source's content changed underneath the view.</summary>
// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class TreeDataChangedEventArgs(TreeNodeId node) : EventArgs
{
    /// <summary>The subtree that changed, or None for the whole tree.</summary>
    public TreeNodeId Node { get; } = node;
}

/// <summary>A data source that can tell the view when it has changed.</summary>
// Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: the source's children or presentations change and DataChanged is not raised, so the view keeps rows for nodes that no longer exist
// Broiler-Human:        PENDING
public interface IObservableTreeDataSource : ITreeDataSource
{
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the source's children or presentations change and DataChanged is not raised, so the view keeps rows for nodes that no longer exist
    // Broiler-Human:        PENDING
    event EventHandler<TreeDataChangedEventArgs>? DataChanged;
}
