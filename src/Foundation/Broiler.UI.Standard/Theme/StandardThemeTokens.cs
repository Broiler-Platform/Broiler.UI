using System;
using System.Diagnostics.CodeAnalysis;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Text;

namespace Broiler.UI.Standard;

/// <summary>
/// A complete platform-neutral semantic color and metric palette — a "design
/// token set". Controls consume these role tokens through
/// <see cref="StandardControlPaint"/> rather than hardcoding colors, so an entire
/// theme (light, dark, high-contrast) can be swapped by changing the active token
/// set. Every role is <c>required</c>, so a preset that omits a token fails to
/// compile rather than silently rendering a transparent value.
/// </summary>
public sealed record StandardThemeTokens
{
    // Surfaces
    public required BColor Surface { get; init; }
    public required BColor SurfaceAlt { get; init; }
    public required BColor SurfaceDisabled { get; init; }

    // Borders / strokes
    public required BColor Border { get; init; }
    public required BColor BorderStrong { get; init; }

    // Text
    public required BColor Text { get; init; }
    public required BColor TextMuted { get; init; }
    public required BColor TextDisabled { get; init; }

    // Accent
    public required BColor Accent { get; init; }
    public required BColor AccentHover { get; init; }
    public required BColor AccentPressed { get; init; }
    public required BColor AccentSoft { get; init; }

    /// <summary>Text/icon color drawn on top of an accent fill (e.g. a primary button label).</summary>
    public required BColor OnAccent { get; init; }

    // Focus
    public required BColor FocusRing { get; init; }
    public double FocusRingThickness { get; init; } = 1;
    public double FocusRingOffset { get; init; } = 2;

    // Status
    public required BColor Success { get; init; }
    public required BColor Warning { get; init; }
    public required BColor Danger { get; init; }
    public required BColor Info { get; init; }

    // Metrics (theme-invariant defaults; a theme may override)
    public double ControlRadius { get; init; } = 6;
    public double SmallRadius { get; init; } = 4;
    public double PillRadius { get; init; } = 999;

    // Typography. Body is the size controls have always drawn (BFontStyle.Default); the others are
    // ranked around it: a surface title, a section heading (as FormSection has drawn it), and a caption.
    public string FontFamily { get; init; } = BFontStyle.Default.FamilyName;
    public BFontStyle FontBody { get; init; } = BFontStyle.Default;
    public BFontStyle FontTitle { get; init; } = BFontStyle.Default with { Size = 24, Weight = BFontWeight.SemiBold };
    public BFontStyle FontSubtitle { get; init; } = BFontStyle.Default with { Size = 20 };
    public BFontStyle FontCaption { get; init; } = BFontStyle.Default with { Size = 13 };
    public BFontStyle FontCode { get; init; } = new BFontStyle("Consolas", 13);

    /// <summary>The system text size these fonts are scaled to; 1 is unscaled. See <see cref="WithTextScale"/>.</summary>
    public double TextScale { get; init; } = 1.0;

    /// <summary>
    /// A copy whose fonts are <paramref name="scale"/> times their unscaled sizes, for the system's text
    /// size setting (Windows "Make text bigger"). Scaling an already scaled theme does not compound.
    /// </summary>
    public StandardThemeTokens WithTextScale(double scale)
    {
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Text scale must be a positive, finite number.");
        double factor = scale / TextScale;
        if (Math.Abs(factor - 1) < 1e-9)
            return this;

        BFontStyle Scale(BFontStyle font) => font with { Size = Math.Round(font.Size * factor * 100) / 100 };
        return this with
        {
            TextScale = scale,
            FontBody = Scale(FontBody),
            FontTitle = Scale(FontTitle),
            FontSubtitle = Scale(FontSubtitle),
            FontCaption = Scale(FontCaption),
            FontCode = Scale(FontCode),
        };
    }

    // Spacing
    public double SpacingXs { get; init; } = 4;
    public double SpacingSm { get; init; } = 8;
    public double SpacingMd { get; init; } = 12;
    public double SpacingLg { get; init; } = 16;
    public double SpacingXl { get; init; } = 24;
    public double SpacingXxl { get; init; } = 32;

    public double Spacing(int step) =>
        step switch
        {
            <= 0 => 0,
            1 => SpacingXs,
            2 => SpacingSm,
            3 => SpacingMd,
            4 => SpacingLg,
            5 => SpacingXl,
            _ => SpacingXxl + (step - 6) * SpacingSm,
        };

    // Density
    public UiDensity Density { get; init; } = UiDensity.Comfortable;

    public double DensityFactor =>
        Density switch
        {
            UiDensity.Compact => 0.8,
            UiDensity.Spacious => 1.25,
            _ => 1.0,
        };

    public double ResolveRowHeight(double baseHeight) =>
        Math.Round(baseHeight * DensityFactor);

    public (double Horizontal, double Vertical) ResolvePadding(double baseHorizontal, double baseVertical) =>
        (Math.Round(baseHorizontal * DensityFactor), Math.Round(baseVertical * DensityFactor));

    public double ResolvePadding(double basePadding) =>
        Math.Round(basePadding * DensityFactor);

