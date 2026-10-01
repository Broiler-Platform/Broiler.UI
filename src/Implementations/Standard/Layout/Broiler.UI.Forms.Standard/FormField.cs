using Broiler.Graphics.Geometry;
using Broiler.UI;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.Forms.Standard;

/// <summary>A labeled control with optional description and a persistent, literal validation message.</summary>
public sealed class FormField : UiElement, IFormField, IStandardThemedControl
{
    private readonly StandardPanel _layout = new() { Spacing = 4 };
    private readonly StandardLabel _description;
    private readonly StandardLabel _error;
    private StandardThemeTokens _theme = StandardControlPaint.Theme;
    public FormField(string label, UiElement control, string description = "")
    {
        Control = control;
        Label = Text(label);
        Label.Target = control;
        _description = Text(description);
        _error = Text("");
        _description.Visibility = description.Length == 0 ? UiVisibility.Collapsed : UiVisibility.Visible;
        _error.Visibility = UiVisibility.Collapsed;
        _layout.AddChild(Label);
        _layout.AddChild(control);
        _layout.AddChild(_description);
        _layout.AddChild(_error);
        AddChild(_layout);
        if (control is StandardEdit edit) edit.TextChanged += (_, _) => SetError(null);
    }

    public StandardLabel Label { get; }
    public UiElement Control { get; }
    public string Error { get; private set; } = "";
    public void SetError(string? message)
    {
        if (IsDisposed || Error == (message ?? "")) return;
        Error = message ?? "";
        _error.Text = Error.Length == 0 ? "" : "Error: " + Error;
        _error.Visibility = Error.Length == 0 ? UiVisibility.Collapsed : UiVisibility.Visible;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public void ApplyTheme(StandardThemeTokens theme) { _theme = theme; Invalidate(UiInvalidationKind.Render); }
    protected override BSize MeasureCore(BSize availableSize) => _layout.Measure(availableSize);
    protected override void ArrangeCore(BRect finalRect) => _layout.Arrange(finalRect);
    protected override void RenderCore(UiRenderContext context)
    {
        // Preserve semantic roles after the theme controller visits child labels.
        _description.Foreground = _theme.TextMuted;
        _error.Foreground = _theme.Danger;
        base.RenderCore(context);
    }
    protected override UiSemanticNode GetSemanticNodeCore() => new(UiSemanticRole.Group,
        string.Join(". ", new[] { Label.DisplayText, _description.DisplayText, Error }.Where(s => s.Length > 0)),
        Bounds, UiSemanticState.Visible | (Error.Length > 0 ? UiSemanticState.Invalid : UiSemanticState.None),
        Children.Select(c => c.GetSemanticNode()).ToArray(), Id: SemanticId);

    internal static StandardLabel Text(string text) => new()
    {
        Text = text, IsLiteral = true, Wrapping = UiTextWrapping.Wrap, Foreground = StandardControlPaint.Text,
    };
}
