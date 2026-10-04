using System;

namespace Broiler.UI;

[Flags]
public enum UiSemanticState
{
    None = 0,
    Visible = 1,
    Enabled = 2,
    Focused = 4,
    ReadOnly = 8,
    Checked = 16,
    Indeterminate = 32,
    Selected = 64,

    /// <summary>
    /// An expandable element whose content is shown. An expandable element reports exactly one of
    /// <see cref="Expanded"/> and <see cref="Collapsed"/>; an element that cannot expand reports neither.
    /// </summary>
    Expanded = 128,
    Modal = 256,

    Invalid = 512,
    Required = 1024,
    Offscreen = 2048,

    /// <summary>
    /// An expandable element whose content is hidden. The counterpart of <see cref="Expanded"/>, so a
    /// closed disclosure can be told apart from an element that does not expand at all.
    /// </summary>
    Collapsed = 4096,
}
