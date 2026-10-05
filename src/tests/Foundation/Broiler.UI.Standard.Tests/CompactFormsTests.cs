using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.UI.Forms;
using Broiler.UI.Forms.Standard;
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Edit.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Standard;

namespace Broiler.UI.Standard.Tests;

[Collection(GlobalThemeCollection.Name)]
public sealed class CompactFormsTests
{
    [Theory]
    [InlineData(360, false, false, 1)]
    [InlineData(640, true, false, 1)]
    [InlineData(640, false, true, 2)]
    [InlineData(640, true, true, 2)]
    public void LongFeedbackAndLargeLabelsLeaveActionsVisible(int width, bool dark, bool contrast, int textScale)
    {
        var theme = contrast ? (dark ? StandardThemeTokens.HighContrastDark : StandardThemeTokens.HighContrastLight)
            : dark ? StandardThemeTokens.Dark : StandardThemeTokens.Light;
        StandardControlPaint.ApplyTheme(theme);
        try
        {
            var fields = new StandardPanel();
            for (int i = 0; i < 8; i++)
            {
                var field = new FormField("A long label with literal & symbols · إعدادات البريد", new StandardEdit(), "Description wraps across multiple lines in a narrow window.");
                fields.AddChild(field);
                foreach (var label in Descendants(field).OfType<StandardLabel>())
                    label.Font = label.Font with { Size = label.Font.Size * textScale };
            }
            var feedback = new InlineFeedback();
            feedback.Set(string.Join(" ", Enumerable.Repeat("Server & connection feedback with details.", 50)), FeedbackKind.Error);
            var save = new StandardButton { Text = "Save settings", IsDefault = true };
            var cancel = new StandardButton { Text = "Cancel connection test" };
            save.Font = save.Font with { Size = save.Font.Size * textScale };
            cancel.Font = cancel.Font with { Size = cancel.Font.Size * textScale };
            using var surface = new FormSurface(fields, FormSurface.ActionBar(save, cancel), feedback);
            using var session = new StandardUiSessionBuilder().Build(new Host(width, 480));
            session.AddRoot(surface);
            session.RenderFrame();
            Assert.InRange(save.Bounds.Top, surface.Content.Bounds.Bottom, 480);
            Assert.InRange(cancel.Bounds.Bottom, save.Bounds.Top, 480);
            Assert.True(surface.Content.Bounds.Height > 200);
            Assert.False(surface.Content.Scroll.HasHorizontalScrollbar);
            Assert.True(surface.Content.Scroll.HasVerticalScrollbar);
            var feedbackScroll = Descendants(surface).OfType<Broiler.UI.ScrollView.Standard.StandardScrollView>().Last();
            Assert.True(feedbackScroll.HasVerticalScrollbar);
            Assert.Contains("Server & connection", feedback.GetSemanticNode().Name);
            Assert.Equal(UiSemanticRole.StatusAnnouncement, feedback.GetSemanticNode().Role);
            Assert.True(feedback.GetSemanticNode().State.HasFlag(UiSemanticState.Invalid));
            double contentHeight = surface.Content.Bounds.Height;
            feedback.Set("");
            session.RenderFrame();
            Assert.True(surface.Content.Bounds.Height > contentHeight);
        }
        finally { StandardControlPaint.ApplyTheme(StandardThemeTokens.Light); }
    }

    [Fact]
    public void DisclosureRetainsValuesAndMovesFocusOutOfCollapsedContent()
    {
        using var section = new FormSection("Cc & Bcc", collapsible: true);
        var edit = new StandardEdit { Text = "hidden@example.test" };
        section.Content.AddChild(new FormField("Bcc", edit));
        using var session = new StandardUiSessionBuilder().Build(new Host(640, 480));
        session.AddRoot(section);
        session.RenderFrame();
        session.SetFocus(edit);
        section.Toggle!.Click();
        Assert.False(section.IsExpanded);
        Assert.Equal("Show Cc & Bcc", section.Toggle.Text);
        Assert.Same(section.Toggle, session.FocusedElement);
        Assert.Equal("hidden@example.test", edit.Text);
        // The toggle carries the disclosure state; the group reports none (ADR 0028).
        Assert.True(section.Toggle.GetSemanticNode().State.HasFlag(UiSemanticState.Collapsed));
        Assert.False(section.Toggle.GetSemanticNode().State.HasFlag(UiSemanticState.Expanded));
        Assert.False(section.GetSemanticNode().State.HasFlag(UiSemanticState.Expanded));
        Assert.False(section.GetSemanticNode().State.HasFlag(UiSemanticState.Collapsed));
        #pragma warning disable CS0618
        var input = new StandardLegacyGraphicsInputAdapter("compact-forms");
        Assert.True(session.DispatchInput(input.FromKey(new BKeyEventArgs(13, false, false, false), KeyboardKeyTransition.Down)));
        session.DispatchInput(input.FromKey(new BKeyEventArgs(13, false, false, false), KeyboardKeyTransition.Up));
        #pragma warning restore CS0618
        Assert.True(section.IsExpanded);
        Assert.True(section.Toggle.GetSemanticNode().State.HasFlag(UiSemanticState.Expanded));
        Assert.False(section.Toggle.GetSemanticNode().State.HasFlag(UiSemanticState.Collapsed));
        Assert.False(section.GetSemanticNode().State.HasFlag(UiSemanticState.Expanded));
        Assert.Equal("hidden@example.test", edit.Text);
    }

    [Fact]
    public void FeedbackAnnouncesChangesOnceAndUnconstrainedSurfaceHasFiniteSize()
    {
        var feedback = new InlineFeedback();
        using var surface = new FormSurface(new StandardPanel(), FormSurface.ActionBar(), feedback);
        var size = surface.Measure(new BSize(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(double.IsFinite(size.Width) && double.IsFinite(size.Height));
        using var session = new StandardUiSessionBuilder().Build(new Host(640, 480));
        session.AddRoot(surface);
        var announced = new List<string?>();
        session.SemanticChanged += (_, e) => { if (e.Change == UiSemanticChangeKind.StatusAnnounced) announced.Add(e.Message); };
        feedback.Set("Connecting & checking", FeedbackKind.Progress);
        feedback.Set("Connecting & checking", FeedbackKind.Progress);
        Assert.Equal(new[] { "Progress: Connecting & checking" }, announced);
    }

    [Fact]
    public void RevealBringsAFieldsErrorIntoViewNotOnlyItsControl()
    {
        var fields = new StandardPanel { Spacing = 8 };
        FormField? last = null;
        for (int index = 1; index <= 12; index++)
            fields.AddChild(last = new FormField($"Field {index}", new StandardEdit()));
        using var surface = new FormSurface(fields, FormSurface.ActionBar(new StandardButton { Text = "Save" }), new InlineFeedback());
        using var session = new StandardUiSessionBuilder().Build(new Host(640, 480));
        session.AddRoot(surface);
        session.RenderFrame();

        last!.SetError("Enter a value.");
        surface.Reveal(last);
        session.RenderFrame();
        Assert.True(last.Bounds.Bottom <= surface.Content.Scroll.ContentBounds.Bottom + 0.5,
            $"The error ends at {last.Bounds.Bottom}, below the viewport's {surface.Content.Scroll.ContentBounds.Bottom}.");
        Assert.Same(last.Control, session.FocusedElement);
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (var child in element.Children)
            foreach (var nested in Descendants(child)) yield return nested;
    }

    private sealed class Host(int width, int height) : IUiHost
    {
        public BSize ViewportSize => new(width, height);
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
