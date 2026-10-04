using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.Button.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;
using Broiler.UI.Toolbar.Standard;

namespace Broiler.UI.Toolbar.Tests;

/// <summary>
/// The command side of the state roles (ADR 0029): a hovered secondary button, a checked, indeterminate or
/// pressed toggle button and the open overflow chevron are drawn in the theme's state pair, and the presets
/// draw them as they always have.
/// </summary>
public sealed class StateFillControlTests
{
    private const string Chevron = "»";

    /// <summary>A palette whose state pair is neither the selection fill nor any text color, so each use shows.</summary>
    private static readonly StandardThemeTokens DistinctStates = StandardThemeTokens.Light with
    {
        Name = "DistinctStates",
        StateFill = BColor.FromArgb(0xFF, 0x30, 0x00, 0x60),
        StateText = BColor.FromArgb(0xFF, 0xFF, 0xF0, 0xA0),
    };

    private static readonly BColor Highlight = BColor.FromArgb(0xFF, 0x1A, 0xEB, 0xFF);
    private static readonly BColor HighlightText = BColor.FromArgb(0xFF, 0x00, 0x00, 0x00);

    /// <summary>
    /// The shape of a palette a host builds from a system contrast theme: the accent and the selection fill are
    /// the same highlight, and only the selection text is set to the text that reads on it.
    /// </summary>
    private static readonly StandardThemeTokens SystemHighlight = StandardThemeTokens.HighContrastDark with
    {
        Name = "HighContrastSystem",
        Surface = BColor.FromArgb(0xFF, 0x20, 0x20, 0x20),
        Text = BColor.White,
        TextMuted = BColor.White,
        Accent = Highlight,
        AccentSoft = Highlight,
        OnAccent = HighlightText,
        SelectionText = HighlightText,
    };

    public static TheoryData<StandardThemeTokens> Presets() => new()
    {
        StandardThemeTokens.Light,
        StandardThemeTokens.Dark,
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
    };

    // --- Secondary button --------------------------------------------------

    [Fact]
    public void A_Hovered_Secondary_Button_Is_Drawn_In_The_State_Pair()
    {
        StandardThemeTokens theme = DistinctStates;
        var button = new StandardButton { Text = "Hover" };
        button.ApplyTheme(theme);
        using var harness = new Harness(button, new BRect(10, 10, 80, 30));

        Assert.Equal((theme.Surface, theme.Text), Look(harness.Render(), button, "Hover"));

        harness.Move(Middle(button.Bounds));
        Assert.Equal((theme.StateFill, theme.StateText), Look(harness.Render(), button, "Hover"));
        Assert.Equal(theme.StateText, button.SecondaryHoverForeground);

        // Pressed is drawn on its own fill, which ordinary text reads on.
        harness.Press(Middle(button.Bounds));
        Assert.Equal((theme.SurfaceDisabled, theme.Text), Look(harness.Render(), button, "Hover"));

        harness.Release(Middle(button.Bounds));
        harness.Move(new BPoint(150, 70));
        Assert.Equal((theme.Surface, theme.Text), Look(harness.Render(), button, "Hover"));
    }

