using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.UI.Button.Standard;
using Broiler.UI.SpinBox.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;
using static Broiler.UI.Toolbar.Tests.StateFillControlTests;

namespace Broiler.UI.Toolbar.Tests;

/// <summary>
/// A focus ring is drawn on a fill, and is drawn in the theme's ring color only where that stands out from the fill
/// the control draws in its current state (3:1); otherwise in the label color drawn on that fill (ADR 0032). Buttons
/// and toggle buttons ring themselves inside their fill, the spin box with its frame around its field.
/// </summary>
public sealed class FocusRingOnFillTests
{
    /// <summary>
    /// Palettes built as Broiler.Hosting builds one from a Windows contrast theme: the four Windows 11 themes, and
    /// custom ones whose selected text is the window text, with a highlight that stands out from the window color
    /// (so it is the ring too) and one that does not (so the ring is the window text).
    /// </summary>
    public static TheoryData<StandardThemeTokens> HostingPalettes() => new()
    {
        HostingPalette("Aquatic", 0x202020, 0xFFFFFF, 0x8EE3F0, 0x263B50),
        HostingPalette("Desert", 0xFFFAEF, 0x3D3D3D, 0x903909, 0xFFF5E3),
        HostingPalette("Dusk", 0x2D3236, 0xFFFFFF, 0xA1BFDE, 0x212D3B),
        HostingPalette("NightSky", 0x000000, 0xFFFFFF, 0xD6B4FD, 0x2B2B2B),
        HostingPalette("SameSelectedText", 0x000000, 0xFFFFFF, 0x1A6FDF, 0xFFFFFF),
        HostingPalette("SameSelectedTextDimHighlight", 0x000000, 0xFFFFFF, 0x0000A0, 0xFFFFFF),
    };

    /// <summary>
    /// The four Windows 11 contrast themes as Broiler.Hosting's main branch builds them, before its contrast palette
    /// set the state pair: the state fill is the highlight, and the text on it the window text.
    /// </summary>
    public static TheoryData<StandardThemeTokens> HostingPalettesWithoutTheStatePair() => new()
    {
        HostingPalette("Aquatic", 0x202020, 0xFFFFFF, 0x8EE3F0, 0x263B50, statePair: false),
        HostingPalette("Desert", 0xFFFAEF, 0x3D3D3D, 0x903909, 0xFFF5E3, statePair: false),
        HostingPalette("Dusk", 0x2D3236, 0xFFFFFF, 0xA1BFDE, 0x212D3B, statePair: false),
        HostingPalette("NightSky", 0x000000, 0xFFFFFF, 0xD6B4FD, 0x2B2B2B, statePair: false),
    };

    public static TheoryData<StandardThemeTokens> EveryPalette()
    {
        var palettes = new TheoryData<StandardThemeTokens>();
        foreach (TheoryData<StandardThemeTokens> set in new[] { Presets(), HostingPalettes() })
        {
            foreach (StandardThemeTokens theme in set)
                palettes.Add(theme);
        }

        return palettes;
    }

