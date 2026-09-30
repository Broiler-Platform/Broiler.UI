namespace Broiler.UI.RadioButton;

// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: two scopes constructed with the same name are treated as one group, so their buttons uncheck each other
// Broiler-Human:        PENDING
public sealed class UiRadioGroupScope
{
    // Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiRadioGroupScope(string? name = null)
    {
        Name = name ?? string.Empty;
    }

    public string Name { get; }
}
