using System;
using System.Collections.Generic;
using Broiler.Documents.Model;
using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>A stretch of a run as it is drawn: the glyphs, and the font to draw them with.</summary>
internal readonly record struct ShapedPiece(string Text, BFontStyle Font);

/// <summary>
/// How stored text becomes the strings that are measured and drawn. Everything
/// here is a function of its arguments, so layout, painting, and hit testing can
/// share it without sharing any state.
/// </summary>
internal static class RichEditTextShaping
{
    /// <summary>How much smaller a small-caps letter is drawn than a full capital.</summary>
    public const double SmallCapsScale = 0.8;

    /// <summary>
    /// The pieces a stored substring is drawn as. Capitalization is a display
    /// property — the document keeps the casing the author typed, and this is the
    /// only place it becomes capitals, so turning it off restores the original
    /// text exactly. Small caps additionally splits the substring wherever the
    /// stored case changes, so letters typed in lower case can be drawn smaller
    /// than the ones typed as capitals.
    /// </summary>
    public static IEnumerable<ShapedPiece> ShapePieces(string text, InlineStyle style, BFontStyle font)
    {
        if (text.Length == 0)
            yield break;

        if (style.Capitalization == TextCapitalization.AllCaps)
        {
            yield return new ShapedPiece(text.ToUpperInvariant(), font);
            yield break;
        }

        if (style.Capitalization != TextCapitalization.SmallCaps)
        {
            yield return new ShapedPiece(text, font);
            yield break;
        }

        BFontStyle reduced = font with { Size = Math.Max(1, font.Size * SmallCapsScale) };
        int start = 0;
        bool small = char.IsLower(text[0]);
        for (int i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && char.IsLower(text[i]) == small)
                continue;

            string piece = text[start..i];
            yield return small
                ? new ShapedPiece(piece.ToUpperInvariant(), reduced)
                : new ShapedPiece(piece, font);

            if (i < text.Length)
            {
                start = i;
                small = char.IsLower(text[i]);
            }
        }
    }

    /// <summary>
    /// The advance of a stored substring as drawn. Every measurement goes through
    /// here so caret placement, selection rectangles, and wrapping agree with what
    /// the painter puts on screen.
    /// </summary>
    public static double MeasurePieces(string text, InlineStyle style, BFontStyle font)
    {
        if (style.Capitalization == TextCapitalization.None)
            return BTextMeasurer.MeasureAdvance(text, font);

        double advance = 0;
        foreach (ShapedPiece piece in ShapePieces(text, style, font))
            advance += BTextMeasurer.MeasureAdvance(piece.Text, piece.Font);

        return advance;
    }

    /// <summary>
    /// Splits a stretch of a paragraph into the pieces between its tab characters
    /// and the tabs themselves, so each piece is measured and drawn as one string
    /// and each tab is resolved against the tab stops instead.
    /// </summary>
    public static IEnumerable<(string Text, bool IsTab)> SplitTabs(string text, int start, int end)
    {
        int pieceStart = start;
        for (int i = start; i < end; i++)
        {
            if (text[i] != '\t')
                continue;

            if (i > pieceStart)
                yield return (text[pieceStart..i], false);

            yield return ("\t", true);
            pieceStart = i + 1;
        }

        if (pieceStart < end)
            yield return (text[pieceStart..end], false);
    }

    /// <summary>
    /// The stretches of a paragraph between its soft breaks. U+2028 (LINE
    /// SEPARATOR) is a soft break inside a paragraph; each one forces a new visual
    /// line and is not itself rendered.
    /// </summary>
    public static IEnumerable<(int Start, int End)> HardSegments(string text)
    {
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == (char)0x2028)
            {
                yield return (start, i);
                start = i + 1;
            }
        }

        yield return (start, text.Length);
    }

    /// <summary>
    /// Splits a piece so a justified line can be drawn one word at a time. Each
    /// chunk keeps the spaces that follow it, and the next chunk starts past the
    /// width those spaces were widened by, so the space glyph is still drawn at
    /// its own width. A line that is not justified yields its piece whole, so
    /// nothing about how it is drawn changes.
    /// </summary>
    public static IEnumerable<string> StretchChunks(string text, double wordSpacing)
    {
        if (wordSpacing == 0 || text.Length == 0)
        {
            yield return text;
            yield break;
        }

        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != ' ')
                continue;

            while (i + 1 < text.Length && text[i + 1] == ' ')
                i++;

            yield return text.Substring(start, i - start + 1);
            start = i + 1;
        }

        if (start < text.Length)
            yield return text.Substring(start);
    }

    public static int CountSpaces(string text, int start, int end)
    {
        int spaces = 0;
        for (int i = start; i < end; i++)
        {
            if (text[i] == ' ')
                spaces++;
        }

        return spaces;
    }
}
