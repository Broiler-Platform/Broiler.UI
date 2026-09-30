using System.Collections.Generic;

namespace Broiler.UI.Menu;

// Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: two separately constructed items share one Children list, so a submenu added under one appears under the other
// Broiler-Human:        PENDING
public sealed class UiMenuItem
{
    public UiMenuItem(string id, string text)
    {
        Id = id;
        Text = text;
    }

    public string Id { get; }

    public string Text { get; set; }

    public string? CommandName { get; set; }

    public char? AccessKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsSeparator { get; set; }

    public bool IsCheckable { get; set; }

    public bool IsChecked { get; set; }

    public IList<UiMenuItem> Children { get; } = [];
}
