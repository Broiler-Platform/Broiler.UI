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

    /// <summary>
    /// The value is not acceptable. An element reports it on itself while its
    /// <see cref="UiElement.ErrorMessage"/> is shown.
    /// </summary>
    Invalid = 512,

    /// <summary>A value must be given before the form can be submitted. See <see cref="UiElement.IsRequired"/>.</summary>
    Required = 1024,

    /// <summary>
    /// Not on screen. A container hid the element from assistive technology (see
    /// <see cref="UiElement.IsHiddenFromAccessibility"/>), which also clears <see cref="Visible"/>, or
    /// the element is laid out but scrolled or clipped entirely out of view (see
    /// <see cref="UiElement.GetVisibleBounds"/>), which keeps <see cref="Visible"/>, as does a tab
    /// whose header is out of view.
    /// </summary>
    /// <remarks>
    /// List items are the exception: a row scrolled out of a list reports Offscreen without
    /// <see cref="Visible"/>, the contract item realization already relied on. Read Offscreen, not the
    /// absence of Visible, to tell whether something is on screen. See Broiler.UI ADR 0028.
    /// </remarks>
    Offscreen = 2048,

    /// <summary>
    /// An expandable element whose content is hidden. The counterpart of <see cref="Expanded"/>, so a
    /// closed disclosure can be told apart from an element that does not expand at all.
    /// </summary>
    Collapsed = 4096,
}
