using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// One laid-out line. Its alignment offset is what the paragraph's alignment
/// adds to the line's left origin, resolved at layout time because it depends
/// only on what layout already knows: the document, the content width, and
/// the font.
/// </summary>
internal readonly record struct VisualLine(
    int ParagraphIndex,
    int Start,
    int End,
    double Top,
    double Height,
    double AlignmentOffset,
    double WordSpacing = 0);

/// <summary>
/// The box a paragraph is laid out in: its left offset from the text column's
/// left edge, and the width it wraps into. An ordinary paragraph gets the whole
/// content column; one in a table gets its cell.
/// </summary>
internal readonly record struct CellFrame(double Left, double Width);

/// <summary>One table cell's box, in the same space a line's top is measured in.</summary>
internal readonly record struct CellBox(BRect Bounds, BColor Shading, CellBorders Borders);

/// <summary>
/// What a paragraph's list and indent add to it: the marker drawn in the
/// gutter — empty when the paragraph is not a list item — the font that marker
/// is drawn with, and the left offsets of the marker and of the text. Both
/// offsets are relative to the left edge of the paragraph's own
/// <see cref="CellFrame"/> — not to the text column — so scrolling the document
/// or moving the control does not invalidate them, and a paragraph in a table
/// cell carries the cell's offset exactly once.
/// </summary>
internal readonly record struct ParagraphDecoration(
    string Marker,
    BFontStyle Font,
    double MarkerIndent,
    double TextIndent)
{
    public static ParagraphDecoration None => new(string.Empty, BFontStyle.Default, 0, 0);
}

/// <summary>
/// A stretch of a visual line as it is drawn. <see cref="Image"/> is set only
/// for a picture, and then <see cref="Text"/> is the placeholder character it
/// occupies rather than anything to draw as glyphs. <see cref="Text"/> is empty
/// for a tab: it carries width and style but has nothing to draw.
/// </summary>
internal readonly record struct LineSegment(
    string Text,
    InlineStyle Style,
    BFontStyle Font,
    double X,
    double Advance,
    InlineImage? Image = null)
{
    public static LineSegment ForImage(InlineImage image, InlineStyle style, double x, double width) =>
        new(InlineImage.PlaceholderText, style, BFontStyle.Default, x, width, image);

    public static LineSegment ForTab(InlineStyle style, BFontStyle font, double x, double width) =>
        new(string.Empty, style, font, x, width);
}
