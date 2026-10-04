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
            Assert.Equal(new BRect(header.Left + tabs.HeaderPaddingX, header.Bottom - 2, header.Width - (tabs.HeaderPaddingX * 2), 2), bar);
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
        tabs.SelectedIndicatorThickness = 2;
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
            AssertRingClearsTheLabelAndTheBar(tabs, header, ring, bar);

            // A focus indicator needs 3:1 against what it is drawn on.
            double ratio = StandardContrast.Ratio(ring.Color, tabs.SelectedHeaderBackground);
            Assert.True(ratio >= StandardContrast.AaLargeOrUi, $"{theme.Name}: the ring stands out at {ratio:0.00}:1.");
        }

        // The high-contrast presets draw it 2 DIP thick, thicker than their borders.
        Assert.Equal(theme.IsHighContrast ? 2 : 1, theme.FocusRingThickness);

        // Larger text makes the header taller; the ring still runs between the label and the bar.
        tabs.Font = tabs.Font with { Size = tabs.Font.Size * 1.5 };
        BRenderList larger = session.RenderFrame();
        Assert.True(tabs.EffectiveHeaderHeight > tabs.HeaderHeight);
        AssertRingClearsTheLabelAndTheBar(
            tabs,
            tabs.GetTabHeaderBounds(tabs.SelectedIndex),
            Assert.Single(Rings(larger, tabs)),
            Assert.Single(larger.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == tabs.SelectedIndicatorColor).Rect);
    }

    /// <summary>
    /// The ring's stroke lies inside the header, below the label's line and at least 1 DIP above the bar, so
    /// it crosses neither the label's descenders nor the mark of the selected tab.
    /// </summary>
    private static void AssertRingClearsTheLabelAndTheBar(StandardTabView tabs, BRect header, BRenderCommand.StrokeRoundedRect ring, BRect bar)
    {
        double half = ring.Thickness / 2;
        double lineBottom = LabelLine(tabs, header).Bottom;
        Assert.True(ring.Rect.Top - half >= header.Top && ring.Rect.Left - half >= header.Left && ring.Rect.Right + half <= header.Right, "The ring leaves the header.");
        Assert.True(ring.Rect.Bottom - half >= lineBottom, $"The ring at {ring.Rect.Bottom - half} crosses the label's line, which ends at {lineBottom}.");
        Assert.True(ring.Rect.Bottom + half + 1 <= bar.Top, $"The ring at {ring.Rect.Bottom + half} runs into the bar at {bar.Top}.");
    }

    [Theory]
    [InlineData(24)]
    [InlineData(28)]
    [InlineData(32)]
    [InlineData(40)]
    public void AtAnyHeaderHeightTheRingAndTheBarKeepOutOfTheLabel(double headerHeight)
    {
        // A 1 DIP ring and a high-contrast 2 DIP one.
        foreach (StandardThemeTokens theme in new[] { StandardThemeTokens.Light, StandardThemeTokens.HighContrastDark })
        {
            StandardTabView tabs = ThreeTabs();
            tabs.HeaderHeight = headerHeight;
            tabs.ApplyTheme(theme);
            using UiSession session = Attach(tabs, 400, 200);
            session.SetFocus(tabs);

            BRenderList frame = session.RenderFrame();
            BRect header = tabs.GetTabHeaderBounds(tabs.SelectedIndex);
            BRect bar = Assert.Single(frame.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == tabs.SelectedIndicatorColor).Rect;
            BRenderCommand.StrokeRoundedRect ring = Assert.Single(Rings(frame, tabs));
            (double lineTop, double lineBottom) = LabelLine(tabs, header);
            double half = ring.Thickness / 2;
            string at = $"{theme.Name} at a {headerHeight} DIP header";

            // Neither the bar nor the ring's strokes cross the label's line, and the ring stays in the header.
            Assert.True(bar.Top >= lineBottom, $"{at}: the bar at {bar.Top} covers the label's line, which ends at {lineBottom}.");
            Assert.True(ring.Rect.Top + half <= lineTop, $"{at}: the ring at {ring.Rect.Top + half} crosses the label's line, which starts at {lineTop}.");
            Assert.True(ring.Rect.Bottom - half >= lineBottom, $"{at}: the ring at {ring.Rect.Bottom - half} crosses the label's line, which ends at {lineBottom}.");
            Assert.True(ring.Rect.Top - half >= header.Top && ring.Rect.Bottom + half <= header.Bottom, $"{at}: the ring leaves the header.");

            // Where the header has the room, at the default height and above, the ring runs clear of the bar too.
            if (headerHeight >= 32)
                AssertRingClearsTheLabelAndTheBar(tabs, header, ring, bar);
        }
    }

    [Theory]
    [InlineData(30)]
    [InlineData(double.PositiveInfinity)]
    public void ABarThickerThanTheRoomUnderTheLabelStopsThereAndLeavesTheRingDrawn(double thickness)
    {
        StandardTabView tabs = ThreeTabs();
        tabs.ApplyTheme(StandardThemeTokens.Light);
        tabs.SelectedIndicatorThickness = thickness;
        using UiSession session = Attach(tabs, 400, 200);
        session.SetFocus(tabs);

        BRenderList frame = session.RenderFrame();
        BRect header = tabs.GetTabHeaderBounds(0);
        (double lineTop, double lineBottom) = LabelLine(tabs, header);

        // The bar fills the room under the label, not the label.
        BRect bar = Assert.Single(frame.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == tabs.SelectedIndicatorColor).Rect;
        Assert.Equal(lineBottom, bar.Top, 6);
        Assert.Equal(header.Bottom, bar.Bottom, 6);

        // The ring is still drawn, out of the label's line.
        BRenderCommand.StrokeRoundedRect ring = Assert.Single(Rings(frame, tabs));
        Assert.True(ring.Rect.Top + (ring.Thickness / 2) <= lineTop);
        Assert.True(ring.Rect.Bottom - (ring.Thickness / 2) >= lineBottom);
        Assert.Equal(thickness, tabs.SelectedIndicatorThickness);
    }

    [Fact]
    public void InAStripWiderThanTheViewTheRingAndTheBarStayInsideTheView()
    {
        int cutHeaders = 0, strips = 0;
        double offset = StandardThemeTokens.Light.FocusRingOffset;
        foreach (double width in new[] { 300.0, 200.0 })
        {
            // A document strip without pages, as an editor's: its headers are laid out past the view's edge.
            var tabs = new StandardTabView();
            for (int index = 0; index < 8; index++)
                tabs.AddTab($"document{index}", $"Document number {index}");
            tabs.ApplyTheme(StandardThemeTokens.Light);
            using UiSession session = Attach(tabs, width, 120);
            session.SetFocus(tabs);
            Assert.True(tabs.GetTabHeaderBounds(tabs.Tabs.Count - 1).Left > tabs.Bounds.Right);

            for (int selected = 0; selected < tabs.Tabs.Count; selected++)
            {
                tabs.SelectedIndex = selected;
                BRenderList frame = session.RenderFrame();
                BRect header = tabs.GetTabHeaderBounds(selected);
                BRect visible = header.Intersect(tabs.Bounds);
                string at = $"Tab {selected} in a {width} DIP view";

                // The ring is drawn whole inside the view, so a focused strip always shows it.
                BRenderCommand.StrokeRoundedRect ring = Assert.Single(Rings(frame, tabs));
                double half = ring.Thickness / 2;
                Assert.True(
                    ring.Rect.Left - half >= tabs.Bounds.Left && ring.Rect.Right + half <= tabs.Bounds.Right &&
                    ring.Rect.Top - half >= tabs.Bounds.Top && ring.Rect.Bottom + half <= tabs.Bounds.Bottom,
                    $"{at}: the ring {ring.Rect} leaves the view {tabs.Bounds}.");

                if (!visible.IsEmpty && visible.Width >= 48)
                {
                    // Around the part of the selected header that shows, ...
                    Assert.Equal(visible.Left + offset, ring.Rect.Left, 6);
                    Assert.Equal(visible.Right - offset, ring.Rect.Right, 6);
                    if (visible.Width < header.Width)
                        cutHeaders++;
                }
                else
                {
                    // ... or around the visible strip, when too little of it shows to read as a tab.
                    Assert.Equal(tabs.Bounds.Left + offset, ring.Rect.Left, 6);
                    Assert.Equal(tabs.Bounds.Right - offset, ring.Rect.Right, 6);
                    strips++;
                }

                // The bar is drawn only where it lies inside the view.
                foreach (BRenderCommand.FillRoundedRect bar in frame.Commands.OfType<BRenderCommand.FillRoundedRect>().Where(fill => fill.Color == tabs.SelectedIndicatorColor))
                    Assert.True(bar.Rect.Left >= tabs.Bounds.Left && bar.Rect.Right <= tabs.Bounds.Right, $"{at}: the bar {bar.Rect} leaves the view {tabs.Bounds}.");
            }
        }

        Assert.True(cutHeaders > 0, "No selected header was cut by the view's edge.");
        Assert.True(strips > 0, "No selected header lay past the view's edge.");
    }

    /// <summary>The label's line in a header: centered in the header's height.</summary>
    private static (double Top, double Bottom) LabelLine(StandardTabView tabs, BRect header)
    {
        double lineHeight = BTextMeasurer.GetLineHeight(tabs.Font);
        double top = header.Top + Math.Max(0, (tabs.EffectiveHeaderHeight - lineHeight) / 2);
        return (top, top + lineHeight);
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
