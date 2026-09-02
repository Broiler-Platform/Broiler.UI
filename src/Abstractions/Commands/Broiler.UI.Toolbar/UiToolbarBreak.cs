namespace Broiler.UI.Toolbar;

/// <summary>
/// What a toolbar puts in front of an item to start a new group.
/// </summary>
/// <remarks>
/// A bar of otherwise evenly spaced controls reads as one long undifferentiated run, and a drawn
/// rule between every group reads as a form. Most bars want the middle option: enough extra space
/// that the eye finds the groups, and no extra ink.
/// </remarks>
public enum UiToolbarBreak
{
    /// <summary>Nothing. The item follows the previous one at the bar's normal spacing.</summary>
    None = 0,

    /// <summary>Extra space and no rule.</summary>
    Gap,

    /// <summary>Extra space with a rule drawn in it.</summary>
    Separator,
}
