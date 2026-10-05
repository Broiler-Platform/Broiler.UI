using Broiler.Graphics;
using Broiler.Graphics.Color;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The accent text role (ADR 0031): accent-colored text on a surface has a color of its own, which is the accent
/// until a theme sets it, and which reads on the surfaces and the soft accent fill in every preset.
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class AccentTextRoleTests
{
    public static TheoryData<StandardThemeTokens> Presets() => SelectionTextRoleTests.Presets();

    [Theory]
    [MemberData(nameof(Presets))]
    public void Accent_Text_Meets_WCAG_AA_On_The_Surfaces(StandardThemeTokens theme)
    {
        // The surface is also the selected tab header's fill; SurfaceAlt is a pane or a strip behind it; AccentSoft
        // is a checked toggle button's fill.
        AssertMeets(theme.AccentText, theme.Surface, theme, "AccentText/Surface");
        AssertMeets(theme.AccentText, theme.SurfaceAlt, theme, "AccentText/SurfaceAlt");
        AssertMeets(theme.AccentText, theme.AccentSoft, theme, "AccentText/AccentSoft");
        Assert.Equal(StandardContrast.Ratio(theme.AccentText, theme.Surface), theme.AccentTextContrast, 6);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Light_And_Dark_Give_Accent_Text_A_Shade_Of_Their_Accent(StandardThemeTokens theme)
    {
        if (theme.IsHighContrast)
        {
            // The high-contrast presets keep their accent, the highlight, as text: it reads everywhere.
            Assert.Equal(theme.Accent, theme.AccentText);
            return;
        }

        // The accent itself is a fill. As text it reads at 3.4:1 on Dark's surface, and at 4.3:1 on Light's soft
        // accent fill; the accent text is a lighter shade on the dark surfaces and a darker one on the light ones.
        double accent = Math.Min(StandardContrast.Ratio(theme.Accent, theme.Surface), StandardContrast.Ratio(theme.Accent, theme.AccentSoft));
        Assert.True(accent < StandardContrast.AaNormalText, $"{theme.Name}: the accent reads at {accent:0.00}:1.");
        Assert.NotEqual(theme.Accent, theme.AccentText);
        Assert.Equal(theme.IsDark, StandardContrast.RelativeLuminance(theme.AccentText) > StandardContrast.RelativeLuminance(theme.Accent));
    }

    [Fact]
    public void A_Copy_Keeps_A_Presets_Accent_Text_Only_With_The_Presets_Accent()
    {
        BColor accent = BColor.FromArgb(0xFF, 0x7B, 0x3F, 0xE4);
        BColor own = BColor.FromArgb(0xFF, 0xB0, 0x90, 0xF0);
        foreach (StandardThemeTokens preset in new[] { StandardThemeTokens.Light, StandardThemeTokens.Dark })
        {
            // Copies that keep the accent keep the preset's accent text.
            Assert.Equal(preset.AccentText, preset.WithTextScale(2).AccentText);
            Assert.Equal(preset.AccentText, (preset with { Text = BColor.FromArgb(0xFF, 0x80, 0x80, 0x80), Density = UiDensity.Compact }).AccentText);
            Assert.Equal(preset.AccentText, StandardThemeTokens.Select(UiContrastPreference.NoPreference, preset.IsDark, UiDensity.Spacious, reducedMotion: true).AccentText);

            // A new accent, a brand's, is drawn as before the role existed: the preset's shade belongs to its own.
            Assert.Equal(accent, (preset with { Accent = accent }).AccentText);
            Assert.Equal(accent, (preset with { Accent = accent }).WithTextScale(2).AccentText);
            Assert.Equal(preset.AccentText, (preset with { Accent = accent } with { Accent = preset.Accent }).AccentText);

            // Accent text the copy sets is its own, in whichever order it is written and whatever accent follows.
            Assert.Equal(own, (preset with { Accent = accent, AccentText = own }).AccentText);
            Assert.Equal(own, (preset with { AccentText = own, Accent = accent }).AccentText);
            Assert.Equal(own, (preset with { AccentText = own } with { Accent = accent }).AccentText);
        }
    }

    [Fact]
    public void Copies_Follow_A_New_Accent_Until_A_Theme_Sets_The_Accent_Text()
    {
        BColor accent = BColor.FromArgb(0xFF, 0x00, 0x55, 0x99);
        Assert.Equal(accent, (StandardThemeTokens.Light with { Accent = accent }).AccentText);
        Assert.Equal(accent, (StandardThemeTokens.HighContrastDark with { Accent = accent }).AccentText);

        // A set value is the theme's own and survives copies.
        Assert.Equal(BColor.Black, (StandardThemeTokens.Light with { AccentText = BColor.Black, Accent = accent }).AccentText);
        Assert.Equal(BColor.Black, (StandardThemeTokens.Light with { AccentText = BColor.Black }).WithTextScale(2).AccentText);

        // A host's system palette maps the accent to the highlight, and so its accent text.
        StandardThemeTokens system = SelectionTextRoleTests.SystemHighContrast();
        Assert.Equal(system.Accent, system.AccentText);

        var legacy = new StandardThemeTokens(BColor.Black, BColor.White, BColor.Green, BColor.Red);
        Assert.Equal(BColor.Green, legacy.AccentText);
    }

    [Theory]
    [InlineData("Aquatic", 0x202020u, 0xFFFFFFu, 0x8EE3F0u, 0x263B50u)]
    [InlineData("Desert", 0xFFFAEFu, 0x3D3D3Du, 0x903909u, 0xFFF5E3u)]
    [InlineData("Dusk", 0x2D3236u, 0xFFFFFFu, 0xA1BFDEu, 0x212D3Bu)]
    [InlineData("NightSky", 0x000000u, 0xFFFFFFu, 0xD6B4FDu, 0x2B2B2Bu)]
    public void A_Windows_Contrast_Theme_Reads_Its_Highlight_As_Accent_Text(string name, uint window, uint windowText, uint highlight, uint highlightText)
    {
        // A host's palette follows its accent, the highlight, which these themes choose to read on the window.
        StandardThemeTokens system = StateFillRoleTests.SystemPalette(name, window, windowText, highlight, highlightText);
        Assert.Equal(system.Accent, system.AccentText);
        AssertMeets(system.AccentText, system.Surface, system, "AccentText/Surface");
    }

    [Fact]
    public void The_Shared_Palette_Reads_The_Accent_Text()
    {
        StandardThemeTokens original = StandardControlPaint.Theme;
        try
        {
            StandardControlPaint.ApplyTheme(StandardThemeTokens.Dark);
            Assert.Equal(StandardThemeTokens.Dark.AccentText, StandardControlPaint.AccentText);
            StandardControlPaint.ApplyTheme(StandardThemeTokens.HighContrastLight);
            Assert.Equal(StandardThemeTokens.HighContrastLight.Accent, StandardControlPaint.AccentText);
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
