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
/// it has something to scroll and nothing else inside it can take focus. ADR 0028.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class ScrollViewFocusTests
{
    [Fact]
    public void AFocusedScrollViewDrawsTheThemeRingOverItsScrollbars()
    {
        var scroll = new StandardScrollView { ScrollbarThickness = 10 };
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
    public void PointerFocusDrawsNoRingUntilTheKeyboardIsUsed()
    {
        var scroll = new StandardScrollView { ScrollbarThickness = 10 };
        scroll.AddChild(new Fixed(new BSize(80, 300)));
        using UiSession session = Attach(scroll, 100, 100);

        session.DispatchInput(MouseDown(40, 40));
        Assert.Same(scroll, session.FocusedElement);
        Assert.Empty(Rings(session.RenderFrame(), scroll));

        Assert.True(session.DispatchInput(Key(BVirtualKey.Down)));
        Assert.Single(Rings(session.RenderFrame(), scroll));
    }

    [Fact]
    public void ApplyThemeFollowsTheRingButKeepsScrollbarColors()
    {
        var scroll = new StandardScrollView();
        BColor track = scroll.ScrollbarTrack;
        BColor thumb = scroll.ScrollbarThumb;
        StandardThemeTokens contrast = StandardThemeTokens.HighContrastDark;

        Assert.IsAssignableFrom<IStandardThemedControl>(scroll);
        scroll.ApplyTheme(contrast);

        Assert.Equal(contrast.FocusRing, scroll.FocusRing);
        Assert.Equal(track, scroll.ScrollbarTrack);
        Assert.Equal(thumb, scroll.ScrollbarThumb);

        scroll.AddChild(new Fixed(new BSize(80, 300)));
        using UiSession session = Attach(scroll, 100, 100);
        scroll.ApplyTheme(contrast);
        session.SetFocus(scroll);
        BRenderCommand.StrokeRect ring = Assert.Single(Rings(session.RenderFrame(), scroll));
        Assert.Equal(contrast.FocusRingThickness, ring.Thickness);
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

    private sealed class Host(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize => viewportSize;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
