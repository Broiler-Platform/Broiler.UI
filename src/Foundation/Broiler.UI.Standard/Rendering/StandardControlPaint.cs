using System;
using System.Runtime.CompilerServices;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;

namespace Broiler.UI.Standard;

/// <summary>
/// Shared drawing helpers plus the resolved semantic palette that standard
/// controls paint with. The color and radius members are single-sourced from the
/// active <see cref="StandardThemeTokens"/> set, so applying a different theme
/// (e.g. <see cref="StandardThemeTokens.Dark"/>) re-colors every control that
/// reads these roles.
/// </summary>
public static class StandardControlPaint
{
    private static readonly ConditionalWeakTable<UiSession, StandardThemeTokens> _sessionThemes = new();
    private static StandardThemeTokens _theme = StandardThemeTokens.Light;

    /// <summary>The active palette that the role accessors below resolve against.</summary>
    public static StandardThemeTokens Theme => _theme;

    /// <summary>
    /// Selects the active global palette. Controls capture these role colors when constructed.
    /// </summary>
    public static void ApplyTheme(StandardThemeTokens theme) =>
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));

    /// <summary>
    /// Sets a theme scoped to a specific <see cref="UiSession"/>, ensuring thread-safe isolation across windows.
    /// </summary>
    public static void SetSessionTheme(UiSession session, StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(theme);
        _sessionThemes.AddOrUpdate(session, theme);
    }

    /// <summary>
    /// Gets the theme for a session, falling back to the global active theme if not explicitly set.
    /// </summary>
    public static StandardThemeTokens GetTheme(UiSession? session)
    {
        if (session is not null && _sessionThemes.TryGetValue(session, out StandardThemeTokens? tokens))
            return tokens;
        return _theme;
    }

    /// <summary>
    /// Gets the theme for an element's session, falling back to the global active theme.
    /// </summary>
    public static StandardThemeTokens GetTheme(UiElement? element) =>
        GetTheme(element?.Session);

    /// <summary>
    /// Clears any session-scoped theme for the specified <see cref="UiSession"/>.
    /// </summary>
    public static bool ClearSessionTheme(UiSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return _sessionThemes.Remove(session);
    }

    // Surfaces
    public static BColor Surface => _theme.Surface;
    public static BColor SurfaceAlt => _theme.SurfaceAlt;
    public static BColor SurfaceDisabled => _theme.SurfaceDisabled;

    // Borders
    public static BColor Border => _theme.Border;
    public static BColor BorderStrong => _theme.BorderStrong;

    // Text
    public static BColor Text => _theme.Text;
    public static BColor TextMuted => _theme.TextMuted;
    public static BColor TextDisabled => _theme.TextDisabled;

    // Accent
    public static BColor Accent => _theme.Accent;
    public static BColor AccentHover => _theme.AccentHover;
    public static BColor AccentPressed => _theme.AccentPressed;
    public static BColor AccentSoft => _theme.AccentSoft;
    public static BColor OnAccent => _theme.OnAccent;
    public static BColor AccentText => _theme.AccentText;

    // Selection
    public static BColor SelectionText => _theme.SelectionText;
    public static BColor SelectionTextMuted => _theme.SelectionTextMuted;

    // State
    public static BColor StateFill => _theme.StateFill;
    public static BColor StateText => _theme.StateText;

    // Scrollbars
    public static BColor ScrollbarTrack => _theme.ScrollbarTrack;
    public static BColor ScrollbarThumb => _theme.ScrollbarThumb;

    // Focus
    public static BColor Focus => _theme.FocusRing;
    public static double FocusRingThickness => _theme.FocusRingThickness;
    public static double FocusRingOffset => _theme.FocusRingOffset;

    // Status
    public static BColor Success => _theme.Success;
    public static BColor Warning => _theme.Warning;
    public static BColor Danger => _theme.Danger;
    public static BColor Info => _theme.Info;

    // Radii
    public static double ControlRadius => _theme.ControlRadius;
    public static double SmallRadius => _theme.SmallRadius;
    public static double PillRadius => _theme.PillRadius;

    // Typography
    public static string FontFamily => _theme.FontFamily;
    public static BFontStyle FontBody => _theme.FontBody;
    public static BFontStyle FontTitle => _theme.FontTitle;
    public static BFontStyle FontSubtitle => _theme.FontSubtitle;
    public static BFontStyle FontCaption => _theme.FontCaption;
    public static BFontStyle FontCode => _theme.FontCode;

    // Spacing
    public static double SpacingXs => _theme.SpacingXs;
    public static double SpacingSm => _theme.SpacingSm;
    public static double SpacingMd => _theme.SpacingMd;
    public static double SpacingLg => _theme.SpacingLg;
    public static double SpacingXl => _theme.SpacingXl;
    public static double SpacingXxl => _theme.SpacingXxl;
    public static double Spacing(int step) => _theme.Spacing(step);

    // Density
    public static UiDensity Density => _theme.Density;
    public static double DensityFactor => _theme.DensityFactor;
    public static double ResolveRowHeight(double baseHeight) => _theme.ResolveRowHeight(baseHeight);
    public static (double Horizontal, double Vertical) ResolvePadding(double baseHorizontal, double baseVertical) => _theme.ResolvePadding(baseHorizontal, baseVertical);
    public static double ResolvePadding(double basePadding) => _theme.ResolvePadding(basePadding);

    // Motion
    public static bool ReducedMotion => _theme.ReducedMotion;
    public static TimeSpan AnimationDurationFast => _theme.AnimationDurationFast;
    public static TimeSpan AnimationDurationNormal => _theme.AnimationDurationNormal;
    public static TimeSpan AnimationDurationSlow => _theme.AnimationDurationSlow;

    public static void FillRounded(BRenderList renderList, BRect rect, BColor color, double radius)
    {
        if (rect.IsEmpty || color.IsEmpty || color.A == 0)
            return;

        double resolved = ResolveRadius(rect, radius);
        renderList.FillRoundedRect(rect, color, resolved, resolved);
    }

    public static void StrokeRounded(BRenderList renderList, BRect rect, BColor color, double radius, double thickness = 1)
    {
        if (rect.IsEmpty || color.IsEmpty || color.A == 0 || thickness <= 0)
            return;

        double resolved = ResolveRadius(rect, radius);
        renderList.StrokeRoundedRect(rect, color, resolved, resolved, thickness);
    }

    public static void DrawFocusRing(BRenderList renderList, BRect rect, double radius) =>
        DrawFocusRing(renderList, rect, radius, _theme);

    public static void DrawFocusRing(BRenderList renderList, BRect rect, double radius, StandardThemeTokens theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        double offset = theme.FocusRingOffset;
        double thickness = theme.FocusRingThickness;
        BRect focus = Inset(rect, offset);
        if (!focus.IsEmpty)
            StrokeRounded(renderList, focus, theme.FocusRing, Math.Max(0, radius - offset), thickness);
    }

    /// <summary>
    /// The color to stroke a focus ring in when it is drawn on <paramref name="fill"/>: <paramref name="ring"/>
    /// where it stands out from the fill (3:1, as a focus indicator needs), and otherwise <paramref name="label"/>,
    /// the color the control draws its label in on that fill, which the theme chose to be read there. A palette
    /// built from a system highlight pair can use the highlight for the ring and for the fill, and a preset's ring
    /// can be its accent, the fill of a default button; the ring would not show. A fill that is not opaque keeps the
    /// ring: what shows through it is not known.
    /// </summary>
    /// <remarks>
    /// The rule every standard control that draws its ring on a fill of its own follows (ADR 0032): a button or
    /// toggle button on the fill it draws in its current state, a spin box on its field, a tab view on its selected
    /// header.
    /// </remarks>
    public static BColor FocusRingColor(BColor ring, BColor fill, BColor label) =>
        fill.A == 255 && StandardContrast.Ratio(ring, fill) < StandardContrast.AaLargeOrUi ? label : ring;

    /// <summary>
    /// The track and thumb a control with scrollbar colors of its own draws under <paramref name="theme"/>: the theme's
    /// <see cref="StandardThemeTokens.ScrollbarTrack"/> and <see cref="StandardThemeTokens.ScrollbarThumb"/> once the
    /// theme gives scrollbars colors, and otherwise <paramref name="track"/> and <paramref name="thumb"/>, the control's
    /// own. A theme gives them colors when it is high contrast, or when either role differs from the role it follows
    /// (<see cref="StandardThemeTokens.SurfaceDisabled"/> and <see cref="StandardThemeTokens.BorderStrong"/>).
    /// </summary>
    /// <remarks>
    /// The rule for the scroll view, the rich edit and the formatting code view, whose translucent bars are older than
    /// the roles and are kept where the theme says nothing about scrollbars, so the Light and Dark presets draw them as
    /// before (ADR 0033). The list, the tree and the code editor draw the roles in every theme; without a value of their
    /// own the roles are the colors those controls always drew.
    /// </remarks>
    public static (BColor Track, BColor Thumb) ScrollbarColors(StandardThemeTokens theme, BColor track, BColor thumb)
    {
        ArgumentNullException.ThrowIfNull(theme);
        bool themed = theme.IsHighContrast ||
            theme.ScrollbarTrack != theme.SurfaceDisabled ||
            theme.ScrollbarThumb != theme.BorderStrong;
        return themed ? (theme.ScrollbarTrack, theme.ScrollbarThumb) : (track, thumb);
    }


    public static BRect Inset(BRect rect, double amount) =>
        new(
            rect.Left + amount,
            rect.Top + amount,
            Math.Max(0, rect.Width - amount * 2),
            Math.Max(0, rect.Height - amount * 2));

    public static double ResolveRadius(BRect rect, double radius)
    {
        double max = Math.Max(0, Math.Min(rect.Width, rect.Height) / 2);
        if (radius >= PillRadius)
            return max;

        return Math.Clamp(radius, 0, max);
    }
}
