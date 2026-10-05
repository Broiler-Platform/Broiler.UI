using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Windowing;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.ScrollView;
using Broiler.UI.ScrollView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// A focused scroll view shows where the keyboard is, and can be a keyboard stop of its own while
/// it has something to scroll and nothing else inside it can take focus (ADR 0028). A stop that
/// stops being one while it has focus hands focus on (ADR 0032).
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class ScrollViewFocusTests
{
    [Fact]
    public void AFocusedScrollViewDrawsTheThemeRingOverItsScrollbars()
    {
        var scroll = new StandardScrollView { ScrollbarThickness = 10, FocusWhenScrollable = true };
        scroll.AddChild(new Fixed(new BSize(140, 180)));
        using UiSession session = Attach(scroll, 100, 100);

        Assert.Empty(Rings(session.RenderFrame(), scroll));

        session.SetFocus(scroll);
        BRenderList frame = session.RenderFrame();

        StandardThemeTokens theme = StandardControlPaint.Theme;
        BRenderCommand.StrokeRect ring = Assert.Single(Rings(frame, scroll));
        Assert.Equal(StandardControlPaint.Inset(scroll.Bounds, theme.FocusRingOffset), ring.Rect);
        Assert.Equal(theme.FocusRingThickness, ring.Thickness);
        int thumb = frame.Commands.ToList().FindLastIndex(command => command is BRenderCommand.FillRoundedRect fill && fill.Color == scroll.ScrollbarThumb);
        Assert.True(thumb >= 0);
        Assert.True(frame.Commands.ToList().IndexOf(ring) > thumb);
    }

    [Fact]
    public void AScrollViewThatIsNoKeyboardStopNeverDrawsARing()
    {
        // The default: a click on blank space focuses the scroll view behind it.
        var scroll = new StandardScrollView { ScrollbarThickness = 10 };
        scroll.AddChild(new Fixed(new BSize(80, 300)));
        using UiSession session = Attach(scroll, 100, 100);
        Assert.False(scroll.Focusable);
        Assert.False(scroll.CanFocus);

        session.DispatchInput(MouseDown(40, 40));
        Assert.Same(scroll, session.FocusedElement);
        Assert.Empty(Rings(session.RenderFrame(), scroll));

        // A key the view does not handle, such as Alt for the menu mnemonics, and one it does.
        Assert.False(session.DispatchInput(Key(18))); // VK_MENU
        Assert.True(session.IsFocusVisible);
        Assert.Empty(Rings(session.RenderFrame(), scroll));
        Assert.True(session.DispatchInput(Key(BVirtualKey.Down)));
        Assert.Empty(Rings(session.RenderFrame(), scroll));
    }

    [Fact]
    public void AKeyboardStopKeepsItsRingWhateverTheLastInput()
    {
        var scroll = new StandardScrollView { ScrollbarThickness = 10, FocusWhenScrollable = true };
        scroll.AddChild(new Fixed(new BSize(80, 300)));
        using UiSession session = Attach(scroll, 100, 100);
        Assert.True(scroll.CanFocus);

        // Focused by a click, as an editor is.
        session.DispatchInput(MouseDown(40, 40));
        Assert.Same(scroll, session.FocusedElement);
        Assert.False(session.IsFocusVisible);
        Assert.Single(Rings(session.RenderFrame(), scroll));

        // Reached by Tab, then the mouse moves: the ring stays where the keys go.
        session.SetFocus(null);
        Assert.Empty(Rings(session.RenderFrame(), scroll));
        Assert.True(new StandardFocusScope(session).MoveFocus(1));
        Assert.Same(scroll, session.FocusedElement);
        session.DispatchInput(MouseMove(60, 60));
        Assert.False(session.IsFocusVisible);
        Assert.Single(Rings(session.RenderFrame(), scroll));
    }

    [Fact]
    public void ApplyThemeFollowsTheRingAndTheScrollbarRolesOfAHighContrastTheme()
    {
        var scroll = new StandardScrollView { FocusWhenScrollable = true };
        BColor track = scroll.ScrollbarTrack;
        BColor thumb = scroll.ScrollbarThumb;
        StandardThemeTokens contrast = StandardThemeTokens.HighContrastDark;

        Assert.IsAssignableFrom<IStandardThemedControl>(scroll);
        scroll.ApplyTheme(StandardThemeTokens.Dark);
        Assert.Equal(StandardThemeTokens.Dark.FocusRing, scroll.FocusRing);
        Assert.Equal(track, scroll.ScrollbarTrack);
        Assert.Equal(thumb, scroll.ScrollbarThumb);

        // High contrast gives the bars its scrollbar roles (ADR 0033); the translucent defaults did not reach 3:1.
        scroll.ApplyTheme(contrast);

        Assert.Equal(contrast.FocusRing, scroll.FocusRing);
        Assert.Equal(contrast.ScrollbarTrack, scroll.ScrollbarTrack);
        Assert.Equal(contrast.ScrollbarThumb, scroll.ScrollbarThumb);

        scroll.AddChild(new Fixed(new BSize(80, 300)));
        using UiSession session = Attach(scroll, 100, 100);
        scroll.ApplyTheme(contrast);
        session.SetFocus(scroll);
        BRenderCommand.StrokeRect ring = Assert.Single(Rings(session.RenderFrame(), scroll));
        Assert.Equal(contrast.FocusRingThickness, ring.Thickness);
    }

    [Fact]
    public void ApplyThemeKeepsAScrollbarColorTheApplicationSet()
    {
        BColor thumb = BColor.FromArgb(0xFF, 0x6A, 0x1B, 0x9A);
        var scroll = new StandardScrollView { ScrollbarThumb = thumb };
        BColor track = scroll.ScrollbarTrack;

        // Light, then a contrast theme, then Light again: the track the application left alone follows each theme.
        scroll.ApplyTheme(StandardThemeTokens.Light);
        Assert.Equal((track, thumb), (scroll.ScrollbarTrack, scroll.ScrollbarThumb));
        scroll.ApplyTheme(StandardThemeTokens.HighContrastDark);
        Assert.Equal((StandardThemeTokens.HighContrastDark.ScrollbarTrack, thumb), (scroll.ScrollbarTrack, scroll.ScrollbarThumb));
        scroll.ApplyTheme(StandardThemeTokens.Light);
        Assert.Equal((track, thumb), (scroll.ScrollbarTrack, scroll.ScrollbarThumb));

        // A color set between themes is kept by the next one as well.
        scroll.ScrollbarTrack = BColor.White;
        scroll.ApplyTheme(StandardThemeTokens.HighContrastDark);
        Assert.Equal((BColor.White, thumb), (scroll.ScrollbarTrack, scroll.ScrollbarThumb));
    }

    [Theory]
    [InlineData(nameof(StandardThemeTokens.HighContrastLight))]
    [InlineData(nameof(StandardThemeTokens.HighContrastDark))]
    public void InHighContrastTheRingStaysWholeWhereItCrossesTheThumb(string name)
    {
        StandardThemeTokens contrast = name == nameof(StandardThemeTokens.HighContrastLight)
            ? StandardThemeTokens.HighContrastLight
            : StandardThemeTokens.HighContrastDark;
        var scroll = new StandardScrollView { ScrollbarThickness = 10, FocusWhenScrollable = true };
        scroll.AddChild(new Fixed(new BSize(80, 300)));
        using UiSession session = Attach(scroll, 100, 100);
        scroll.ApplyTheme(contrast);
        session.SetFocus(scroll);

        BRenderCommand[] commands = session.RenderFrame().Commands.ToArray();
        BRenderCommand.StrokeRect ring = Assert.Single(commands.OfType<BRenderCommand.StrokeRect>(), command => command.Color == scroll.FocusRing);
        BRenderCommand.FillRoundedRect thumb = commands.OfType<BRenderCommand.FillRoundedRect>().Last(fill => fill.Color == scroll.ScrollbarThumb);

        // The ring's right side runs through the bar, and the thumb is drawn in a color the ring is lost on.
        Assert.InRange(ring.Rect.Right, thumb.Rect.Left, thumb.Rect.Right);
        Assert.True(StandardContrast.Ratio(ring.Color, thumb.Color) < StandardContrast.AaLargeOrUi);

        // So the stretch over the thumb is drawn again after the ring, clipped to the thumb, in a color that stands out on it.
        BRenderCommand[] after = commands.Skip(Array.IndexOf(commands, ring) + 1).ToArray();
        BRenderCommand.PushClip clip = Assert.IsType<BRenderCommand.PushClip>(after[0]);
        BRenderCommand.StrokeRect across = Assert.IsType<BRenderCommand.StrokeRect>(after[1]);
        Assert.IsType<BRenderCommand.PopClip>(after[2]);
        Assert.Equal(thumb.Rect, clip.Rect);
        Assert.Equal(ring.Rect, across.Rect);
        Assert.Equal(ring.Thickness, across.Thickness);
        Assert.True(StandardContrast.Ratio(across.Color, thumb.Color) >= StandardContrast.AaLargeOrUi);

        // The translucent thumb of the Light preset leaves the ring as it was: drawn once.
        scroll.ApplyTheme(StandardThemeTokens.Light);
        Assert.Single(session.RenderFrame().Commands.OfType<BRenderCommand.StrokeRect>(), command => command.Rect == ring.Rect);
    }

    [Fact]
    public void FocusWhenScrollableMakesAStopOnlyWhileItScrollsWithNothingFocusableInside()
    {
        var content = new Fixed(new BSize(80, 300));
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth };
        scroll.AddChild(content);
        using UiSession session = Attach(scroll, 100, 100);
        var focus = new StandardFocusScope(session);

        // Off by default: the current behavior.
        Assert.False(scroll.CanFocus);
        Assert.False(focus.MoveFocus(1));

        scroll.FocusWhenScrollable = true;
        Assert.True(scroll.CanFocus);
        Assert.True(focus.MoveFocus(1));
        Assert.Same(scroll, session.FocusedElement);

        // Nothing to scroll: no stop.
        content.Desired = new BSize(80, 60);
        session.RenderFrame();
        Assert.False(scroll.CanFocus);

        // Something inside that takes focus: that is the stop instead.
        content.Desired = new BSize(80, 300);
        var button = new StandardButton { Text = "Retry" };
        content.AddChild(button);
        session.RenderFrame();
        Assert.False(scroll.CanFocus);
        button.Visibility = UiVisibility.Collapsed;
        Assert.True(scroll.CanFocus);

        scroll.Visibility = UiVisibility.Collapsed;
        Assert.False(scroll.CanFocus);
    }

    [Fact]
    public void AFocusedScrollViewScrollsWithTheKeyboard()
    {
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(new Fixed(new BSize(80, 1000)));
        using UiSession session = Attach(scroll, 100, 100);
        Assert.True(scroll.Focus());

        Assert.True(session.DispatchInput(Key(BVirtualKey.Down)));
        Assert.Equal(scroll.LineScrollAmount, scroll.VerticalOffset);
        Assert.True(session.DispatchInput(Key(BVirtualKey.PageDown)));
        Assert.Equal(scroll.LineScrollAmount + (scroll.ViewportSize.Height * scroll.PageScrollFraction), scroll.VerticalOffset, 6);
        Assert.True(session.DispatchInput(Key(BVirtualKey.End)));
        Assert.Equal(scroll.ExtentSize.Height - scroll.ViewportSize.Height, scroll.VerticalOffset, 6);
        Assert.True(session.DispatchInput(Key(BVirtualKey.Home)));
        Assert.Equal(0, scroll.VerticalOffset);
    }

    [Theory]
    [InlineData("content shrinks")]
    [InlineData("viewport grows")]
    public void AStopThatStopsScrollingWhileFocusedHandsFocusToTheNextStop(string change)
    {
        var content = new Fixed(new BSize(80, 300));
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(content);
        var before = new StandardButton { Text = "Before" };
        var after = new StandardButton { Text = "After" };
        var column = new Column().Add(before, 30).Add(scroll, 100).Add(after, 30);
        using UiSession session = Attach(column, 200, 600);
        Assert.True(scroll.CanFocus);
        session.SetFocus(scroll);
        Assert.Single(Rings(session.RenderFrame(), scroll));

        if (change == "content shrinks")
            content.Desired = new BSize(80, 60);
        else
            column.SetHeight(scroll, 400);
        BRenderList frame = session.RenderFrame();

        Assert.False(scroll.CanFocus);
        Assert.Same(after, session.FocusedElement);
        Assert.Empty(Rings(frame, scroll));
    }

    [Fact]
    public void AStopWithNoStopAfterItHandsFocusToThePreviousOne()
    {
        var content = new Fixed(new BSize(80, 300));
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(content);
        var before = new StandardButton { Text = "Before" };
        var hidden = new StandardButton { Text = "Hidden", Visibility = UiVisibility.Collapsed };
        using UiSession session = Attach(new Column().Add(before, 30).Add(scroll, 100).Add(hidden, 30), 200, 600);
        session.SetFocus(scroll);

        content.Desired = new BSize(80, 60);
        session.RenderFrame();

        Assert.Same(before, session.FocusedElement);
    }

    [Fact]
    public void AStopWhoseContentGainsAControlHandsFocusToIt()
    {
        var content = new Fixed(new BSize(80, 300));
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(content);
        var after = new StandardButton { Text = "After" };
        using UiSession session = Attach(new Column().Add(scroll, 100).Add(after, 30), 200, 600);
        session.SetFocus(scroll);

        // The view still scrolls, but the control inside it is the stop now, and it comes next in Tab order.
        var retry = new StandardButton { Text = "Retry" };
        content.AddChild(retry);
        session.RenderFrame();

        Assert.Same(retry, session.FocusedElement);
    }

    [Fact]
    public void AStopThatStopsScrollingTakesNoFocusFromElsewhere()
    {
        var content = new Fixed(new BSize(80, 300));
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(content);
        var before = new StandardButton { Text = "Before" };
        var after = new StandardButton { Text = "After" };
        using UiSession session = Attach(new Column().Add(before, 30).Add(scroll, 100).Add(after, 30), 200, 600);

        session.SetFocus(before);
        content.Desired = new BSize(80, 60);
        session.RenderFrame();
        Assert.Same(before, session.FocusedElement);

        // A view that was never a stop keeps the focus a click on its blank space gave it.
        session.DispatchInput(MouseDown(100, 80));
        Assert.Same(scroll, session.FocusedElement);
        content.Desired = new BSize(80, 70);
        session.RenderFrame();
        Assert.Same(scroll, session.FocusedElement);
    }

    [Fact]
    public void AStopHiddenWhileFocusedIsLeftToWhatHidItUntilItIsShownAsNoStop()
    {
        var content = new Fixed(new BSize(80, 300));
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(content);
        var after = new StandardButton { Text = "After" };
        using UiSession session = Attach(new Column().Add(scroll, 100).Add(after, 30), 200, 600);
        session.SetFocus(scroll);

        scroll.Visibility = UiVisibility.Collapsed;
        session.RenderFrame();
        Assert.Same(scroll, session.FocusedElement);

        // Its content shrinks while it is hidden, and it is shown again with nothing to scroll.
        content.Desired = new BSize(80, 60);
        session.RenderFrame();
        Assert.Same(scroll, session.FocusedElement);
        scroll.Visibility = UiVisibility.Visible;
        BRenderList frame = session.RenderFrame();

        Assert.False(scroll.CanFocus);
        Assert.Same(after, session.FocusedElement);
        Assert.Empty(Rings(frame, scroll));
    }

    [Theory]
    [InlineData("a control inside is enabled")]
    [InlineData("a control inside is made focusable")]
    public void AStopWhoseContentGainsAControlWithoutALayoutHandsFocusToIt(string change)
    {
        // Neither change measures or arranges anything: only the next frame that draws the view sees it.
        var retry = new StandardButton { Text = "Retry", IsEnabled = false };
        var link = new Fixed(new BSize(80, 20));
        var content = new Fixed(new BSize(80, 300));
        content.AddChild(change == "a control inside is enabled" ? retry : link);
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(content);
        var after = new StandardButton { Text = "After" };
        using UiSession session = Attach(new Column().Add(scroll, 100).Add(after, 30), 200, 600);
        Assert.True(scroll.CanFocus);
        session.SetFocus(scroll);
        Assert.Single(Rings(session.RenderFrame(), scroll));

        if (change == "a control inside is enabled")
            retry.IsEnabled = true;
        else
            link.Focusable = true;
        BRenderList frame = session.RenderFrame();

        Assert.False(scroll.CanFocus);
        Assert.Same(change == "a control inside is enabled" ? retry : link, session.FocusedElement);
        Assert.Empty(Rings(frame, scroll));
    }

    [Fact]
    public void AStopWhoseFocusWhenScrollableIsTurnedOffWhileFocusedHandsFocusOn()
    {
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(new Fixed(new BSize(80, 300)));
        var before = new StandardButton { Text = "Before" };
        var after = new StandardButton { Text = "After" };
        using UiSession session = Attach(new Column().Add(before, 30).Add(scroll, 100).Add(after, 30), 200, 600);
        session.SetFocus(scroll);
        Assert.Single(Rings(session.RenderFrame(), scroll));

        scroll.FocusWhenScrollable = false;
        BRenderList frame = session.RenderFrame();

        Assert.False(scroll.CanFocus);
        Assert.Same(after, session.FocusedElement);
        Assert.Empty(Rings(frame, scroll));

        // A view that is focusable of its own keeps focus: it is still a stop.
        scroll.FocusWhenScrollable = true;
        scroll.Focusable = true;
        session.SetFocus(scroll);
        session.RenderFrame();
        scroll.FocusWhenScrollable = false;
        session.RenderFrame();
        Assert.Same(scroll, session.FocusedElement);
    }

    [Fact]
    public void HandingFocusOnRevealsAStopNoneOfWhichShowsAndScrollsNothingElse()
    {
        // The status area, a list scrolled part of the way, and a form whose first control is below its fold.
        var status = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        var statusContent = new Fixed(new BSize(80, 300));
        status.AddChild(statusContent);
        var list = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth };
        list.AddChild(new Fixed(new BSize(80, 400)));
        var form = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth };
        var save = new StandardButton { Text = "Save" };
        form.AddChild(new Column().Add(new Fixed(new BSize(80, 150)), 150).Add(save, 30));
        using UiSession session = Attach(new Column().Add(status, 100).Add(list, 100).Add(form, 100), 200, 600);
        Assert.True(list.ScrollBy(0, 50));
        session.RenderFrame();
        session.SetFocus(status);
        Assert.True(save.GetVisibleBounds().IsEmpty);

        statusContent.Desired = new BSize(80, 60);
        session.RenderFrame();
        session.RenderFrame();

        // The form scrolls just far enough to show Save whole, as Tab would; nothing else moves.
        Assert.Same(save, session.FocusedElement);
        Assert.Equal(save.Bounds, save.GetVisibleBounds());
        Assert.Equal(80, form.VerticalOffset);
        Assert.Equal(0, status.VerticalOffset);
        Assert.Equal(50, list.VerticalOffset);
    }

    [Theory]
    [InlineData(40, 0)]  // Save shows in part: its ring shows where focus went, so nothing scrolls.
    [InlineData(60, 40)] // Save cannot be seen: the form that holds the status area too scrolls to show it.
    public void HandingFocusOnScrollsOnlyWhenNoneOfTheNewFocusShows(double gap, double formOffset)
    {
        // A form 150 tall whose status area is followed, after a gap, by Save.
        var status = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        var statusContent = new Fixed(new BSize(80, 300));
        status.AddChild(statusContent);
        var save = new StandardButton { Text = "Save" };
        var form = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth };
        form.AddChild(new Column().Add(status, 100).Add(new Fixed(new BSize(80, gap)), gap).Add(save, 30));
        using UiSession session = Attach(new Column().Add(form, 150), 200, 600);
        session.SetFocus(status);

        statusContent.Desired = new BSize(80, 60);
        session.RenderFrame();
        session.RenderFrame();

        Assert.Same(save, session.FocusedElement);
        Assert.False(save.GetVisibleBounds().IsEmpty);
        Assert.Equal(formOffset, form.VerticalOffset);
    }

    [Fact]
    public void FocusMovesOnAfterTheFrameAndOnlyWhileTheViewStillHasItAndStillDoesNotScroll()
    {
        var content = new Fixed(new BSize(80, 300));
        var scroll = new StandardScrollView { ScrollbarThickness = 10, Constraint = UiScrollConstraint.ConstrainWidth, FocusWhenScrollable = true };
        scroll.AddChild(content);
        var before = new StandardButton { Text = "Before" };
        var after = new StandardButton { Text = "After" };
        var dispatcher = new StandardQueuedUiDispatcher();
        UiSession session = new StandardUiSessionBuilder().WithDispatcher(dispatcher).Build(new Host(new BSize(200, 600)));
        using (session)
        {
            session.AddRoot(new Column().Add(before, 30).Add(scroll, 100).Add(after, 30));
            session.RenderFrame();

            // Posted, so the frame that ended the stop is over before focus moves.
            session.SetFocus(scroll);
            content.Desired = new BSize(80, 60);
            session.RenderFrame();
            Assert.Same(scroll, session.FocusedElement);
            Assert.True(dispatcher.HasPendingWork);
            dispatcher.Drain();
            Assert.Same(after, session.FocusedElement);

            // The user moved focus before the post ran: it stays where they put it.
            content.Desired = new BSize(80, 300);
            session.RenderFrame();
            session.SetFocus(scroll);
            content.Desired = new BSize(80, 60);
            session.RenderFrame();
            session.SetFocus(before);
            dispatcher.Drain();
            Assert.Same(before, session.FocusedElement);

            // The view scrolls again before the post ran: it keeps focus.
            content.Desired = new BSize(80, 300);
            session.RenderFrame();
            session.SetFocus(scroll);
            content.Desired = new BSize(80, 60);
            session.RenderFrame();
            content.Desired = new BSize(80, 300);
            session.RenderFrame();
            dispatcher.Drain();
            Assert.Same(scroll, session.FocusedElement);
            Assert.False(dispatcher.HasPendingWork);
        }
    }

    private static IEnumerable<BRenderCommand.StrokeRect> Rings(BRenderList frame, StandardScrollView scroll) =>
        frame.Commands.OfType<BRenderCommand.StrokeRect>().Where(command => command.Color == scroll.FocusRing);

    private static UiSession Attach(UiElement root, double width, double height)
    {
        UiSession session = new StandardUiSessionBuilder().Build(new Host(new BSize(width, height)));
        session.AddRoot(root);
        session.RenderFrame();
        return session;
    }

