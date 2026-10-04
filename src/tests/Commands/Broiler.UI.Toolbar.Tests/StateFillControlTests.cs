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

    public static TheoryData<StandardThemeTokens> Presets() => new()
    {
        StandardThemeTokens.Light,
        StandardThemeTokens.Dark,
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
    };

    /// <summary>
    /// Palettes built as a host builds one from a system contrast theme: the four Windows 11 contrast themes, and
    /// a custom one whose selected text is its window text, a pairing Windows lets a user choose.
    /// </summary>
    public static TheoryData<StandardThemeTokens> SystemPalettes() => new()
    {
        SystemPalette("Aquatic", 0x202020, 0xFFFFFF, 0x8EE3F0, 0x263B50),
        SystemPalette("Desert", 0xFFFAEF, 0x3D3D3D, 0x903909, 0xFFF5E3),
        SystemPalette("Dusk", 0x2D3236, 0xFFFFFF, 0xA1BFDE, 0x212D3B),
        SystemPalette("NightSky", 0x000000, 0xFFFFFF, 0xD6B4FD, 0x2B2B2B),
        SystemPalette("SameSelectedText", 0x000000, 0xFFFFFF, 0x0000A0, 0xFFFFFF),
    };

    /// <summary>
    /// The mapping a host makes from the system colors: the window pair on the surfaces and every text role, and
    /// the highlight pair on the accent, the selection fill and the selection text.
    /// </summary>
    internal static StandardThemeTokens SystemPalette(string name, uint window, uint windowText, uint highlight, uint highlightText)
    {
        BColor surface = Rgb(window), text = Rgb(windowText), fill = Rgb(highlight), onFill = Rgb(highlightText);
        bool dark = StandardContrast.RelativeLuminance(surface) < StandardContrast.RelativeLuminance(text);
        return (dark ? StandardThemeTokens.HighContrastDark : StandardThemeTokens.HighContrastLight) with
        {
            Name = name,
            Surface = surface,
            SurfaceAlt = surface,
            SurfaceDisabled = surface,
            Text = text,
            TextMuted = text,
            Accent = fill,
            AccentHover = fill,
            AccentPressed = fill,
            AccentSoft = fill,
            OnAccent = onFill,
            SelectionText = onFill,
            SelectionTextMuted = onFill,
        };

        static BColor Rgb(uint rgb) => BColor.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

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

    [Fact]
    public void A_Toggle_Button_Whose_Accent_Is_The_State_Fill_Draws_Its_Label_In_The_State_Text()
    {
        // A system palette maps the accent and the state fill to one highlight. Here the selected text is the
        // window text, so the state text is the theme's text color, and the accent label would be the fill itself.
        StandardThemeTokens theme = SystemPalette("SameSelectedText", 0x000000, 0xFFFFFF, 0x0000A0, 0xFFFFFF);
        Assert.Equal(theme.Text, theme.StateText);
        Assert.Equal(theme.Accent, theme.StateFill);

        var toggle = new StandardToggleButton { Text = "Bold", IsThreeState = true };
        toggle.ApplyTheme(theme);
        using var harness = new Harness(toggle, new BRect(10, 10, 80, 30));
        (BColor, BColor) readable = (theme.StateFill, theme.StateText);

        harness.Press(Middle(toggle.Bounds));
        Assert.Equal(readable, Look(harness.Render(), toggle, "Bold"));
        harness.Release(Middle(toggle.Bounds));
        Assert.Equal(readable, Look(harness.Render(), toggle, "Bold"));
        toggle.ToggleState = UiToggleState.Indeterminate;
        Assert.Equal(readable, Look(harness.Render(), toggle, "Bold"));
        Assert.Equal(theme.StateText, toggle.CheckedForeground);

        // Off, the accent is drawn on the window color, not on itself, and is kept.
        toggle.ToggleState = UiToggleState.Off;
        harness.Move(new BPoint(150, 70));
        Assert.Equal((theme.Surface, theme.Accent), Look(harness.Render(), toggle, "Bold"));
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

    [Theory]
    [MemberData(nameof(SystemPalettes))]
    public void Every_Command_State_Reads_In_A_System_Contrast_Theme(StandardThemeTokens theme)
    {
        // Every state is drawn in the highlight pair, which the system chose to be read together.
        (BColor Fill, BColor Text) highlight = (theme.AccentSoft, theme.SelectionText);

        var button = new StandardButton { Text = "Hover" };
        button.ApplyTheme(theme);
        using (var harness = new Harness(button, new BRect(10, 10, 80, 30)))
        {
            harness.Move(Middle(button.Bounds));
            AssertReadable(highlight, Look(harness.Render(), button, "Hover"));
        }

        var toggle = new StandardToggleButton { Text = "Bold", IsThreeState = true };
        toggle.ApplyTheme(theme);
        using (var harness = new Harness(toggle, new BRect(10, 10, 80, 30)))
        {
            // Off, the accent label is on the window color, where it reads.
            Assert.Equal((theme.Surface, theme.Accent), Look(harness.Render(), toggle, "Bold"));
            harness.Press(Middle(toggle.Bounds));
            AssertReadable(highlight, Look(harness.Render(), toggle, "Bold"));
            harness.Release(Middle(toggle.Bounds));
            AssertReadable(highlight, Look(harness.Render(), toggle, "Bold"));
            toggle.ToggleState = UiToggleState.Indeterminate;
            AssertReadable(highlight, Look(harness.Render(), toggle, "Bold"));
        }

        (Harness bar, StandardToolbar toolbar) = OverflowingBar(theme);
        using (bar)
        {
            bar.Render();
            Assert.True(toolbar.OpenOverflow());
            AssertReadable(highlight, ChevronLook(bar.Render(), toolbar));
        }
    }

    /// <summary>The drawn fill and label are the expected pair, and the label reads on the fill (WCAG AA).</summary>
    internal static void AssertReadable((BColor Fill, BColor Text) expected, (BColor Fill, BColor Text) drawn)
    {
        Assert.Equal(expected, drawn);
        double ratio = StandardContrast.Ratio(drawn.Text, drawn.Fill);
        Assert.True(ratio >= StandardContrast.AaNormalText, $"{drawn.Text} on {drawn.Fill} is {ratio:0.00}:1.");
    }

    // --- Harness -----------------------------------------------------------

    /// <summary>An overflowing bar, themed with <paramref name="theme"/>, or left as built when that is null.</summary>
    internal static (Harness Harness, StandardToolbar Toolbar) OverflowingBar(StandardThemeTokens? theme)
    {
        // Four 80-wide items in a bar with room for two of them.
        var toolbar = new StandardToolbar { Padding = 10, Spacing = 4 };
        for (int index = 0; index < 4; index++)
            toolbar.AddChild(new StandardButton { Text = "Item" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), PreferredSize = new BSize(80, 30) });
        if (theme is not null)
            toolbar.ApplyTheme(theme);
        return (new Harness(toolbar, new BRect(0, 0, 220, 44)), toolbar);
    }

    internal static (BColor Fill, BColor Text) Look(BRenderList list, UiElement element, string text) =>
        (Assert.Single(list.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == element.Bounds).Color, TextColor(list, text));

    internal static (BColor Fill, BColor Text) ChevronLook(BRenderList list, StandardToolbar toolbar) =>
        (Assert.Single(list.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == toolbar.OverflowButtonBounds).Color, TextColor(list, Chevron));

    internal static BColor TextColor(BRenderList list, string text) =>
        Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == text).Text.Color;

    internal static BPoint Middle(BRect rect) => new(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));

    /// <summary>A session showing one element in a fixed box, and the pointer to work it with.</summary>
    internal sealed class Harness : System.IDisposable
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
