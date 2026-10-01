namespace Broiler.UI.Forms;

/// <summary>
/// Defines the neutral contract for a form surface supporting invalid field reveal.
/// </summary>
public interface IFormSurface
{
    void Reveal(IFormField field);
}