    /// <summary>
    /// The mapping Broiler.Hosting's <c>WindowsTheme.CreateHighContrastTheme</c> makes from the system colors: the
    /// window pair on the surfaces, the borders and every text role; the highlight pair on the accent, the selection
    /// and the state fill; and the highlight as the focus ring where it stands out from the window color (3:1), the
    /// window text otherwise. Without <paramref name="statePair"/>, the mapping before Hosting set the selection and
    /// state text: those derive from the window text.
    /// </summary>
    internal static StandardThemeTokens HostingPalette(string name, uint window, uint windowText, uint highlight, uint highlightText, bool statePair = true)
    {
        BColor surface = Rgb(window), text = Rgb(windowText), fill = Rgb(highlight), onFill = Rgb(highlightText);
        bool dark = StandardContrast.RelativeLuminance(surface) < StandardContrast.RelativeLuminance(text);
        StandardThemeTokens palette = (dark ? StandardThemeTokens.HighContrastDark : StandardThemeTokens.HighContrastLight) with
        {
            Name = name,
            IsDark = dark,
            Surface = surface,
            SurfaceAlt = surface,
            SurfaceDisabled = surface,
            Border = text,
            BorderStrong = text,
            Text = text,
            TextMuted = text,
            Accent = fill,
            AccentHover = fill,
            AccentPressed = fill,
            AccentSoft = fill,
            OnAccent = onFill,
            FocusRing = StandardContrast.Ratio(fill, surface) >= StandardContrast.AaLargeOrUi ? fill : text,
            IsHighContrast = true,
        };

        return statePair
            ? palette with
            {
                SelectionText = onFill,
                SelectionTextMuted = onFill,
                StateFill = fill,
                StateText = onFill,
            }
            : palette;

        static BColor Rgb(uint rgb) => BColor.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    // --- The rule ----------------------------------------------------------

    [Fact]
    public void The_Ring_Takes_The_Label_Color_Only_Where_It_Does_Not_Stand_Out_From_An_Opaque_Fill()
    {
        BColor ring = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8);
        BColor label = BColor.White;

        Assert.Equal(label, StandardControlPaint.FocusRingColor(ring, ring, label));
        Assert.Equal(ring, StandardControlPaint.FocusRingColor(ring, BColor.White, label));

        // 3:1 is enough: Light's ring on its soft accent fill reads at 4.3:1 and is kept.
        Assert.Equal(ring, StandardControlPaint.FocusRingColor(ring, StandardThemeTokens.Light.AccentSoft, label));

        // What shows through a fill that is not opaque is not known, so the ring is kept.
        Assert.Equal(ring, StandardControlPaint.FocusRingColor(ring, BColor.FromArgb(0x80, 0x0B, 0x6F, 0xD8), label));
    }

    // --- Presets -----------------------------------------------------------

    [Theory]
    [MemberData(nameof(Presets), MemberType = typeof(StateFillControlTests))]
    public void A_Preset_Rings_Secondary_And_Toggle_Buttons_As_Before(StandardThemeTokens theme)
    {
        // The ring stands out from every fill these draw, so it keeps the theme's color.
        foreach ((string state, Drawn look) in SecondaryButtonStates(theme).Concat(ToggleButtonStates(theme)))
        {
            Assert.True(theme.FocusRing == look.Ring, $"{theme.Name} {state}: ring {look.Ring}, not the theme's {theme.FocusRing}.");
            AssertVisible(theme, state, look);
        }
    }

    [Theory]
    [MemberData(nameof(Presets), MemberType = typeof(StateFillControlTests))]
    public void A_Preset_Rings_A_Default_Button_In_Its_Label_Color(StandardThemeTokens theme)
    {
        // The rings that did not show at rest: Light's ring is its accent (1:1), Dark's reads at 2.3:1 on its accent,
        // the high-contrast presets' at 1.9:1 and 1.5:1. They are drawn in the label color instead.
        Dictionary<string, Drawn> states = DefaultButtonStates(theme).ToDictionary(state => state.State, state => state.Look);
        Assert.True(StandardContrast.Ratio(theme.FocusRing, theme.Accent) < StandardContrast.AaLargeOrUi, theme.Name);
        Assert.Equal(theme.Accent, states["at rest"].Fill);
        Assert.Equal(theme.OnAccent, states["at rest"].Ring);

        // Hovered and pressed the same rule picks against those fills: Dark's ring stands out from its darker
        // pressed accent (3.1:1) and is kept there.
        foreach ((string state, Drawn look) in states)
        {
            BColor expected = StandardContrast.Ratio(theme.FocusRing, look.Fill) >= StandardContrast.AaLargeOrUi ? theme.FocusRing : theme.OnAccent;
            Assert.True(expected == look.Ring, $"{theme.Name} {state}: ring {look.Ring}, expected {expected} on {look.Fill}.");
            AssertVisible(theme, state, look);
        }
    }

