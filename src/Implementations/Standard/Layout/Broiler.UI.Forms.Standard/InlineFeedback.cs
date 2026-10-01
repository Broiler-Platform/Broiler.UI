using Broiler.Graphics.Geometry;
using Broiler.UI;
using Broiler.UI.Label.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.Forms.Standard;

/// <summary>Text and semantic state always accompany the status color. No animation timer is needed.</summary>
public sealed class InlineFeedback : UiElement, IInlineFeedback, IStandardThemedControl
{
    private readonly StandardLabel _label = FormField.Text("");
    private StandardThemeTokens _theme = StandardControlPaint.Theme;
    public InlineFeedback() { AddChild(_label); Visibility = UiVisibility.Collapsed; }
    public string Message { get; private set; } = "";
    public FeedbackKind Kind { get; private set; }
    public void Set(string? message, FeedbackKind kind = FeedbackKind.Information)
    {
        if (IsDisposed || (Message == (message ?? "") && Kind == kind)) return;
        Message = message ?? "";
        Kind = kind;
        _label.Text = Message.Length == 0 ? "" : $"{kind}: {Message}";
        Visibility = Message.Length == 0 ? UiVisibility.Collapsed : UiVisibility.Visible;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        if (Message.Length > 0) Session?.AnnounceStatus(this, _label.DisplayText);
    }
    public void ApplyTheme(StandardThemeTokens theme) { _theme = theme; Invalidate(UiInvalidationKind.Render); }
    protected override BSize MeasureCore(BSize availableSize)
    {
        var size = _label.Measure(new BSize(Math.Max(0, availableSize.Width - 20), double.PositiveInfinity));
        return new BSize(size.Width + 20, size.Height + 16);
    }
    protected override void ArrangeCore(BRect finalRect) => _label.Arrange(new BRect(finalRect.X + 12, finalRect.Y + 8, Math.Max(0, finalRect.Width - 20), Math.Max(0, finalRect.Height - 16)));
    protected override void RenderCore(UiRenderContext context)
    {
        var color = Kind switch { FeedbackKind.Error => _theme.Danger, FeedbackKind.Warning => _theme.Warning,
            FeedbackKind.Success => _theme.Success, _ => _theme.Info };
        context.RenderList.FillRect(Bounds, _theme.SurfaceAlt);
        context.RenderList.FillRect(new BRect(Bounds.X, Bounds.Y, 3, Bounds.Height), color);
        _label.Foreground = _theme.Text;
        base.RenderCore(context);
    }
    protected override UiSemanticNode GetSemanticNodeCore() => new(UiSemanticRole.StatusAnnouncement,
        _label.DisplayText, Bounds, UiSemanticState.Visible | (Kind == FeedbackKind.Error ? UiSemanticState.Invalid : UiSemanticState.None), [], Id: SemanticId);
}
