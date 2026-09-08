namespace Broiler.UI.TreeView;

/// <summary>
/// Where a row draws its <see cref="TreeNodePresentation.SecondaryLabel"/>.
/// </summary>
/// <remarks>
/// A file tree's secondary label is a word or two — a status, a count — and
/// belongs beside the name it qualifies. A tree used to report structured
/// findings carries sentences there instead, and beside the name they run off
/// the end of the pane: the reader gets the subject and loses the answer.
///
/// Which of the two a tree is cannot be decided here, so it is the host's to
/// say. <see cref="Inline"/> is the default because it is what every tree did
/// before this existed.
/// </remarks>
public enum TreeSecondaryLabelPlacement
{
    /// <summary>After the label, on the same line.</summary>
    Inline = 0,

    /// <summary>
    /// On its own line under the label, which makes every row in the tree two
    /// lines tall — rows are a uniform height, and a tree whose rows were not
    /// could not answer a hit test by division.
    /// </summary>
    BelowLabel,
}