    // --- Palettes a host builds from a Windows contrast theme -------------

    [Theory]
    [MemberData(nameof(HostingPalettes))]
    public void A_System_Contrast_Palette_Leaves_Every_Button_Ring_Visible(StandardThemeTokens theme)
    {
        IEnumerable<(string, Drawn)> states = SecondaryButtonStates(theme)
            .Concat(DefaultButtonStates(theme))
            .Concat(ToggleButtonStates(theme));
        foreach ((string state, Drawn look) in states)
        {
            // The theme's ring where it shows, which is on the window color, and the label on the highlight.
            BColor expected = StandardContrast.Ratio(theme.FocusRing, look.Fill) >= StandardContrast.AaLargeOrUi ? theme.FocusRing : look.Text;
            Assert.True(expected == look.Ring, $"{theme.Name} {state}: ring {look.Ring}, expected {expected} on {look.Fill}.");
            AssertVisible(theme, state, look);
        }
    }

    [Theory]
    [MemberData(nameof(HostingPalettes))]
    public void A_Hovered_Secondary_Button_And_A_Default_Button_Ring_Themselves_In_The_Highlight_Text_When_The_Ring_Is_The_Highlight(StandardThemeTokens theme)
    {
        // The toolbar buttons and the Cc/Bcc toggle under the pointer, and Send, in Mail's system contrast palette.
        Dictionary<string, Drawn> secondary = SecondaryButtonStates(theme).ToDictionary(state => state.State, state => state.Look);
        Dictionary<string, Drawn> primary = DefaultButtonStates(theme).ToDictionary(state => state.State, state => state.Look);
        Dictionary<string, Drawn> toggle = ToggleButtonStates(theme).ToDictionary(state => state.State, state => state.Look);
        Assert.Equal(theme.StateFill, secondary["hovered"].Fill);

        BColor onHighlight = theme.FocusRing == theme.StateFill ? theme.SelectionText : theme.FocusRing;
        Assert.Equal(onHighlight, secondary["hovered"].Ring);
        Assert.Equal(onHighlight, primary["at rest"].Ring);
        Assert.Equal(onHighlight, primary["hovered"].Ring);
        Assert.Equal(onHighlight, toggle["checked"].Ring);

        // On the window color the ring is the theme's.
        Assert.Equal(theme.FocusRing, secondary["at rest"].Ring);
        Assert.Equal(theme.FocusRing, toggle["off"].Ring);
    }

    [Theory]
    [MemberData(nameof(HostingPalettesWithoutTheStatePair))]
    public void Without_The_State_Pair_Only_The_Default_Button_Ring_Is_Restored_And_The_State_Fill_Ring_Is_Its_Label(StandardThemeTokens theme)
    {
        // The default button's label is OnAccent, the highlight text, whichever Hosting mapping built the palette.
        Dictionary<string, Drawn> primary = DefaultButtonStates(theme).ToDictionary(state => state.State, state => state.Look);
        foreach ((string state, Drawn look) in primary)
        {
            Assert.Equal(theme.OnAccent, look.Ring);
            AssertVisible(theme, state, look);
        }

        // On the state fill the label is the window text, which the palette left there and which reads at 1.4 to
        // 1.9:1 on the highlight. The ring follows that label: the hovered ring shows only once the palette sets the
        // state text to the highlight text (ADR 0032).
        Drawn hovered = SecondaryButtonStates(theme).Single(state => state.State == "hovered").Look;
        Assert.Equal(theme.StateFill, hovered.Fill);
        Assert.Equal(theme.Text, hovered.Text);
        Assert.Equal(theme.Text, hovered.Ring);
        Assert.True(StandardContrast.Ratio(theme.Text, theme.StateFill) < StandardContrast.AaLargeOrUi, theme.Name);
    }

