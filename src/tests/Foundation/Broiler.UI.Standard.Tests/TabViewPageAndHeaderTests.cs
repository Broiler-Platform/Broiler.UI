using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.TabView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The tab view draws its page inside the page's frame, and marks the selected tab and the focus on the
/// selected tab's header. ADR 0031.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class TabViewPageAndHeaderTests
{
    private static readonly BColor PageInk = BColor.FromArgb(0xFF, 0x12, 0x9A, 0x34);

    [Fact]
    public void TheSelectedPageIsDrawnHitTestedAndReportedInsideItsFrame()
    {
        var page = new Swatch(PageInk);
        var tabs = new StandardTabView();
        tabs.AddTab("inbox", "Inbox", page);
        tabs.AddTab("sent", "Sent", new Swatch(BColor.FromArgb(0xFF, 0x40, 0x40, 0x40)));
        using UiSession session = Attach(tabs, 300, 200);

        // The content is still arranged at the whole page, edge to edge with the frame.
        BRect pageRect = new(0, tabs.EffectiveHeaderHeight, 300, 200 - tabs.EffectiveHeaderHeight);
        Assert.Equal(pageRect, page.Bounds);
        BRect inside = new(1, pageRect.Top + 1, 298, pageRect.Height - 2);

        // What it draws at its edges stays inside the frame: the frame is drawn, then the page under a clip.
        BRenderList frame = session.RenderFrame();
        Assert.Equal(inside, ClipAt(frame, command => command is BRenderCommand.FillRect fill && fill.Color == PageInk));
        Assert.Contains(frame.Commands.OfType<BRenderCommand.StrokeRoundedRect>(), stroke => stroke.Rect == pageRect && stroke.Color == tabs.BorderColor);
        frame.Validate();

        // A host reports the same clip, and a pointer on the frame is not the page's.
        Assert.Equal(inside, page.GetVisibleBounds());
        Assert.Same(tabs, session.HitTest(new BPoint(0.5, pageRect.Top + 40)));
        Assert.Same(tabs, session.HitTest(new BPoint(150, pageRect.Bottom - 0.5)));
        Assert.Same(page, session.HitTest(new BPoint(1.5, pageRect.Top + 40)));
        Assert.Same(page, session.HitTest(new BPoint(150, pageRect.Bottom - 1.5)));
    }

    public static TheoryData<StandardThemeTokens> Presets() => SelectionTextRoleTests.Presets();

    [Theory]
    [MemberData(nameof(Presets))]
    public void TheSelectedLabelIsAccentTextThatReadsOnItsHeader(StandardThemeTokens theme)
    {
        StandardTabView tabs = ThreeTabs();
        tabs.ApplyTheme(theme);
        using UiSession session = Attach(tabs, 400, 200);

        BRenderList frame = session.RenderFrame();

        Assert.Equal(theme.AccentText, Label(frame, "Inbox").Color);
        Assert.Equal(theme.Text, Label(frame, "Sent").Color);
        double ratio = StandardContrast.Ratio(theme.AccentText, tabs.SelectedHeaderBackground);
        Assert.True(ratio >= StandardContrast.AaNormalText, $"{theme.Name}: the selected label reads at {ratio:0.00}:1.");
    }

    [Fact]
    public void AnUnthemedTabViewTakesTheSelectedLabelFromTheSharedPaletteUntilTheApplicationSetsIt()
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardTabView tabs = ThreeTabs();
            using UiSession session = Attach(tabs, 400, 200);

            StandardControlPaint.ApplyTheme(StandardThemeTokens.Dark);
            Assert.Equal(StandardThemeTokens.Dark.AccentText, Label(session.RenderFrame(), "Inbox").Color);

            BColor custom = BColor.FromArgb(0xFF, 0xF0, 0xC0, 0x40);
            tabs.SelectedHeaderForeground = custom;
            Assert.Equal(custom, Label(session.RenderFrame(), "Inbox").Color);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    private static StandardTabView ThreeTabs()
    {
        var tabs = new StandardTabView();
        tabs.AddTab("inbox", "Inbox", new Swatch(PageInk));
        tabs.AddTab("sent", "Sent", new Swatch(PageInk));
        tabs.AddTab("settings", "Settings", new Swatch(PageInk));
        return tabs;
    }

    private static BTextRun Label(BRenderList list, string header) =>
        Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == header).Text;

    /// <summary>
    /// The clip in force when the first command <paramref name="match"/> accepts was drawn: the
    /// intersection of every clip pushed and not yet popped, or null when none was.
    /// </summary>
    private static BRect? ClipAt(BRenderList list, Func<BRenderCommand, bool> match)
    {
        var clips = new List<BRect>();
        foreach (BRenderCommand command in list.Commands)
        {
            if (command is BRenderCommand.PushClip push)
                clips.Add(push.Rect);
            else if (command is BRenderCommand.PopClip)
                clips.RemoveAt(clips.Count - 1);
            else if (match(command))
                return clips.Count == 0 ? null : clips.Aggregate(static (clip, next) => clip.Intersect(next));
        }

        Assert.Fail("No command matched.");
        return null;
    }

    private static UiSession Attach(UiElement root, double width, double height)
    {
        UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new TestHost(new BSize(width, height)));
        session.AddRoot(root);
        session.RenderFrame();
        return session;
    }

    /// <summary>Fills all of its bounds, as a list or a scrolled form does up to its edges.</summary>
    private sealed class Swatch(BColor color) : UiElement
    {
        protected override BSize MeasureCore(BSize availableSize) => BSize.Empty;

        protected override void RenderCore(UiRenderContext context) => context.RenderList.FillRect(Bounds, color);
    }

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize => viewportSize;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
