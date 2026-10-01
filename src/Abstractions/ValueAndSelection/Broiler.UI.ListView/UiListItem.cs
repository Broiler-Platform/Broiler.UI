namespace Broiler.UI.ListView;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed record UiListItem(string Id, string Text)
{
    public string? SecondaryText { get; init; }
    public string? TertiaryText { get; init; }
    public bool IsRead { get; init; } = true;
    public object? Tag { get; init; }

    public UiListItem(
        string id,
        string text,
        string? secondaryText,
        string? tertiaryText = null,
        bool isRead = true,
        object? tag = null) : this(id, text)
    {
        SecondaryText = secondaryText;
        TertiaryText = tertiaryText;
        IsRead = isRead;
        Tag = tag;
    }
}