    // --- Spin box ----------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryPalette))]
    public void A_Focused_Spin_Box_Frame_Stands_Out_From_Its_Field_And_Its_Arrows(StandardThemeTokens theme)
    {
        var spin = new StandardSpinBox { Minimum = 0, Maximum = 10, Value = 5 };
        spin.ApplyTheme(theme);
        using var harness = new Harness(spin, new BRect(10, 10, 120, 32));
        harness.Session.SetFocus(spin.Edit);

        (BColor field, BRenderCommand.StrokeRoundedRect frame) = SpinLook(harness.Render(), spin);
        Assert.Equal(theme.Surface, field);
        Assert.Equal(2, frame.Thickness);
        // Every palette here has a ring that stands out from its window or surface color.
        Assert.Equal(theme.FocusRing, frame.Color);
        AssertStandsOut(theme, "frame on the field", frame.Color, field);

        // A hovered arrow is filled up to the frame where the ring stands out from the fill, as before, and inside it
        // where it does not, so the field shows between them.
        harness.Move(Middle(spin.UpArrowBounds));
        BRenderList hovered = harness.Render();
        Assert.Equal(theme.FocusRing, SpinLook(hovered, spin).Frame.Color);
        if (StandardContrast.Ratio(theme.FocusRing, theme.StateFill) >= StandardContrast.AaLargeOrUi)
        {
            Assert.Contains(hovered.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Rect == spin.UpArrowBounds && fill.Color == theme.StateFill);
        }
        else
        {
            Assert.DoesNotContain(hovered.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Rect == spin.UpArrowBounds);
            BRenderCommand.FillRoundedRect cell = Assert.Single(hovered.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Color == theme.StateFill);
            Assert.Equal(StandardControlPaint.Inset(spin.UpArrowBounds, 2), cell.Rect);
            Assert.Equal(StandardControlPaint.ResolveRadius(cell.Rect, spin.CornerRadius - 2), cell.RadiusX);
        }

        // Pressed, an arrow is filled with the disabled surface, which the ring stands out from.
        harness.Press(Middle(spin.DownArrowBounds));
        BRenderList pressed = harness.Render();
        Assert.Contains(pressed.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Rect == spin.DownArrowBounds && fill.Color == theme.SurfaceDisabled);
        Assert.Equal(theme.FocusRing, SpinLook(pressed, spin).Frame.Color);
    }

    [Fact]
    public void A_Spin_Box_Whose_Ring_Is_Its_Field_Color_Frames_Itself_In_The_Field_Text()
    {
        StandardThemeTokens theme = StandardThemeTokens.Light with { FocusRing = StandardThemeTokens.Light.Surface };
        var spin = new StandardSpinBox { Minimum = 0, Maximum = 10, Value = 5 };
        spin.ApplyTheme(theme);
        using var harness = new Harness(spin, new BRect(10, 10, 120, 32));
        harness.Session.SetFocus(spin.Edit);

        (BColor field, BRenderCommand.StrokeRoundedRect frame) = SpinLook(harness.Render(), spin);
        Assert.Equal(theme.Text, spin.Edit.Foreground);
        Assert.Equal(theme.Text, frame.Color);
        AssertStandsOut(theme, "frame on the field", frame.Color, field);

        // Unfocused, the frame is the border again.
        harness.Session.SetFocus(null);
        Assert.Equal(theme.Border, SpinLook(harness.Render(), spin).Frame.Color);
    }

    // --- States ------------------------------------------------------------

    /// <summary>What a focused control drew: its fill, its label and its ring.</summary>
    internal readonly record struct Drawn(BColor Fill, BColor Text, BColor Ring);

    /// <summary>A keyboard-focused secondary button at rest, hovered and pressed.</summary>
    private static IEnumerable<(string State, Drawn Look)> SecondaryButtonStates(StandardThemeTokens theme) =>
        ButtonStates(theme, isDefault: false);

    /// <summary>A keyboard-focused default button at rest, hovered and pressed.</summary>
    private static IEnumerable<(string State, Drawn Look)> DefaultButtonStates(StandardThemeTokens theme) =>
        ButtonStates(theme, isDefault: true);