    [Fact]
    public void A_Hovered_Secondary_Button_Paints_Its_Icon_In_The_State_Text_And_A_Primary_Button_Is_Unchanged()
    {
        StandardThemeTokens theme = DistinctStates;
        BColor iconColor = BColor.Empty;
        var icon = new StandardButton { Text = "Icon", IconPainter = (_, _, color) => iconColor = color };
        icon.ApplyTheme(theme);
        using (var harness = new Harness(icon, new BRect(10, 10, 30, 30)))
        {
            harness.Move(Middle(icon.Bounds));
            harness.Render();
            Assert.Equal(theme.StateText, iconColor);
        }

        var primary = new StandardButton { Text = "OK", IsDefault = true };
        primary.ApplyTheme(theme);
        using (var harness = new Harness(primary, new BRect(10, 10, 80, 30)))
        {
            harness.Move(Middle(primary.Bounds));
            Assert.Equal((theme.AccentHover, theme.OnAccent), Look(harness.Render(), primary, "OK"));
        }
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void A_Preset_Draws_A_Hovered_Secondary_Button_As_Before(StandardThemeTokens theme)
    {
        var button = new StandardButton { Text = "Hover" };
        button.ApplyTheme(theme);
        using var harness = new Harness(button, new BRect(10, 10, 80, 30));

        harness.Move(Middle(button.Bounds));
        Assert.Equal((theme.AccentSoft, theme.Text), Look(harness.Render(), button, "Hover"));

        // A foreground the application sets after the theme still reaches the hovered label, as it always has.
        BColor custom = BColor.FromArgb(0xFF, 0x80, 0x10, 0x10);
        button.Foreground = custom;
        Assert.Equal((theme.AccentSoft, custom), Look(harness.Render(), button, "Hover"));
    }

    // --- Toggle button -----------------------------------------------------

    [Fact]
    public void A_Checked_Indeterminate_Or_Pressed_Toggle_Button_Is_Drawn_In_The_State_Pair()
    {
        StandardThemeTokens theme = DistinctStates;
        var toggle = new StandardToggleButton { Text = "Bold", IsThreeState = true };
        toggle.ApplyTheme(theme);
        using var harness = new Harness(toggle, new BRect(10, 10, 80, 30));

        // Off, the label is the accent on the surface, and on the hover fill.
        Assert.Equal((theme.Surface, theme.Accent), Look(harness.Render(), toggle, "Bold"));
        harness.Move(Middle(toggle.Bounds));
        Assert.Equal((theme.SurfaceAlt, theme.Accent), Look(harness.Render(), toggle, "Bold"));

        harness.Press(Middle(toggle.Bounds));
        Assert.Equal((theme.StateFill, theme.StateText), Look(harness.Render(), toggle, "Bold"));

        harness.Release(Middle(toggle.Bounds));
        Assert.Equal(UiToggleState.On, toggle.ToggleState);
        Assert.Equal((theme.StateFill, theme.StateText), Look(harness.Render(), toggle, "Bold"));

        toggle.ToggleState = UiToggleState.Indeterminate;
        Assert.Equal((theme.StateFill, theme.StateText), Look(harness.Render(), toggle, "Bold"));
        Assert.Equal(theme.StateText, toggle.CheckedForeground);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void A_Preset_Draws_A_Checked_Or_Pressed_Toggle_Button_As_Before(StandardThemeTokens theme)
    {
        var toggle = new StandardToggleButton { Text = "Bold", IsThreeState = true };
        toggle.ApplyTheme(theme);
        using var harness = new Harness(toggle, new BRect(10, 10, 80, 30));

        harness.Press(Middle(toggle.Bounds));
        Assert.Equal((theme.AccentSoft, theme.Accent), Look(harness.Render(), toggle, "Bold"));

        harness.Release(Middle(toggle.Bounds));
        Assert.Equal((theme.AccentSoft, theme.Accent), Look(harness.Render(), toggle, "Bold"));

        toggle.ToggleState = UiToggleState.Indeterminate;
        Assert.Equal((theme.AccentSoft, theme.Accent), Look(harness.Render(), toggle, "Bold"));

        BColor custom = BColor.FromArgb(0xFF, 0x80, 0x10, 0x10);
        toggle.Foreground = custom;
        Assert.Equal((theme.AccentSoft, custom), Look(harness.Render(), toggle, "Bold"));
    }

    // --- Toolbar overflow --------------------------------------------------

    [Fact]
    public void The_Open_Overflow_Chevron_Is_Drawn_In_The_State_Pair()
    {
        StandardThemeTokens theme = DistinctStates;
        (Harness harness, StandardToolbar toolbar) = OverflowingBar(theme);
        using (harness)
        {
            BRenderList shut = harness.Render();
            Assert.DoesNotContain(shut.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == toolbar.OverflowButtonBounds);
            Assert.Equal(theme.Text, TextColor(shut, Chevron));

            Assert.True(toolbar.OpenOverflow());
            Assert.Equal((theme.StateFill, theme.StateText), ChevronLook(harness.Render(), toolbar));
            Assert.Equal(theme.StateFill, toolbar.OverflowOpenBackground);
            Assert.Equal(theme.StateText, toolbar.OverflowOpenForeground);
        }
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void A_Preset_Draws_The_Open_Overflow_Chevron_As_Before(StandardThemeTokens theme)
    {
        (Harness harness, StandardToolbar toolbar) = OverflowingBar(theme);
        using (harness)
        {
            harness.Render();
            Assert.True(toolbar.OpenOverflow());
            Assert.Equal((theme.AccentSoft, theme.Text), ChevronLook(harness.Render(), toolbar));
        }
    }

    // --- A system highlight palette ----------------------------------------

    [Fact]
    public void Every_Command_State_Reads_On_A_System_Highlight()
    {
        StandardThemeTokens theme = SystemHighlight;
        (BColor, BColor) readable = (Highlight, HighlightText);

        var button = new StandardButton { Text = "Hover" };
        button.ApplyTheme(theme);
        using (var harness = new Harness(button, new BRect(10, 10, 80, 30)))
        {
            harness.Move(Middle(button.Bounds));
            Assert.Equal(readable, Look(harness.Render(), button, "Hover"));
        }

        var toggle = new StandardToggleButton { Text = "Bold" };
        toggle.ApplyTheme(theme);
        using (var harness = new Harness(toggle, new BRect(10, 10, 80, 30)))
        {
            // Off, the accent label is on the window color, where it reads.
            Assert.Equal((theme.Surface, Highlight), Look(harness.Render(), toggle, "Bold"));
            toggle.ToggleState = UiToggleState.On;
            Assert.Equal(readable, Look(harness.Render(), toggle, "Bold"));
        }

        (Harness bar, StandardToolbar toolbar) = OverflowingBar(theme);
        using (bar)
        {
            bar.Render();
            Assert.True(toolbar.OpenOverflow());
            Assert.Equal(readable, ChevronLook(bar.Render(), toolbar));
        }
    }

    // --- Harness -----------------------------------------------------------

    private static (Harness Harness, StandardToolbar Toolbar) OverflowingBar(StandardThemeTokens theme)
    {
        // Four 80-wide items in a bar with room for two of them.
        var toolbar = new StandardToolbar { Padding = 10, Spacing = 4 };
        for (int index = 0; index < 4; index++)
            toolbar.AddChild(new StandardButton { Text = "Item" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), PreferredSize = new BSize(80, 30) });
        toolbar.ApplyTheme(theme);
        return (new Harness(toolbar, new BRect(0, 0, 220, 44)), toolbar);
    }

    private static (BColor Fill, BColor Text) Look(BRenderList list, UiElement element, string text) =>
        (Assert.Single(list.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == element.Bounds).Color, TextColor(list, text));

    private static (BColor Fill, BColor Text) ChevronLook(BRenderList list, StandardToolbar toolbar) =>
        (Assert.Single(list.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == toolbar.OverflowButtonBounds).Color, TextColor(list, Chevron));

    private static BColor TextColor(BRenderList list, string text) =>
        Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == text).Text.Color;

    private static BPoint Middle(BRect rect) => new(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));

