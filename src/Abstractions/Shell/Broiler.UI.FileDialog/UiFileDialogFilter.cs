using System;

namespace Broiler.UI.FileDialog;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: a blank or whitespace filter name is accepted instead of refused with ArgumentException
// Broiler-Human:        PENDING
public sealed class UiFileDialogFilter
{
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a blank or whitespace filter name is accepted instead of refused with ArgumentException
    // Broiler-Human:        PENDING
    public UiFileDialogFilter(string name, string pattern, string defaultExtension = "")
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A file dialog filter name is required.", nameof(name)) : name.Trim();
        Pattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();
        DefaultExtension = NormalizeExtension(defaultExtension);
    }

    public string Name { get; }

    public string Pattern { get; }

    public string DefaultExtension { get; }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an extension given as 'txt' or ' .txt ' is not returned as '.txt'
    // Broiler-Human:        PENDING
    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        string trimmed = extension.Trim();
        if (StringComparer.Ordinal.Equals(trimmed, "*"))
            return string.Empty;

        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }
}