#pragma warning disable CS0618
    private static UiInputEvent Key(int virtualKey) =>
        new StandardLegacyGraphicsInputAdapter("scroll-focus").FromKey(new BKeyEventArgs(virtualKey, false, false, false), KeyboardKeyTransition.Down);
#pragma warning restore CS0618

    private static UiInputEvent MouseMove(double x, double y) =>
        UiInputEvent.FromMouseMove(
            new MouseMoveEvent(
                new InputEventHeader(
                    InputDeviceId.FromOpaqueValue("mouse"),
                    new InputTimestamp(2, TimeSpan.TicksPerSecond, "scroll-focus-test"),
                    2),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.None,
                InputEventSource.Synthetic));

    private static UiInputEvent MouseDown(double x, double y) =>
        UiInputEvent.FromMouseButton(
            new MouseButtonEvent(
                new InputEventHeader(
                    InputDeviceId.FromOpaqueValue("mouse"),
                    new InputTimestamp(1, TimeSpan.TicksPerSecond, "scroll-focus-test"),
                    1),
                InputPoint.ClientDeviceIndependentPixels(x, y),
                MouseButtons.Left,
                MouseButton.Left,
                MouseButtonTransition.Down,
                InputEventSource.Synthetic));

    private sealed class Fixed(BSize desired) : UiElement
    {
        public BSize Desired
        {
            get => desired;
            set
            {
                desired = value;
                InvalidateMeasure();
            }
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            foreach (UiElement child in Children)
                child.Measure(new BSize(80, 32));
            return desired;
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            foreach (UiElement child in Children)
                child.Arrange(new BRect(finalRect.Left, finalRect.Top, 80, 32));
        }
    }

    /// <summary>Stacks its children top to bottom, each as tall as it was added with.</summary>
    private sealed class Column : UiElement
    {
        private readonly List<double> _heights = [];

        public Column Add(UiElement child, double height)
        {
            AddChild(child);
            _heights.Add(height);
            return this;
        }

        public void SetHeight(UiElement child, double height)
        {
            _heights[Children.ToList().IndexOf(child)] = height;
            InvalidateMeasure();
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            double width = double.IsFinite(availableSize.Width) ? availableSize.Width : 100;
            for (int index = 0; index < Children.Count; index++)
                Children[index].Measure(new BSize(width, _heights[index]));
            return new BSize(width, _heights.Sum());
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            double top = finalRect.Top;
            for (int index = 0; index < Children.Count; index++)
            {
                Children[index].Arrange(new BRect(finalRect.Left, top, finalRect.Width, _heights[index]));
                top += _heights[index];
            }
        }
    }

    private sealed class Host(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize => viewportSize;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