    /// <summary>A session showing one element in a fixed box, and the pointer to work it with.</summary>
    private sealed class Harness : System.IDisposable
    {
        private readonly UiSession _session;
        private readonly StandardInputRoute _route;
        private long _sequence;

        public Harness(UiElement element, BRect box)
        {
            _session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new TestHost(new BSize(320, 200)));
            _session.AddRoot(new FixedBox(element, box));
            _route = new StandardInputRoute(_session);
            _session.RenderFrame();
        }

        public BRenderList Render() => _session.RenderFrame();

        public void Move(BPoint point) =>
            _route.Dispatch(new MouseMoveEvent(Header(), InputPoint.ClientDeviceIndependentPixels(point.X, point.Y), MouseButtons.None, InputEventSource.Synthetic));

        public void Press(BPoint point) => Button(point, MouseButtonTransition.Down);

        public void Release(BPoint point) => Button(point, MouseButtonTransition.Up);

        public void Dispose() => _session.Dispose();

        private void Button(BPoint point, MouseButtonTransition transition) =>
            _route.Dispatch(new MouseButtonEvent(
                Header(),
                InputPoint.ClientDeviceIndependentPixels(point.X, point.Y),
                transition == MouseButtonTransition.Down ? MouseButtons.Left : MouseButtons.None,
                MouseButton.Left,
                transition,
                InputEventSource.Synthetic));

        private InputEventHeader Header()
        {
            _sequence++;
            return new InputEventHeader(
                InputDeviceId.FromOpaqueValue("mouse:state"),
                new InputTimestamp(_sequence, System.TimeSpan.TicksPerSecond, "state-fill"),
                _sequence);
        }
    }

    /// <summary>Arranges its one child into a fixed rectangle.</summary>
    private sealed class FixedBox : UiElement
    {
        private readonly UiElement _child;
        private readonly BRect _box;

        public FixedBox(UiElement child, BRect box)
        {
            _child = child;
            _box = box;
            AddChild(child);
        }

        protected override BSize MeasureCore(BSize availableSize)
        {
            _child.Measure(new BSize(_box.Width, _box.Height));
            return new BSize(availableSize.Width, availableSize.Height);
        }

        protected override void ArrangeCore(BRect finalRect) => _child.Arrange(_box);
    }

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize { get; } = viewportSize;

        public double Scale => 1;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
