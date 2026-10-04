using Broiler.Graphics;
using Broiler.Graphics.Color;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The accent text role (ADR 0031): accent-colored text on a surface has a color of its own, which is the accent
/// until a theme sets it, and which reads on the surfaces in every preset.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class AccentTextRoleTests
{
    public static TheoryData<StandardThemeTokens> Presets() => SelectionTextRoleTests.Presets();

    [Theory]
    [MemberData(nameof(Presets))]
    public void Accent_Text_Meets_WCAG_AA_On_The_Surfaces(StandardThemeTokens theme)
    {
        // The surface is also the selected tab header's fill; SurfaceAlt is a pane or a strip behind it.
        AssertMeets(theme.AccentText, theme.Surface, theme, "AccentText/Surface");
        AssertMeets(theme.AccentText, theme.SurfaceAlt, theme, "AccentText/SurfaceAlt");
        Assert.Equal(StandardContrast.Ratio(theme.AccentText, theme.Surface), theme.AccentTextContrast, 6);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Only_The_Dark_Preset_Gives_Accent_Text_A_Color_Of_Its_Own(StandardThemeTokens theme)
    {
        if (theme.Name != "Dark")
        {
            // Light reads at 4.9:1, and the high-contrast presets keep their accent, the highlight, as text.
            Assert.Equal(theme.Accent, theme.AccentText);
            return;
        }

        // A lighter tint of the accent: the accent itself is a fill that reads at 3.4:1 on the dark surface.
        Assert.True(StandardContrast.Ratio(theme.Accent, theme.Surface) < StandardContrast.AaNormalText);
        Assert.True(StandardContrast.RelativeLuminance(theme.AccentText) > StandardContrast.RelativeLuminance(theme.Accent));
    }

    [Fact]
    public void Copies_Follow_A_New_Accent_Until_A_Theme_Sets_The_Accent_Text()
    {
        BColor accent = BColor.FromArgb(0xFF, 0x00, 0x55, 0x99);
        Assert.Equal(accent, (StandardThemeTokens.Light with { Accent = accent }).AccentText);
        Assert.Equal(accent, (StandardThemeTokens.HighContrastDark with { Accent = accent }).AccentText);

        // A set value is the theme's own and survives copies, as Dark's does.
        Assert.Equal(StandardThemeTokens.Dark.AccentText, (StandardThemeTokens.Dark with { Accent = accent }).WithTextScale(2).AccentText);
        Assert.Equal(BColor.Black, (StandardThemeTokens.Light with { AccentText = BColor.Black, Accent = accent }).AccentText);

        // A host's system palette maps the accent to the highlight, and so its accent text.
        StandardThemeTokens system = SelectionTextRoleTests.SystemHighContrast();
        Assert.Equal(system.Accent, system.AccentText);

        var legacy = new StandardThemeTokens(BColor.Black, BColor.White, BColor.Green, BColor.Red);
        Assert.Equal(BColor.Green, legacy.AccentText);
    }

    [Fact]
    public void The_Shared_Palette_Reads_The_Accent_Text()
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardControlPaint.ApplyTheme(StandardThemeTokens.Dark);
            Assert.Equal(StandardThemeTokens.Dark.AccentText, StandardControlPaint.AccentText);
            StandardControlPaint.ApplyTheme(StandardThemeTokens.Light);
            Assert.Equal(StandardThemeTokens.Light.Accent, StandardControlPaint.AccentText);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(original);
        }
    }

    private static void AssertMeets(BColor foreground, BColor background, StandardThemeTokens theme, string pair)
    {
        double ratio = StandardContrast.Ratio(foreground, background);
        Assert.True(
            ratio >= StandardContrast.AaNormalText,
            $"{theme.Name}: {pair} contrast {ratio:0.00}:1 is below the required {StandardContrast.AaNormalText:0.0}:1.");
    }
}
