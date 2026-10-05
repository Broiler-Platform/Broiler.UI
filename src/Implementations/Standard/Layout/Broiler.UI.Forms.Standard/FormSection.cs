using Broiler.Graphics.Geometry;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Panel.Standard;

namespace Broiler.UI.Forms.Standard;

/// <summary>Named field group with an optional keyboard-operable disclosure.</summary>
/// <remarks>
/// A collapsible section follows the disclosure pattern: its <see cref="Toggle"/> discloses the section
/// (<see cref="UiElement.Discloses"/>), so the focused button reports Expanded or Collapsed and a host
/// can expand and collapse it there, and it controls the <see cref="Content"/>
/// (<see cref="UiElement.Controls"/>), which a host exposes while it is shown. The group itself reports
/// neither state. See Broiler.UI ADR 0028.
/// </remarks>
public sealed class FormSection : UiElement, IFormSection, IUiExpandable
{
    private readonly StandardPanel _layout = new() { Spacing = 8 };
    private readonly string _title;
    private readonly Broiler.UI.Label.Standard.StandardLabel _summary = FormField.Text("");
    public FormSection(string title, string description = "", bool collapsible = false, bool expanded = true)
    {
        _title = title;
        if (!collapsible)
        {
            var heading = FormField.Text(title);
            heading.TextStyle = Broiler.UI.Standard.StandardTextStyle.Subtitle;
            _layout.AddChild(heading);
        }
        if (collapsible)
        {
            Toggle = new StandardButton();
            Toggle.Clicked += (_, _) => IsExpanded = !IsExpanded;
            Toggle.Discloses = this;
            // The section is what expands; its content is where the toggle sends a reader.
            Toggle.Controls = Content;
            _layout.AddChild(Toggle);
        }
        _summary.Visibility = UiVisibility.Collapsed;
        _layout.AddChild(_summary);
        if (description.Length > 0) Content.AddChild(FormField.Text(description));
        _layout.AddChild(Content);
        AddChild(_layout);
        IsExpanded = !collapsible || expanded;
    }
    public StandardPanel Content { get; } = new() { Spacing = 12 };
    public StandardButton? Toggle { get; }
    public string Summary
    {
        get => _summary.Text;
        set
        {
            _summary.Text = value;
            _summary.Visibility = value.Length == 0 ? UiVisibility.Collapsed : UiVisibility.Visible;
        }
    }
    public bool IsExpanded
    {
        get => Content.Visibility == UiVisibility.Visible;
        set
        {
            if (!value && Toggle is null) return;
            if (!value && Session?.FocusedElement is { } focused)
                for (var parent = focused; parent is not null; parent = parent.Parent)
                    if (parent == Content) { Session.SetFocus(Toggle); break; }
            Content.Visibility = value ? UiVisibility.Visible : UiVisibility.Collapsed;
            if (Toggle is not null)
            {
                Toggle.Text = $"{(value ? "Hide" : "Show")} {_title}";
                // The toggle reports this section's state, so its semantics change with it.
                Toggle.Invalidate(UiInvalidationKind.Semantic);
            }
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>Shows the content.</summary>
    /// <returns>False when it was already shown.</returns>
    public bool Expand()
    {
        if (IsExpanded) return false;
        IsExpanded = true;
        return true;
    }

    /// <summary>Hides the content, moving focus inside it to the toggle.</summary>
    /// <returns>False when it was already hidden or the section is not collapsible.</returns>
    public bool Collapse()
    {
        if (!IsExpanded || Toggle is null) return false;
        IsExpanded = false;
        return true;
    }
    protected override BSize MeasureCore(BSize availableSize) => _layout.Measure(availableSize);
    protected override void ArrangeCore(BRect finalRect) => _layout.Arrange(finalRect);
    // The expand state belongs to the toggle that discloses the section, not to the group.
    protected override UiSemanticNode GetSemanticNodeCore() => new(UiSemanticRole.Group, _title,
        Bounds, UiSemanticState.Visible, Children.Select(c => c.GetSemanticNode()).ToArray(), Id: SemanticId);
}
