using System;
using Broiler.Graphics.Text;

namespace Broiler.UI.Standard;

/// <summary>The type roles of a theme. Text in a role shows that role's font from the active theme.</summary>
public enum StandardTextStyle
{
    /// <summary>Running text and control text.</summary>
    Body,
    /// <summary>The main heading of a surface, such as a message subject.</summary>
    Title,
    /// <summary>A section heading.</summary>
    Subtitle,
    /// <summary>Secondary, smaller text.</summary>
    Caption,
    /// <summary>Monospaced text.</summary>
    Code,
}

/// <summary>
/// How standard controls take their fonts from the theme. A control starts with the active theme's
/// font for its role and follows later themes, including a text-scaled theme, until the application
/// sets a font of its own, which is then kept.
/// </summary>
public static class StandardThemeFonts
{
    /// <summary>The font <paramref name="theme"/> gives <paramref name="style"/>.</summary>
    public static BFontStyle For(StandardThemeTokens theme, StandardTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return style switch
        {
            StandardTextStyle.Title => theme.FontTitle,
            StandardTextStyle.Subtitle => theme.FontSubtitle,
            StandardTextStyle.Caption => theme.FontCaption,
            StandardTextStyle.Code => theme.FontCode,
            _ => theme.FontBody,
        };
    }

    /// <summary>
    /// The font after a theme change: the new theme's font while the control still shows the font the
    /// previous theme gave it (<paramref name="previousThemeFont"/>), otherwise the application's own.
    /// </summary>
    public static BFontStyle Follow(BFontStyle current, BFontStyle previousThemeFont, BFontStyle themeFont) =>
        current == previousThemeFont ? themeFont : current;
}