    // Motion policy
    public bool ReducedMotion { get; init; } = false;

    public TimeSpan AnimationDurationFast =>
        ReducedMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(100);

    public TimeSpan AnimationDurationNormal =>
        ReducedMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(200);

    public TimeSpan AnimationDurationSlow =>
        ReducedMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(350);

    // Contrast measurements
    public double TextContrast => StandardContrast.Ratio(Text, Surface);
    public double TextMutedContrast => StandardContrast.Ratio(TextMuted, Surface);
    public double AccentContrast => StandardContrast.Ratio(OnAccent, Accent);
    public double FocusRingContrast => StandardContrast.Ratio(FocusRing, Surface);
    public bool MeetsAaNormalText => StandardContrast.Meets(Text, Surface, StandardContrast.AaNormalText);
    public bool MeetsAaLargeOrUi => StandardContrast.Meets(BorderStrong, Surface, StandardContrast.AaLargeOrUi);

    // Metadata
    public bool IsDark { get; init; }
    public string Name { get; init; } = "Custom";

    // Back-compat aliases for the original four-token shape.
    public BColor Background => Surface;
    public BColor Foreground => Text;
    public BColor Focus => FocusRing;

    /// <summary>
    /// Legacy four-color constructor. Preserved for existing call sites and tests;
    /// the remaining roles are derived from the four supplied colors. New code
    /// should use a named preset or an object initializer.
    /// </summary>
    [SetsRequiredMembers]
    public StandardThemeTokens(BColor background, BColor foreground, BColor accent, BColor focusRing)
    {
        Surface = background;
        SurfaceAlt = background;
        SurfaceDisabled = background;
        Border = foreground;
        BorderStrong = foreground;
        Text = foreground;
        TextMuted = foreground;
        TextDisabled = foreground;
        Accent = accent;
        AccentHover = accent;
        AccentPressed = accent;
        AccentSoft = background;
        OnAccent = background;
        FocusRing = focusRing;
        Success = accent;
        Warning = accent;
        Danger = accent;
        Info = accent;
        Name = "Custom";
    }

    private StandardThemeTokens()
    {
    }

    /// <summary>The default light palette.</summary>
    public static StandardThemeTokens Default => Light;

