using Broiler.UI;

namespace Broiler.UI.Forms;

/// <summary>
/// Defines the neutral contract for a labeled form field with error presentation.
/// </summary>
public interface IFormField
{
    UiElement Control { get; }
    string Error { get; }
    void SetError(string? message);
}
