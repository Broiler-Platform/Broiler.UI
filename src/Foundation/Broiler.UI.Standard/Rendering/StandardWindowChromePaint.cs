using System;
using Broiler.Graphics;

namespace Broiler.UI.Standard;

/// <summary>Which system-button glyph to draw in owner-drawn window chrome.</summary>
public enum StandardWindowChromeGlyph
{
    Minimize,
    Maximize,
    Restore,
    Close,
}

/// <summary>
/// Draws the owner-drawn window title bar: its background, and the minimize, maximize/restore and
/// close glyphs. Neutral by design — it takes rectangles and colors, not window objects — so both
/// the window and the dialog implementations paint identical chrome from the same code while
/// staying in their own assemblies.
/// </summary>
/// <remarks>
/// The render list has no line or path primitive, so every glyph is built from rectangles: the
/// close cross is two bars drawn under a rotation transform.
/// </remarks>
public static class StandardWindowChromePaint
{
    /// <summary>Fills the title-bar strip, rounding only its top corners against the window frame.</summary>
    public static void FillTitleBar(BRenderList renderList, BRect titleBar, BColor background, double cornerRadius)
    {
        ArgumentNullException.ThrowIfNull(renderList);
        if (titleBar.IsEmpty || background.IsEmpty || background.A == 0)
            return;

        double radius = StandardControlPaint.ResolveRadius(titleBar, cornerRadius);
        if (radius <= 0)
        {
            renderList.FillRect(titleBar, background);
            return;
        }

        // A rounded rect plus a square patch over its lower half: the top corners follow the
        // window frame, the bottom edge stays flush with the content.
        renderList.FillRoundedRect(titleBar, background, radius, radius);
        double patchHeight = Math.Min(radius, titleBar.Height);
        renderList.FillRect(
            new BRect(titleBar.Left, titleBar.Bottom - patchHeight, titleBar.Width, patchHeight),
            background);
    }

    /// <summary>
    /// Draws one system button: its hover/pressed background when
    /// <paramref name="background"/> is not transparent, then its glyph.
    /// </summary>
    public static void DrawButton(
        BRenderList renderList,
        BRect button,
        StandardWindowChromeGlyph glyph,
        BColor glyphColor,
        BColor background)
    {
        ArgumentNullException.ThrowIfNull(renderList);
        if (button.IsEmpty)
            return;

        if (!background.IsEmpty && background.A > 0)
            renderList.FillRect(button, background);

        DrawGlyph(renderList, button, glyph, glyphColor);
    }

    /// <summary>
    /// Draws one system button, resolving its hover/pressed background and the glyph color that
    /// stays readable on it. The common overload: implementations pass their interaction state and
    /// the color the title text uses.
    /// </summary>
    public static void DrawButton(
        BRenderList renderList,
        BRect button,
        StandardWindowChromeGlyph glyph,
        bool isHot,
        bool isPressed,
        BColor normalGlyphColor)
    {
        if (button.IsEmpty)
            return;

        DrawButton(
            renderList,
            button,
            glyph,
            ResolveGlyphColor(glyph, isHot, isPressed, normalGlyphColor),
            ResolveButtonBackground(glyph, isHot, isPressed));
    }

    /// <summary>
    /// Draws the window title, vertically centered in <paramref name="bounds"/> and clipped to it,
    /// with a trailing ellipsis when it does not fit.
    /// </summary>
    public static void DrawTitleText(BRenderList renderList, BRect bounds, string? title, BFontStyle font, BColor color)
    {
        ArgumentNullException.ThrowIfNull(renderList);
        if (bounds.IsEmpty || string.IsNullOrWhiteSpace(title) || color.IsEmpty || color.A == 0)
            return;

        string text = Ellipsize(title, font, bounds.Width);
        if (text.Length == 0)
            return;

        double lineHeight = BTextMeasurer.GetLineHeight(font);
        double y = bounds.Top + Math.Max(0, (bounds.Height - lineHeight) / 2);

        renderList.PushClip(bounds);
        renderList.DrawText(new BTextRun(text, font, color), new BPoint(bounds.Left, y));
        renderList.PopClip();
    }

    /// <summary>Draws the window icon into <paramref name="bounds"/>, scaled to fit.</summary>
    public static void DrawIcon(BRenderList renderList, BRect bounds, BImageHandle icon)
    {
        ArgumentNullException.ThrowIfNull(renderList);
        if (bounds.IsEmpty || !icon.IsValid || icon.PixelSize.IsEmpty)
            return;

        renderList.DrawImage(icon, new BRect(0, 0, icon.PixelSize.Width, icon.PixelSize.Height), bounds);
    }

