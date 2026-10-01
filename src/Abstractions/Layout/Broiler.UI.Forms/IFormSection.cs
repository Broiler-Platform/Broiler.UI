namespace Broiler.UI.Forms;

/// <summary>
/// Defines the neutral contract for a named section of a form with optional disclosure.
/// </summary>
public interface IFormSection
{
    string Summary { get; set; }
    bool IsExpanded { get; set; }
}
