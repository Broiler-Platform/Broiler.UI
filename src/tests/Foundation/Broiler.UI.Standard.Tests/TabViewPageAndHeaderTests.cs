using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Input;
using Broiler.Input.Mouse;
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

    [Theory]
    [MemberData(nameof(Presets))]
    public void OnlyTheSelectedTabIsMarkedByABarUnderItsLabelThatStandsOutFromTheStrip(StandardThemeTokens theme)
    {
        StandardTabView tabs = ThreeTabs();
        tabs.ApplyTheme(theme);
        using UiSession session = Attach(tabs, 400, 200);
        Assert.Equal(theme.AccentText, tabs.SelectedIndicatorColor);

        for (int selected = 0; selected < tabs.Tabs.Count; selected++)
        {
            tabs.SelectedIndex = selected;
            BRenderList frame = session.RenderFrame();

            // One bar, under the selected label, along the bottom of its header.
            BRect header = tabs.GetTabHeaderBounds(selected);
            BRect bar = Assert.Single(frame.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == tabs.SelectedIndicatorColor).Rect;
            Assert.Equal(new BRect(header.Left + tabs.HeaderPaddingX, header.Bottom - 3, header.Width - (tabs.HeaderPaddingX * 2), 3), bar);
        }

        // A mark that is not text needs 3:1 against what lies around it: the selected header's fill, and the
        // strip, which shows what is behind the tab view.
        foreach (BColor strip in new[] { tabs.SelectedHeaderBackground, theme.Surface, theme.SurfaceAlt })
        {
            double ratio = StandardContrast.Ratio(tabs.SelectedIndicatorColor, strip);
            Assert.True(ratio >= StandardContrast.AaLargeOrUi, $"{theme.Name}: the bar stands out at {ratio:0.00}:1 from {strip}.");
        }
    }

    [Fact]
    public void TheBarLeavesTheHeadersAndTheirHitTestingAsTheyWere()
    {
        StandardTabView tabs = ThreeTabs();
        using UiSession session = Attach(tabs, 400, 200);
        double height = tabs.EffectiveHeaderHeight;
        BRect[] headers = [.. Enumerable.Range(0, tabs.Tabs.Count).Select(tabs.GetTabHeaderBounds)];
        Assert.Equal(tabs.HeaderHeight, height);

        // Turned off, nothing is drawn and nothing moves.
        tabs.SelectedIndicatorThickness = 0;
        BRenderList frame = session.RenderFrame();
        Assert.DoesNotContain(frame.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == tabs.SelectedIndicatorColor);
        Assert.Equal(height, tabs.EffectiveHeaderHeight);
        Assert.Equal(headers, Enumerable.Range(0, tabs.Tabs.Count).Select(tabs.GetTabHeaderBounds));

        // A color of its own leaves the label alone.
        BColor custom = BColor.FromArgb(0xFF, 0xC0, 0x30, 0x90);
        tabs.SelectedIndicatorThickness = 3;
        tabs.SelectedIndicatorColor = custom;
        frame = session.RenderFrame();
        Assert.Single(frame.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == custom);
        Assert.Equal(tabs.SelectedHeaderForeground, Label(frame, "Inbox").Color);
        Assert.Equal(height, tabs.EffectiveHeaderHeight);
        Assert.Equal(headers, Enumerable.Range(0, tabs.Tabs.Count).Select(tabs.GetTabHeaderBounds));

        // The strip where a bar is drawn still selects the tab under it.
        BRect sent = tabs.GetTabHeaderBounds(1);
        session.DispatchInput(MouseDown(sent.Left + (sent.Width / 2), sent.Bottom - 1));
        Assert.Equal(1, tabs.SelectedIndex);
        BRect bar = Assert.Single(session.RenderFrame().Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == custom).Rect;
        Assert.True(sent.Contains(new BPoint(bar.Left + (bar.Width / 2), bar.Top + (bar.Height / 2))));
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void TheFocusRingMarksTheSelectedHeaderAndFollowsTheSelection(StandardThemeTokens theme)
    {
        StandardTabView tabs = ThreeTabs();
        tabs.ApplyTheme(theme);
        using UiSession session = Attach(tabs, 400, 200);
        Assert.Empty(Rings(session.RenderFrame(), tabs));

        session.SetFocus(tabs);
        for (int selected = 0; selected < tabs.Tabs.Count; selected++)
        {
            tabs.SelectedIndex = selected;
            BRenderList frame = session.RenderFrame();
            BRect header = tabs.GetTabHeaderBounds(selected);
            BRect bar = Assert.Single(frame.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == tabs.SelectedIndicatorColor).Rect;

            // One ring, the theme's offset inside the header and above the bar: not around the view, so it
            // neither encloses the page nor crosses its content.
            BRenderCommand.StrokeRoundedRect ring = Assert.Single(Rings(frame, tabs));
            double offset = theme.FocusRingOffset;
            Assert.Equal(new BRect(header.Left + offset, header.Top + offset, header.Width - (offset * 2), header.Height - bar.Height - (offset * 2)), ring.Rect);
            Assert.Equal(tabs.FocusRing, ring.Color);
            Assert.Equal(theme.FocusRingThickness, ring.Thickness);
            Assert.True(ring.Rect.Bottom + (ring.Thickness / 2) <= bar.Top, "The ring crosses the bar.");
            Assert.True(ring.Rect.Bottom <= tabs.Bounds.Top + tabs.EffectiveHeaderHeight);

            // A focus indicator needs 3:1 against what it is drawn on.
            double ratio = StandardContrast.Ratio(ring.Color, tabs.SelectedHeaderBackground);
            Assert.True(ratio >= StandardContrast.AaLargeOrUi, $"{theme.Name}: the ring stands out at {ratio:0.00}:1.");
        }

        // The high-contrast presets draw it 2 DIP thick, thicker than their borders.
        Assert.Equal(theme.IsHighContrast ? 2 : 1, theme.FocusRingThickness);
    }

    [Fact]
    public void AFocusRingThatWouldVanishIntoTheHeaderTakesTheSelectedLabelsColor()
    {
        // A focus color equal to the selected header's fill, and a high-contrast palette whose ring is as thin
        // as its borders.
        StandardThemeTokens theme = StandardThemeTokens.HighContrastLight with { FocusRing = BColor.White, FocusRingThickness = 1 };
        StandardTabView tabs = ThreeTabs();
        tabs.ApplyTheme(theme);
        using UiSession session = Attach(tabs, 400, 200);
        session.SetFocus(tabs);

        BRenderCommand.StrokeRoundedRect ring = Assert.Single(Rings(session.RenderFrame(), tabs));

        Assert.Equal(theme.AccentText, ring.Color);
        Assert.Equal(2, ring.Thickness);
        Assert.True(StandardContrast.Ratio(ring.Color, tabs.SelectedHeaderBackground) >= StandardContrast.AaLargeOrUi);
    }

    [Fact]
    public void AFocusedTabViewWithNoTabsRingsTheView()
    {
        var tabs = new StandardTabView();
        using UiSession session = Attach(tabs, 400, 200);
        session.SetFocus(tabs);

        BRenderCommand.StrokeRoundedRect ring = Assert.Single(Rings(session.RenderFrame(), tabs));

        Assert.Equal(StandardControlPaint.Inset(tabs.Bounds, StandardControlPaint.FocusRingOffset), ring.Rect);
    }

    /// <summary>Every rounded stroke but the page's frame.</summary>
    private static List<BRenderCommand.StrokeRoundedRect> Rings(BRenderList list, StandardTabView tabs)
    {
        BRect page = new(tabs.Bounds.Left, tabs.Bounds.Top + tabs.EffectiveHeaderHeight, tabs.Bounds.Width, tabs.Bounds.Height - tabs.EffectiveHeaderHeight);
        return [.. list.Commands.OfType<BRenderCommand.StrokeRoundedRect>().Where(stroke => stroke.Rect != page)];
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

    private static UiInputEvent MouseDown(double x, double y) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                new InputEventHeader(
                    InputDeviceId.FromOpaqueValue("mouse"),
                    new InputTimestamp(1, TimeSpan.TicksPerSecond, "tab-header-test"),
                    1),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

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
