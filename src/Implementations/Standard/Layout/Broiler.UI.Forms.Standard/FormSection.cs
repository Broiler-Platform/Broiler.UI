using Broiler.Graphics.Geometry;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Panel.Standard;

namespace Broiler.UI.Forms.Standard;

/// <summary>Named field group with an optional keyboard-operable disclosure.</summary>
public sealed class FormSection : UiElement, IFormSection
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
            heading.Font = heading.Font with { Size = 20 };
            _layout.AddChild(heading);
        }
        if (collapsible)
        {
            Toggle = new StandardButton();
            Toggle.Clicked += (_, _) => IsExpanded = !IsExpanded;
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
            if (Toggle is not null) Toggle.Text = $"{(value ? "Hide" : "Show")} {_title}";
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }
    protected override BSize MeasureCore(BSize availableSize) => _layout.Measure(availableSize);
    protected override void ArrangeCore(BRect finalRect) => _layout.Arrange(finalRect);
    protected override UiSemanticNode GetSemanticNodeCore() => new(UiSemanticRole.Group, _title,
        Bounds, UiSemanticState.Visible | (IsExpanded ? UiSemanticState.Expanded : UiSemanticState.None),
        Children.Select(c => c.GetSemanticNode()).ToArray(), Id: SemanticId);
}
