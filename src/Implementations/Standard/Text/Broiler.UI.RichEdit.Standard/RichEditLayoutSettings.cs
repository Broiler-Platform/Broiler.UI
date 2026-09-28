using System;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// Everything a <see cref="RichEditLayout"/> depends on besides the document: the
/// column it wraps to, and the view settings that scale and space its text.
/// </summary>
/// <remarks>
/// <para>
/// One value rather than a set of fields, so a layout can tell whether it is still
/// current by comparing it, and a setting that moves the lines cannot be left out
/// of that comparison. Tab and indent widths are here for exactly that reason: they
/// were once kept beside the snapshot, and changing one after the first frame kept
/// the old line breaks.
/// </para>
/// <para>
/// Zoom multiplies every measurement read from the document - font sizes, indents,
/// tab stops, picture sizes - and nothing read from the control's own chrome. The
/// members here are where that multiplication happens, so it happens one way.
/// </para>
/// </remarks>
internal readonly record struct RichEditLayoutSettings(
    double ContentWidth,
    double Zoom,
    BFontStyle Font,
    double IndentWidth,
    double TabStopWidth)
{
    /// <summary>
    /// The default distance between tab stops: half an inch at 96 dpi, which is
    /// the tab every word processor starts a document with, and twice the default
    /// <see cref="StandardRichEdit.IndentWidth"/> so tabs and indent levels share a grid.
    /// </summary>
    public const double DefaultTabStopWidth = 48;

    /// <summary>Space left around an inline image so it does not touch the text above and below it.</summary>
    public const double ImageMargin = 2;

    /// <summary>
    /// The control's own font at this zoom, which is what text with no run of its
    /// own - the placeholder, an empty line - is measured and drawn with.
    /// </summary>
    public BFontStyle ZoomedFont => ZoomedFontFor(Font, Zoom);

    /// <summary>The height of a line of <see cref="ZoomedFont"/>.</summary>
    public double DefaultLineHeight => BTextMeasurer.GetLineHeight(ZoomedFont);

    /// <summary>One indent level as it is drawn.</summary>
    public double ZoomedIndentWidth => IndentWidth * Zoom;

    /// <summary>The space kept above and below an inline picture, as it is drawn.</summary>
    public double ZoomedImageMargin => ImageMargin * Zoom;

    /// <summary>
    /// The control's font at a zoom, for a caller that has no layout to ask - the
    /// wheel scrolls by lines of the font the control has now, not the one it was
    /// last laid out with.
    /// </summary>
    public static BFontStyle ZoomedFontFor(BFontStyle font, double zoom) =>
        font with { Size = ZoomedFontSize(font.Size, zoom) };

    /// <summary>
    /// The font one run is drawn with, in this control's own units.
    /// </summary>
    /// <remarks>
    /// The document states type in <em>points</em> and this control measures in
    /// device-independent pixels, so a stated size is converted rather than
    /// passed across. It used to be passed across, which rendered a twelve-point
    /// document at twelve pixels — a quarter smaller than it asks for, and
    /// smaller than the same file drawn by broilerdoc, which converts.
    ///
    /// The control's own <see cref="Font"/> needs no conversion: it is already
    /// in the units this control measures in, because a host set it there.
    /// </remarks>
    public BFontStyle RunFont(InlineStyle style)
    {
        double size = style.FontSize is > 0
            ? BFontStyle.PointsToPixels(style.FontSize.Value)
            : Font.Size;

        return Font with
        {
            FamilyName = string.IsNullOrWhiteSpace(style.FontFamily) ? Font.FamilyName : style.FontFamily,
            Size = ZoomedFontSize(size, Zoom),
            Weight = style.Bold ? BFontWeight.Bold : Font.Weight,
            Slant = style.Italic ? BFontSlant.Italic : Font.Slant,
        };
    }

    /// <summary>A size the document states, as it is drawn.</summary>
    public BSize Zoomed(BSize size) =>
        Zoom == 1 ? size : new BSize(size.Width * Zoom, size.Height * Zoom);

    /// <summary>
    /// The advance a tab reaching <paramref name="advance"/> lands on: the first
    /// tab stop strictly past it, so a tab always moves the text along even when
    /// it starts exactly on a stop.
    /// </summary>
    public double NextTabStop(double advance)
    {
        double width = (TabStopWidth > 0 ? TabStopWidth : DefaultTabStopWidth) * Zoom;
        return (Math.Floor(Math.Max(0, advance) / width) + 1) * width;
    }

    /// <summary>
    /// A stated font size as it is drawn. A whole pixel is the floor: zoomed far
    /// enough out, a size that rounded away would leave a document that is laid
    /// out but not legible.
    /// </summary>
    private static double ZoomedFontSize(double size, double zoom) => Math.Max(1, size * zoom);
}