    /// <summary>Light theme — the historical Broiler.UI palette, now fully tokenized.</summary>
    public static StandardThemeTokens Light { get; } = new()
    {
        Surface = BColor.White,
        SurfaceAlt = BColor.FromArgb(0xFF, 0xF8, 0xFA, 0xFD),
        SurfaceDisabled = BColor.FromArgb(0xFF, 0xF1, 0xF4, 0xF8),
        Border = BColor.FromArgb(0xFF, 0xC9, 0xD4, 0xE1),
        BorderStrong = BColor.FromArgb(0xFF, 0x8D, 0xA0, 0xB6),
        Text = BColor.FromArgb(0xFF, 0x14, 0x24, 0x3A),
        TextMuted = BColor.FromArgb(0xFF, 0x5B, 0x6B, 0x82),
        TextDisabled = BColor.FromArgb(0xFF, 0x93, 0x9E, 0xAD),
        Accent = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8),
        AccentHover = BColor.FromArgb(0xFF, 0x0A, 0x61, 0xBE),
        AccentPressed = BColor.FromArgb(0xFF, 0x08, 0x4C, 0x98),
        AccentSoft = BColor.FromArgb(0xFF, 0xE7, 0xF0, 0xFF),
        OnAccent = BColor.White,
        FocusRing = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8),
        Success = BColor.FromArgb(0xFF, 0x0F, 0x7B, 0x0F),
        Warning = BColor.FromArgb(0xFF, 0x8A, 0x5A, 0x00),
        Danger = BColor.FromArgb(0xFF, 0xB4, 0x23, 0x1A),
        Info = BColor.FromArgb(0xFF, 0x0B, 0x6F, 0xD8),
        IsDark = false,
        Name = "Light",
    };

    /// <summary>Dark theme — WCAG AA text/accent contrast on dark surfaces.</summary>
    public static StandardThemeTokens Dark { get; } = new()
    {
        Surface = BColor.FromArgb(0xFF, 0x20, 0x20, 0x24),
        SurfaceAlt = BColor.FromArgb(0xFF, 0x2A, 0x2A, 0x30),
        SurfaceDisabled = BColor.FromArgb(0xFF, 0x2E, 0x2E, 0x34),
        Border = BColor.FromArgb(0xFF, 0x3C, 0x40, 0x48),
        BorderStrong = BColor.FromArgb(0xFF, 0x5C, 0x62, 0x70),
        Text = BColor.FromArgb(0xFF, 0xF2, 0xF4, 0xF8),
        TextMuted = BColor.FromArgb(0xFF, 0x9A, 0xA4, 0xB4),
        TextDisabled = BColor.FromArgb(0xFF, 0x6B, 0x72, 0x80),
        Accent = BColor.FromArgb(0xFF, 0x26, 0x73, 0xCE),
        AccentHover = BColor.FromArgb(0xFF, 0x3A, 0x85, 0xDE),
        AccentPressed = BColor.FromArgb(0xFF, 0x1B, 0x5E, 0xAF),
        AccentSoft = BColor.FromArgb(0xFF, 0x17, 0x32, 0x4E),
        OnAccent = BColor.White,
        FocusRing = BColor.FromArgb(0xFF, 0x7A, 0xB7, 0xFF),
        Success = BColor.FromArgb(0xFF, 0x5B, 0xC8, 0x73),
        Warning = BColor.FromArgb(0xFF, 0xE0, 0xA7, 0x2E),
        Danger = BColor.FromArgb(0xFF, 0xF1, 0x70, 0x7A),
        Info = BColor.FromArgb(0xFF, 0x7A, 0xB7, 0xFF),
        IsDark = true,
        Name = "Dark",
    };

    /// <summary>High-contrast light theme — pure black on white with vivid accents.</summary>
    public static StandardThemeTokens HighContrastLight { get; } = new()
    {
        Surface = BColor.White,
        SurfaceAlt = BColor.White,
        SurfaceDisabled = BColor.White,
        Border = BColor.Black,
        BorderStrong = BColor.Black,
        Text = BColor.Black,
        TextMuted = BColor.Black,
        TextDisabled = BColor.FromArgb(0xFF, 0x6E, 0x6E, 0x6E),
        Accent = BColor.FromArgb(0xFF, 0x00, 0x00, 0xCC),
        AccentHover = BColor.FromArgb(0xFF, 0x00, 0x00, 0x99),
        AccentPressed = BColor.FromArgb(0xFF, 0x00, 0x00, 0x66),
        AccentSoft = BColor.FromArgb(0xFF, 0xEA, 0xEA, 0xFF),
        OnAccent = BColor.White,
        FocusRing = BColor.Black,
        FocusRingThickness = 2,
        Success = BColor.FromArgb(0xFF, 0x00, 0x60, 0x00),
        Warning = BColor.FromArgb(0xFF, 0x6E, 0x4A, 0x00),
        Danger = BColor.FromArgb(0xFF, 0xA4, 0x00, 0x00),
        Info = BColor.FromArgb(0xFF, 0x00, 0x00, 0xCC),
        IsDark = false,
        Name = "HighContrastLight",
    };

    /// <summary>High-contrast dark theme — pure white on black with vivid accents.</summary>
    public static StandardThemeTokens HighContrastDark { get; } = new()
    {
        Surface = BColor.Black,
        SurfaceAlt = BColor.Black,
        SurfaceDisabled = BColor.Black,
        Border = BColor.White,
        BorderStrong = BColor.White,
        Text = BColor.White,
        TextMuted = BColor.White,
        TextDisabled = BColor.FromArgb(0xFF, 0x9A, 0x9A, 0x9A),
        Accent = BColor.FromArgb(0xFF, 0x00, 0xE0, 0xFF),
        AccentHover = BColor.FromArgb(0xFF, 0x66, 0xED, 0xFF),
        AccentPressed = BColor.FromArgb(0xFF, 0x00, 0xB4, 0xCC),
        AccentSoft = BColor.FromArgb(0xFF, 0x00, 0x33, 0x3A),
        OnAccent = BColor.Black,
        FocusRing = BColor.FromArgb(0xFF, 0xFF, 0xFF, 0x00),
        FocusRingThickness = 2,
        Success = BColor.FromArgb(0xFF, 0x3F, 0xF2, 0x3F),
        Warning = BColor.FromArgb(0xFF, 0xFF, 0xD7, 0x00),
        Danger = BColor.FromArgb(0xFF, 0xFF, 0x60, 0x6A),
        Info = BColor.FromArgb(0xFF, 0x00, 0xE0, 0xFF),
        IsDark = true,
        Name = "HighContrastDark",
    };

    /// <summary>
    /// Selects a preset from the neutral system preferences: dark mode plus the
    /// user's contrast preference (<see cref="UiContrastPreference.More"/> routes
    /// to a high-contrast set).
    /// </summary>
    public static StandardThemeTokens Select(UiContrastPreference contrast, bool dark) =>
        contrast == UiContrastPreference.More
            ? (dark ? HighContrastDark : HighContrastLight)
            : (dark ? Dark : Light);

    /// <summary>
    /// Selects a preset configured with contrast, dark mode, density, and motion policy.
    /// </summary>
    public static StandardThemeTokens Select(
        UiContrastPreference contrast,
        bool dark,
        UiDensity density,
        bool reducedMotion = false)
    {
        StandardThemeTokens baseTokens = Select(contrast, dark);
        return baseTokens with
        {
            Density = density,
            ReducedMotion = reducedMotion,
        };
    }

    /// <summary>
    /// Selects a preset configured to match all preferences in <paramref name="settings"/>.
    /// </summary>
    public static StandardThemeTokens Select(UiSystemSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Select(
            settings.ContrastPreference,
            settings.ColorScheme == UiColorScheme.Dark,
            settings.Density,
            settings.ReducedMotion).WithTextScale(double.IsFinite(settings.TextScale) && settings.TextScale > 0 ? settings.TextScale : 1);
    }
}

