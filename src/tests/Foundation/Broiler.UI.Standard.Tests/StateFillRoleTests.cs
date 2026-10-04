using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Mouse;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.SpinBox.Standard;
using Xunit;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The state roles (ADR 0029): the fill a control draws for a state that is not a selection, and the text on
/// it, have colors of their own that the presets leave at the selection fill and the text color.
/// </summary>
public sealed class StateFillRoleTests
{
    private static readonly BColor HighlightText = BColor.FromArgb(0xFF, 0x00, 0x00, 0x00);

    /// <summary>A palette whose state pair is neither the selection fill nor any text color, so each use shows.</summary>
    internal static StandardThemeTokens DistinctStates() =>
        StandardThemeTokens.Light with
        {
            Name = "DistinctStates",
            StateFill = BColor.FromArgb(0xFF, 0x30, 0x00, 0x60),
            StateText = BColor.FromArgb(0xFF, 0xFF, 0xF0, 0xA0),
        };

    public static TheoryData<StandardThemeTokens> Presets() => SelectionTextRoleTests.Presets();

    [Theory]
    [MemberData(nameof(Presets))]
    public void Presets_Draw_States_On_The_Selection_Fill_In_Their_Text_Color(StandardThemeTokens theme)
    {
        Assert.Equal(theme.AccentSoft, theme.StateFill);
        Assert.Equal(theme.Text, theme.StateText);
        Assert.Equal(StandardContrast.Ratio(theme.Text, theme.AccentSoft), theme.StateTextContrast, 6);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void State_Text_Meets_WCAG_AA_On_The_State_Fill(StandardThemeTokens theme)
    {
        Assert.True(
            theme.StateTextContrast >= StandardContrast.AaNormalText,
            $"{theme.Name}: StateText/StateFill contrast {theme.StateTextContrast:0.00}:1 is below the required {StandardContrast.AaNormalText:0.0}:1.");
    }

    [Fact]
    public void A_System_Highlight_Palette_Reads_Its_States_In_The_Selection_Text()
    {
        // The highlight is the selection fill, so the states are drawn on it, in the text that reads on it.
        StandardThemeTokens system = SelectionTextRoleTests.SystemHighContrast();
        Assert.Equal(system.AccentSoft, system.StateFill);
        Assert.Equal(HighlightText, system.StateText);
        Assert.True(system.StateTextContrast >= StandardContrast.AaNormalText, $"{system.StateTextContrast:0.00}:1");

        // A fill of the states' own is not the highlight, so the text that reads on the highlight is not assumed.
        BColor fill = BColor.FromArgb(0xFF, 0x00, 0x00, 0x80);
        Assert.Equal(system.Text, (system with { StateFill = fill }).StateText);
        Assert.Equal(BColor.Black, (system with { StateFill = fill, StateText = BColor.Black }).StateText);
    }

    [Fact]
    public void Copies_Follow_A_New_Selection_Fill_And_Text_Until_A_Theme_Sets_The_State_Pair()
    {
        BColor fill = BColor.FromArgb(0xFF, 0x11, 0x22, 0x33);
        BColor text = BColor.FromArgb(0xFF, 0xEE, 0xDD, 0xCC);
        StandardThemeTokens recolored = StandardThemeTokens.Dark with { AccentSoft = fill, Text = text };
        Assert.Equal(fill, recolored.StateFill);
        Assert.Equal(text, recolored.StateText);
        Assert.Equal(fill, (recolored with { SelectionText = BColor.Black }).StateFill);
        Assert.Equal(BColor.Black, (recolored with { SelectionText = BColor.Black }).StateText);

        // Once set, the pair is the theme's own: it survives copies that change the roles it would follow.
        StandardThemeTokens states = DistinctStates();
        StandardThemeTokens copy = (states with { AccentSoft = fill, Text = text }).WithTextScale(2);
        Assert.Equal(DistinctStates().StateFill, copy.StateFill);
        Assert.Equal(DistinctStates().StateText, copy.StateText);

        // The legacy constructor leaves the pair unset as well.
        var legacy = new StandardThemeTokens(BColor.Black, BColor.White, BColor.Green, BColor.Red);
        Assert.Equal(legacy.AccentSoft, legacy.StateFill);
        Assert.Equal(BColor.White, legacy.StateText);

        Assert.Equal(StandardControlPaint.Theme.StateFill, StandardControlPaint.StateFill);
        Assert.Equal(StandardControlPaint.Theme.StateText, StandardControlPaint.StateText);
    }

    [Fact]
    public void A_Hovered_Spin_Arrow_Is_Drawn_In_The_State_Pair()
    {
        StandardThemeTokens theme = DistinctStates();
        using SpinFixture fixture = SpinFixture.Create(theme);

        (BRenderList list, BRect up) = fixture.HoverUpArrow();

        Assert.Equal(theme.StateFill, fixture.Spin.ArrowHoverBackground);
        Assert.Equal(theme.StateText, fixture.Spin.ArrowHoverColor);
        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Rect == up && fill.Color == theme.StateFill);
        Assert.Equal([theme.StateText, theme.TextMuted], ArrowColors(list));
    }

