using Broiler.Graphics.Color;
using Xunit;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// Palettes shaped like the one Broiler.Hosting builds from a Windows contrast theme
/// (<c>WindowsTheme.CreateHighContrastTheme</c>), for the four Windows 11 contrast themes as Windows ships them: the
/// window pair for every surface, border and text, the highlight pair for the accent, the selection and the states,
/// the window text on the window for every scrollbar, and the highlight as the focus ring where it shows on the
/// window (3:1).
/// </summary>
internal static class HostingShapedPalettes
{
    public static TheoryData<string> Names => new() { "Aquatic", "Desert", "Dusk", "Night sky" };

    public static StandardThemeTokens Named(string name) => name switch
    {
        "Aquatic" => Create(name, 0x202020, 0xFFFFFF, 0x8EE3F0, 0x263B50),
        "Desert" => Create(name, 0xFFFAEF, 0x3D3D3D, 0x903909, 0xFFF5E3),
        "Dusk" => Create(name, 0x2D3236, 0xFFFFFF, 0xA1BFDE, 0x212D3B),
        "Night sky" => Create(name, 0x000000, 0xFFFFFF, 0xD6B4FD, 0x2B2B2B),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    /// <summary>A palette from a window pair and a highlight pair, given as 0xRRGGBB.</summary>
    public static StandardThemeTokens Create(string name, uint window, uint windowText, uint highlight, uint highlightText)
    {
        BColor w = Rgb(window);
        BColor t = Rgb(windowText);
        BColor h = Rgb(highlight);
        BColor ht = Rgb(highlightText);
        bool dark = StandardContrast.RelativeLuminance(w) < StandardContrast.RelativeLuminance(t);
        StandardThemeTokens preset = dark ? StandardThemeTokens.HighContrastDark : StandardThemeTokens.HighContrastLight;
        return preset with
        {
            Name = "HighContrastSystem " + name,
            IsDark = dark,
            Surface = w,
            SurfaceAlt = w,
            SurfaceDisabled = w,
            Border = t,
            BorderStrong = t,
            Text = t,
            TextMuted = t,
            Accent = h,
            AccentHover = h,
            AccentPressed = h,
            AccentSoft = h,
            OnAccent = ht,
            AccentText = ht == t ? t : StandardContrast.Ratio(h, w) >= StandardContrast.AaNormalText ? h : t,
            SelectionText = ht,
            SelectionTextMuted = ht,
            StateFill = h,
            StateText = ht,
            ScrollbarTrack = w,
            ScrollbarThumb = t,
            FocusRing = StandardContrast.Ratio(h, w) >= StandardContrast.AaLargeOrUi ? h : t,
            IsHighContrast = true,
        };
    }

    private static BColor Rgb(uint rgb) => BColor.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