    private static List<(string State, Drawn Look)> ButtonStates(StandardThemeTokens theme, bool isDefault)
    {
        var button = new StandardButton { Text = "Send", IsDefault = isDefault };
        button.ApplyTheme(theme);
        using var harness = new Harness(button, new BRect(10, 10, 80, 30));
        harness.Session.SetFocus(button);
        var states = new List<(string, Drawn)> { ("at rest", ButtonLook(harness.Render(), button, "Send")) };

        // The pointer rests on the button while the keyboard works it: the ring shows on the hover fill.
        harness.Move(Middle(button.Bounds));
        harness.Key("Tab", BVirtualKey.Tab);
        states.Add(("hovered", ButtonLook(harness.Render(), button, "Send")));

        harness.Key("Space", BVirtualKey.Space);
        Assert.True(button.IsPressed);
        states.Add(("pressed", ButtonLook(harness.Render(), button, "Send")));
        harness.Key("Space", BVirtualKey.Space, KeyboardKeyTransition.Up);
        return states;
    }

    /// <summary>A keyboard-focused toggle button off, hovered, pressed, checked and indeterminate.</summary>
    private static List<(string State, Drawn Look)> ToggleButtonStates(StandardThemeTokens theme)
    {
        var toggle = new StandardToggleButton { Text = "Bold", IsThreeState = true };
        toggle.ApplyTheme(theme);
        using var harness = new Harness(toggle, new BRect(10, 10, 80, 30));
        harness.Session.SetFocus(toggle);
        var states = new List<(string, Drawn)> { ("off", ButtonLook(harness.Render(), toggle, "Bold")) };

        harness.Move(Middle(toggle.Bounds));
        harness.Key("Tab", BVirtualKey.Tab);
        states.Add(("hovered", ButtonLook(harness.Render(), toggle, "Bold")));

        harness.Key("Space", BVirtualKey.Space);
        states.Add(("pressed", ButtonLook(harness.Render(), toggle, "Bold")));
        harness.Key("Space", BVirtualKey.Space, KeyboardKeyTransition.Up);
        Assert.Equal(UiToggleState.On, toggle.ToggleState);
        states.Add(("checked", ButtonLook(harness.Render(), toggle, "Bold")));

        toggle.ToggleState = UiToggleState.Indeterminate;
        states.Add(("indeterminate", ButtonLook(harness.Render(), toggle, "Bold")));
        return states;
    }

    /// <summary>The fill drawn over the button's bounds, its label, and the ring stroked 2 DIP inside it.</summary>
    private static Drawn ButtonLook(BRenderList list, UiElement element, string text)
    {
        (BColor fill, BColor label) = StateFillControlTests.Look(list, element, text);
        BRect ring = StandardControlPaint.Inset(element.Bounds, 2);
        return new Drawn(fill, label, Assert.Single(list.Commands.OfType<BRenderCommand.StrokeRoundedRect>(), stroke => stroke.Rect == ring).Color);
    }

    /// <summary>The spin box's field fill and its frame.</summary>
    private static (BColor Field, BRenderCommand.StrokeRoundedRect Frame) SpinLook(BRenderList list, StandardSpinBox spin) =>
        (Assert.Single(list.Commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect == spin.Bounds).Color,
         Assert.Single(list.Commands.OfType<BRenderCommand.StrokeRoundedRect>(), stroke => stroke.Rect == spin.Bounds));

    private static void AssertVisible(StandardThemeTokens theme, string state, Drawn look) =>
        AssertStandsOut(theme, state, look.Ring, look.Fill);

    private static void AssertStandsOut(StandardThemeTokens theme, string what, BColor ring, BColor fill)
    {
        double ratio = StandardContrast.Ratio(ring, fill);
        Assert.True(ratio >= StandardContrast.AaLargeOrUi, $"{theme.Name} {what}: ring {ring} on {fill} is {ratio:0.00}:1.");
    }
}
