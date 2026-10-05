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

    /// <summary>The space between the action strip and the feedback below it, as between stacked banners.</summary>
    private const double FeedbackGap = 4;

    protected override BSize MeasureCore(BSize availableSize)
    {
        availableSize = new BSize(double.IsFinite(availableSize.Width) ? availableSize.Width : 640,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 480);
        double width = Math.Max(0, availableSize.Width - 24);
        Actions.Measure(new BSize(width, double.PositiveInfinity));
        double feedbackHeight = _feedbackContent.Measure(new BSize(width, double.PositiveInfinity)).Height;
        _feedback.Visibility = feedbackHeight > 0 ? UiVisibility.Visible : UiVisibility.Collapsed;
        double shownFeedback = Math.Min(feedbackHeight, Math.Min(112, availableSize.Height / 4));
        _feedback.Measure(Grow(new BSize(width, shownFeedback), _feedback));
        shownFeedback = Inner(_feedback);
        double gap = shownFeedback > 0 ? FeedbackGap : 0;
        Content.Measure(Grow(new BSize(width, Math.Max(0, availableSize.Height - Actions.DesiredSize.Height - shownFeedback - gap - 32)), Content));
        return availableSize;
    }
    protected override void ArrangeCore(BRect finalRect)
    {
        double width = Math.Max(0, finalRect.Width - 24);
        double feedbackHeight = Math.Min(Inner(_feedback), finalRect.Height / 4);
        double gap = feedbackHeight > 0 ? FeedbackGap : 0;
        double actionHeight = Math.Min(Actions.DesiredSize.Height, Math.Max(0, finalRect.Height - feedbackHeight - gap - 24));
        double contentHeight = Math.Max(0, finalRect.Height - actionHeight - feedbackHeight - gap - 32);
        Content.Arrange(Widen(new BRect(finalRect.X + 12, finalRect.Y + 12, width, contentHeight), Content));
        Actions.Arrange(new BRect(finalRect.X + 12, finalRect.Y + 20 + contentHeight, width, actionHeight));
        _feedback.Arrange(Widen(new BRect(finalRect.X + 12, finalRect.Y + 20 + contentHeight + actionHeight + gap, width, feedbackHeight), _feedback));
    }

    // A viewport reaches out into the space around it by the room it leaves around its content for focus rings,
    // so the fields and banners inside it keep the edges of the action strip and their places: into the 12 DIP
    // margins at the sides, and above and below no further than the 4 DIP between the strip and the feedback.
    private static double Room(FormViewport viewport) => Math.Min(12, viewport.Scroll.HorizontalContentInset);

    private static double VerticalRoom(FormViewport viewport) => Math.Min(FeedbackGap, viewport.Scroll.VerticalContentInset);

    private static BSize Grow(BSize size, FormViewport viewport) =>
        new(size.Width + (2 * Room(viewport)), size.Height > 0 ? size.Height + (2 * VerticalRoom(viewport)) : 0);

    // The height a viewport shows its content in, without the room above and below it.
    private static double Inner(FormViewport viewport) =>
        Math.Max(0, viewport.DesiredSize.Height - (2 * VerticalRoom(viewport)));

    private static BRect Widen(BRect rect, FormViewport viewport)
    {
        double room = Room(viewport);
        double vertical = rect.Height > 0 ? VerticalRoom(viewport) : 0;
        return new BRect(rect.X - room, rect.Y - vertical, rect.Width + (2 * room), rect.Height + (2 * vertical));
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
        // The whole field, including the error below the control, comes into view, with the room the
        // viewport keeps for a ring at its top and bottom. A field taller than the viewport keeps the
        // control's top visible instead.
        double showTop = scroll.ContentBounds.Top + scroll.VerticalContentInset;
        double showBottom = scroll.ContentBounds.Bottom - scroll.VerticalContentInset;
        double bottom = Math.Max(field.Control.Bounds.Bottom, element.Bounds.Bottom);
        double delta = bottom > showBottom
            ? Math.Min(bottom - showBottom, field.Control.Bounds.Top - showTop)
            : element.Bounds.Top < showTop ? element.Bounds.Top - showTop : 0;
        scroll.ScrollBy(0, delta);
    }
}
