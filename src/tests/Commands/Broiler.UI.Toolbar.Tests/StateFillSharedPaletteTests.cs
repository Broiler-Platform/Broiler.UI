using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.UI.Button.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;
using Broiler.UI.ToggleButton.Standard;
using Broiler.UI.Toolbar.Standard;
using static Broiler.UI.Toolbar.Tests.StateFillControlTests;

namespace Broiler.UI.Toolbar.Tests;

/// <summary>
/// The state roles of controls that follow the shared palette (ADR 0029): a control built after the shared palette
/// changes, and never themed, draws its states in the shared state pair, as it draws them on the shared state fill.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class StateFillSharedPaletteTests
{
    /// <summary>The fixed pressed and indeterminate fills of a toggle button that is never themed.</summary>
    private static readonly BColor UnthemedPressed = BColor.FromArgb(0xFF, 0xD8, 0xE8, 0xFC);
    private static readonly BColor UnthemedIndeterminate = BColor.FromArgb(0xFF, 0xF0, 0xF5, 0xFF);

    [Theory]
    [MemberData(nameof(SystemPalettes), MemberType = typeof(StateFillControlTests))]
    public void A_Control_Built_After_A_System_Palette_Is_Shared_Draws_Its_States_In_The_Shared_Pair(StandardThemeTokens theme)
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardControlPaint.ApplyTheme(theme);
            (BColor Fill, BColor Text) shared = (StandardControlPaint.StateFill, StandardControlPaint.StateText);
            Assert.Equal((theme.AccentSoft, theme.SelectionText), shared);

            var button = new StandardButton { Text = "Hover" };
            using (var harness = new Harness(button, new BRect(10, 10, 80, 30)))
            {
                harness.Move(Middle(button.Bounds));
                AssertReadable(shared, Look(harness.Render(), button, "Hover"));
            }

            var toggle = new StandardToggleButton { Text = "Bold", IsThreeState = true };
            using (var harness = new Harness(toggle, new BRect(10, 10, 80, 30)))
            {
                harness.Press(Middle(toggle.Bounds));
                AssertReadable(shared, Look(harness.Render(), toggle, "Bold"));
                harness.Release(Middle(toggle.Bounds));
                Assert.Equal(UiToggleState.On, toggle.ToggleState);
                AssertReadable(shared, Look(harness.Render(), toggle, "Bold"));
                toggle.ToggleState = UiToggleState.Indeterminate;
                AssertReadable(shared, Look(harness.Render(), toggle, "Bold"));
            }

            (Harness bar, StandardToolbar toolbar) = OverflowingBar(null);
            using (bar)
            {
                bar.Render();
                Assert.True(toolbar.OpenOverflow());
                AssertReadable(shared, ChevronLook(bar.Render(), toolbar));
            }
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    [Theory]
    [MemberData(nameof(Presets), MemberType = typeof(StateFillControlTests))]
    public void A_Control_Built_Under_A_Preset_Draws_Its_States_As_Before(StandardThemeTokens theme)
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardControlPaint.ApplyTheme(theme);

            var button = new StandardButton { Text = "Hover" };
            using (var harness = new Harness(button, new BRect(10, 10, 80, 30)))
            {
                harness.Move(Middle(button.Bounds));
                Assert.Equal((theme.AccentSoft, theme.Text), Look(harness.Render(), button, "Hover"));
            }

            var toggle = new StandardToggleButton { Text = "Bold", IsThreeState = true };
            using (var harness = new Harness(toggle, new BRect(10, 10, 80, 30)))
            {
                harness.Press(Middle(toggle.Bounds));
                Assert.Equal((UnthemedPressed, theme.Accent), Look(harness.Render(), toggle, "Bold"));
                harness.Release(Middle(toggle.Bounds));
                Assert.Equal((theme.AccentSoft, theme.Accent), Look(harness.Render(), toggle, "Bold"));
                toggle.ToggleState = UiToggleState.Indeterminate;
                Assert.Equal((UnthemedIndeterminate, theme.Accent), Look(harness.Render(), toggle, "Bold"));
            }

            (Harness bar, StandardToolbar toolbar) = OverflowingBar(null);
            using (bar)
            {
                bar.Render();
                Assert.True(toolbar.OpenOverflow());
                Assert.Equal((theme.AccentSoft, theme.Text), ChevronLook(bar.Render(), toolbar));
            }
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }
}