    [Fact]
    public void A_Hovered_Spin_Arrow_Reads_On_A_System_Highlight()
    {
        StandardThemeTokens system = SelectionTextRoleTests.SystemHighContrast();
        using SpinFixture fixture = SpinFixture.Create(system);

        (BRenderList list, BRect up) = fixture.HoverUpArrow();

        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Rect == up && fill.Color == system.AccentSoft);
        Assert.Equal([HighlightText, system.TextMuted], ArrowColors(list));
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void A_Preset_Draws_A_Hovered_Spin_Arrow_As_Before(StandardThemeTokens theme)
    {
        using SpinFixture fixture = SpinFixture.Create(theme);

        (BRenderList list, BRect up) = fixture.HoverUpArrow();

        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Rect == up && fill.Color == theme.AccentSoft);
        Assert.Equal([theme.TextMuted, theme.TextMuted], ArrowColors(list));

        // An arrow color the application sets after the theme still reaches the hovered arrow, as it always has.
        BColor custom = BColor.FromArgb(0xFF, 0x80, 0x10, 0x10);
        fixture.Spin.ArrowColor = custom;
        Assert.Equal([custom, custom], ArrowColors(fixture.Session.RenderFrame()));
    }

    [Fact]
    public void A_Matching_Bracket_Is_Marked_With_The_State_Fill()
    {
        Assert.Equal(DistinctStates().StateFill, StandardCodeEditorPalette.FromTokens(DistinctStates()).BracketMatch);
        foreach (StandardThemeTokens preset in new[] { StandardThemeTokens.Light, StandardThemeTokens.Dark, StandardThemeTokens.HighContrastLight, StandardThemeTokens.HighContrastDark })
            Assert.Equal(preset.AccentSoft, StandardCodeEditorPalette.FromTokens(preset).BracketMatch);
    }

    /// <summary>The up arrow's color, then the down arrow's: the order the box draws them in.</summary>
    private static List<BColor> ArrowColors(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.FillTriangle>().Select(static triangle => triangle.Color).ToList();

    private sealed class SpinFixture : System.IDisposable
    {
        private SpinFixture(UiSession session, StandardSpinBox spin)
        {
            Session = session;
            Spin = spin;
        }

        public UiSession Session { get; }

        public StandardSpinBox Spin { get; }

        public static SpinFixture Create(StandardThemeTokens theme)
        {
            UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new TestHost());
            var spin = new StandardSpinBox { Minimum = 0, Maximum = 100, Value = 5 };
            spin.ApplyTheme(theme);
            session.AddRoot(spin);
            return new SpinFixture(session, spin);
        }

        public (BRenderList List, BRect Up) HoverUpArrow()
        {
            Session.RenderFrame();
            BRect up = Spin.UpArrowBounds;
            Assert.False(up.IsEmpty);
            BPoint center = new(up.Left + (up.Width / 2), up.Top + (up.Height / 2));
            Session.DispatchInput(UiInputEvent.FromMouseMove(
                new MouseMoveEvent(
                    new InputEventHeader(InputDeviceId.FromOpaqueValue("mouse"), new InputTimestamp(1, System.TimeSpan.TicksPerSecond, "state-test"), 1),
                    InputPoint.ClientDeviceIndependentPixels(center.X, center.Y),
                    MouseButtons.None,
                    InputEventSource.Synthetic)));
            return (Session.RenderFrame(), up);
        }

        public void Dispose() => Session.Dispose();
    }

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize { get; } = new(160, 32);
        public double Scale => 1.0;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
