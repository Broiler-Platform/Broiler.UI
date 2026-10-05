using System.Linq;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Xunit;
using static Broiler.UI.Standard.Tests.StateFillRoleTests;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The state roles of the shared palette (ADR 0029): the shared accessors read them, and a spin box built after the
/// shared palette changes, and never themed, draws a hovered arrow in the shared state pair.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class StateFillSharedPaletteTests
{
    [Fact]
    public void The_Shared_Palette_Reads_The_State_Pair_Of_Its_Theme()
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardThemeTokens states = DistinctStates();
            StandardControlPaint.ApplyTheme(states);
            Assert.Equal(states.StateFill, StandardControlPaint.StateFill);
            Assert.Equal(states.StateText, StandardControlPaint.StateText);
            Assert.NotEqual(states.AccentSoft, StandardControlPaint.StateFill);
            Assert.NotEqual(states.Text, StandardControlPaint.StateText);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    [Theory]
    [MemberData(nameof(SystemPalettes), MemberType = typeof(StateFillRoleTests))]
    public void A_Spin_Box_Built_After_A_System_Palette_Is_Shared_Draws_A_Hovered_Arrow_In_The_Shared_Pair(StandardThemeTokens theme)
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardControlPaint.ApplyTheme(theme);
            using SpinFixture fixture = SpinFixture.Create(null);

            (BRenderList list, BRect up) = fixture.HoverUpArrow();

            AssertHoveredArrowReads(list, up, (StandardControlPaint.StateFill, StandardControlPaint.StateText));
            Assert.Equal(theme.SelectionText, StandardControlPaint.StateText);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    [Theory]
    [MemberData(nameof(Presets), MemberType = typeof(StateFillRoleTests))]
    public void A_Spin_Box_Built_Under_A_Preset_Draws_A_Hovered_Arrow_As_Before(StandardThemeTokens theme)
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardControlPaint.ApplyTheme(theme);
            using SpinFixture fixture = SpinFixture.Create(null);

            (BRenderList list, BRect up) = fixture.HoverUpArrow();

            Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Rect == up && fill.Color == theme.AccentSoft);
            Assert.Equal([theme.TextMuted, theme.TextMuted], ArrowColors(list));
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }
}
