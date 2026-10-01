using Broiler.Graphics.Geometry;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Toolbar;
using Broiler.UI.Toolbar.Standard;

namespace Broiler.UI.Forms.Standard;

/// <summary>Scrollable form with wrapping actions that remain visible; long feedback scrolls independently.</summary>
public sealed class FormSurface : UiElement, IFormSurface
{
    private readonly FormViewport _feedback;
    private readonly UiElement _feedbackContent;
    public FormSurface(UiElement content, StandardToolbar actions, UiElement feedback)
    {
        Content = new FormViewport(content);
        Actions = actions;
        _feedbackContent = feedback;
        _feedback = new FormViewport(feedback);
        // Tree order is the keyboard order: fields, actions, then feedback.
        AddChild(Content); AddChild(Actions); AddChild(_feedback);
    }
    public FormViewport Content { get; }
    public StandardToolbar Actions { get; }
    protected override BSize MeasureCore(BSize availableSize)
    {
        availableSize = new BSize(double.IsFinite(availableSize.Width) ? availableSize.Width : 640,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 480);
        double width = Math.Max(0, availableSize.Width - 24);
        Actions.Measure(new BSize(width, double.PositiveInfinity));
        double feedbackHeight = _feedbackContent.Measure(new BSize(width, double.PositiveInfinity)).Height;
        _feedback.Visibility = feedbackHeight > 0 ? UiVisibility.Visible : UiVisibility.Collapsed;
        _feedback.Measure(new BSize(width, Math.Min(feedbackHeight, Math.Min(112, availableSize.Height / 4))));
        Content.Measure(new BSize(width, Math.Max(0, availableSize.Height - Actions.DesiredSize.Height - _feedback.DesiredSize.Height - 32)));
        return availableSize;
    }
    protected override void ArrangeCore(BRect finalRect)
    {
        double width = Math.Max(0, finalRect.Width - 24);
        double feedbackHeight = Math.Min(_feedback.DesiredSize.Height, finalRect.Height / 4);
        double actionHeight = Math.Min(Actions.DesiredSize.Height, Math.Max(0, finalRect.Height - feedbackHeight - 24));
        double contentHeight = Math.Max(0, finalRect.Height - actionHeight - feedbackHeight - 32);
        Content.Arrange(new BRect(finalRect.X + 12, finalRect.Y + 12, width, contentHeight));
        Actions.Arrange(new BRect(finalRect.X + 12, finalRect.Y + 20 + contentHeight, width, actionHeight));
        _feedback.Arrange(new BRect(finalRect.X + 12, finalRect.Y + 20 + contentHeight + actionHeight, width, feedbackHeight));
    }
    public static StandardToolbar ActionBar(params StandardButton[] buttons)
    {
        var bar = new StandardToolbar { Overflow = UiToolbarOverflow.Wrap, Padding = 4, Spacing = 8, PreferredSize = new BSize(0, 36) };
        foreach (var button in buttons) bar.AddChild(button);
        return bar;
    }
    public void Reveal(FormField field) => Reveal((IFormField)field);

    public void Reveal(IFormField field)
    {
        if (field is not UiElement element) return;
        // Expand ancestors before measuring, then use the existing host-independent reveal behavior.
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
            if (parent is IFormSection section) section.IsExpanded = true;
        Session?.RenderFrame();
        Session?.SetFocus(field.Control);
        var scroll = Content.Scroll;
        double delta = field.Control.Bounds.Bottom > scroll.ContentBounds.Bottom ? field.Control.Bounds.Bottom - scroll.ContentBounds.Bottom
            : element.Bounds.Top < scroll.ContentBounds.Top ? element.Bounds.Top - scroll.ContentBounds.Top : 0;
        scroll.ScrollBy(0, delta);
    }
}