    /// <summary>Draws a system-button glyph centered in <paramref name="button"/>.</summary>
    public static void DrawGlyph(BRenderList renderList, BRect button, StandardWindowChromeGlyph glyph, BColor color)
    {
        ArgumentNullException.ThrowIfNull(renderList);
        if (button.IsEmpty || color.IsEmpty || color.A == 0)
            return;

        // A 10x10 glyph box is what Windows uses at 100%; scale it down inside a tighter button.
        double extent = Math.Min(10, Math.Min(button.Width, button.Height) - 8);
        if (extent < 4)
            return;

        double centerX = Math.Round(button.Left + (button.Width / 2));
        double centerY = Math.Round(button.Top + (button.Height / 2));
        double half = Math.Round(extent / 2);

        switch (glyph)
        {
            case StandardWindowChromeGlyph.Minimize:
                renderList.FillRect(new BRect(centerX - half, centerY, half * 2, 1), color);
                break;

            case StandardWindowChromeGlyph.Maximize:
                renderList.StrokeRect(new BRect(centerX - half, centerY - half, half * 2, half * 2), color, 1);
                break;

            case StandardWindowChromeGlyph.Restore:
                // The "restore down" pair: a back square peeking out behind a front one.
                renderList.StrokeRect(new BRect(centerX - half + 2, centerY - half, (half * 2) - 2, (half * 2) - 2), color, 1);
                renderList.StrokeRect(new BRect(centerX - half, centerY - half + 2, (half * 2) - 2, (half * 2) - 2), color, 1);
                break;

            case StandardWindowChromeGlyph.Close:
                DrawCross(renderList, centerX, centerY, half, color);
                break;
        }
    }

    /// <summary>
    /// The hover/pressed background for a system button. Close goes red the way every desktop
    /// close button does; the others tint with the surrounding surface.
    /// </summary>
    public static BColor ResolveButtonBackground(StandardWindowChromeGlyph glyph, bool isHot, bool isPressed)
    {
        if (!isHot && !isPressed)
            return BColor.Transparent;

        if (glyph == StandardWindowChromeGlyph.Close)
        {
            BColor danger = StandardControlPaint.Danger;
            return isPressed ? Blend(danger, BColor.Black, 0.25) : danger;
        }

        BColor tint = StandardControlPaint.Theme.IsDark ? BColor.White : BColor.Black;
        return BColor.FromArgb((byte)(isPressed ? 46 : 26), tint.R, tint.G, tint.B);
    }

    /// <summary>The glyph color that stays readable on <paramref name="background"/>.</summary>
    public static BColor ResolveGlyphColor(StandardWindowChromeGlyph glyph, bool isHot, bool isPressed, BColor normal) =>
        glyph == StandardWindowChromeGlyph.Close && (isHot || isPressed)
            ? StandardControlPaint.Theme.OnAccent
            : normal;

    private static string Ellipsize(string text, BFontStyle font, double maxWidth)
    {
        if (maxWidth <= 0)
            return string.Empty;
        if (BTextMeasurer.MeasureAdvance(text, font) <= maxWidth)
            return text;

        const string Ellipsis = "…";
        for (int length = text.Length - 1; length > 0; length--)
        {
            string candidate = text[..length] + Ellipsis;
            if (BTextMeasurer.MeasureAdvance(candidate, font) <= maxWidth)
                return candidate;
        }

        return string.Empty;
    }

    private static void DrawCross(BRenderList renderList, double centerX, double centerY, double half, BColor color)
    {
        const double thickness = 1;
        double length = half * 2 * Math.Sqrt(2) / 2 * 1.15;

        DrawRotatedBar(renderList, centerX, centerY, length, thickness, Math.PI / 4, color);
        DrawRotatedBar(renderList, centerX, centerY, length, thickness, -Math.PI / 4, color);
    }

    private static void DrawRotatedBar(
        BRenderList renderList,
        double centerX,
        double centerY,
        double length,
        double thickness,
        double angle,
        BColor color)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        // Rotate about (centerX, centerY): p' = (p - c) * R + c.
        var rotation = new BMatrix3x2(
            cos,
            sin,
            -sin,
            cos,
            centerX - ((centerX * cos) - (centerY * sin)),
            centerY - ((centerX * sin) + (centerY * cos)));

        renderList.PushTransform(rotation);
        renderList.FillRect(
            new BRect(centerX - (length / 2), centerY - (thickness / 2), length, thickness),
            color);
        renderList.PopTransform();
    }

    private static BColor Blend(BColor from, BColor to, double amount)
    {
        double clamped = Math.Clamp(amount, 0, 1);
        return BColor.FromArgb(
            from.A,
            (byte)Math.Round(from.R + ((to.R - from.R) * clamped)),
            (byte)Math.Round(from.G + ((to.G - from.G) * clamped)),
            (byte)Math.Round(from.B + ((to.B - from.B) * clamped)));
    }
}
