using System;

namespace Broiler.UI.TabView;

// Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: changing Header or IsDirty to a new value raises no Changed, so the tab strip and its accessible description keep the old label or a clean state
// Broiler-Human:        PENDING
public sealed class UiTabItem
{
    private string _header;
    private bool _isDirty;

    internal UiTabItem(string id, string header, UiElement? content)
    {
        Id = id;
        _header = header;
        Content = content;
    }

    /// <summary>
    /// Raised when the header or the dirty flag changes, so the view can
    /// repaint one tab without being told to rebuild the strip.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    internal event Action<UiTabItem>? Changed;

    /// <summary>
    /// Caller-supplied and stable. Reorder, overflow, and activation are all
    /// expressed in terms of this rather than an index, so a host holding "the
    /// tab for document X" stays correct across every operation.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The displayed label. Settable because a rename must not lose the tab's
    /// position, its content, or its identity.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a header that differs from the current one only by letter case is treated as unchanged and never displayed
    // Broiler-Human:        PENDING
    public string Header
    {
        get => _header;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.Equals(_header, value, StringComparison.Ordinal))
                return;
            _header = value;
            Changed?.Invoke(this);
        }
    }

    /// <summary>
    /// Whether the tab's content has unsaved changes. Presented as a distinct
    /// visual element and in the accessible description — never by colour
    /// alone, which is unreadable in high contrast and to a viewer who cannot
    /// distinguish the hues.
    /// </summary>
    // Broiler-AI:           Origin=AI; Spec=ADR-0024; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: setting IsDirty to true raises no Changed, so the unsaved-changes marker and its spoken description do not appear
    // Broiler-Human:        PENDING
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty == value)
                return;
            _isDirty = value;
            Changed?.Invoke(this);
        }
    }

    public UiElement? Content { get; }
}
