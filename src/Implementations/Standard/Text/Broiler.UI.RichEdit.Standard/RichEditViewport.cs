using System;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// Where a rich edit's content sits inside the control at one moment: the
/// control's box less its padding, the sheet the document is written on when it
/// states one, the text column, and how far that column is scrolled.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="RichEditLayout"/> measures in content space - x from the text
/// column's left edge, y from its top before scrolling - and this is the one place
/// that space is mapped into the control's. Every value is a function of the
/// control's bounds and padding, the document, the zoom, and the scroll offset, so
/// the control makes a new one whenever it needs one rather than keeping one that
/// could go stale.
/// </para>
/// <para>
/// The expressions keep the order their terms were always added in. Floating-point
/// addition is not associative, and the caret, the selection, and the glyphs only
/// land on the same pixel because they are all computed the same way.
/// </para>
/// </remarks>
internal readonly struct RichEditViewport
{
    public RichEditViewport(
        BRect bounds,
        double paddingX,
        double paddingY,
        PageGeometry? page,
        double shapeGutter,
        double scrollY)
    {
        Bounds = bounds;
        PaddingX = paddingX;
        PaddingY = paddingY;
        Page = page;
        ShapeGutter = shapeGutter;
        ScrollY = scrollY;
    }

    /// <summary>The control's arranged bounds.</summary>
    public BRect Bounds { get; }

    public double PaddingX { get; }

    public double PaddingY { get; }

    /// <summary>
    /// The page this document is written for, at the size it is drawn, or null
    /// when it states none and is laid out to the width of the control.
    /// </summary>
    public PageGeometry? Page { get; }

    /// <summary>
    /// How far left of the text column the document's shapes reach, which the
    /// column moves over to make room for. Unused when there is a page, whose
    /// margin is that room.
    /// </summary>
    public double ShapeGutter { get; }

    /// <summary>How far the content is scrolled up, in content units.</summary>
    public double ScrollY { get; }

    /// <summary>The control's box less its padding: what the content is clipped to.</summary>
    public BRect Inner => InnerOf(Bounds, PaddingX, PaddingY);

    /// <summary>
    /// Where the sheet starts. It is centred in whatever width the control has,
    /// and never left of the padding - a window narrower than the paper shows the
    /// left of the sheet rather than centring half of it out of view.
    /// </summary>
    public double PageLeft =>
        Page is PageGeometry page
            ? Bounds.Left + Math.Max(PaddingX, (Bounds.Width - page.Width) / 2)
            : Bounds.Left + PaddingX;

    /// <summary>The control-space x of the text column's left edge.</summary>
    public double ContentLeft =>
        Page is PageGeometry page
            ? PageLeft + page.MarginLeft
            : Bounds.Left + PaddingX + ShapeGutter;

    /// <summary>The control-space y of the content's top, before scrolling.</summary>
    public double ContentTop =>
        Bounds.Top + PaddingY + (Page is PageGeometry page ? page.MarginTop : 0);

    /// <summary>The width the text column wraps to.</summary>
    public double ContentWidth =>
        Page is PageGeometry page
            ? page.ContentWidth
            : Math.Max(0, Bounds.Width - (PaddingX * 2) - ShapeGutter);

    /// <summary>The control-space y of a content-space y.</summary>
    public double ToControlY(double contentY) => ContentTop + contentY - ScrollY;

    /// <summary>
    /// The paper, in control space. A sheet is at least a page tall, and taller
    /// when the text runs past the bottom - this surface flows rather than
    /// paginating, so the paper grows instead of a second sheet starting.
    /// </summary>
    public BRect Sheet(PageGeometry page, double contentHeight) => new(
        PageLeft,
        Bounds.Top + PaddingY - ScrollY,
        page.Width,
        Math.Max(page.Height, contentHeight + page.MarginTop + page.MarginBottom));

    /// <summary>
    /// A box less its padding, for a caller that needs the window onto the content
    /// without the page and gutter a whole viewport works out.
    /// </summary>
    public static BRect InnerOf(BRect bounds, double paddingX, double paddingY) => new(
        bounds.Left + paddingX,
        bounds.Top + paddingY,
        Math.Max(0, bounds.Width - (paddingX * 2)),
        Math.Max(0, bounds.Height - (paddingY * 2)));

    /// <summary>The viewport for the control's current state.</summary>
    public static RichEditViewport Create(
        BRect bounds,
        double paddingX,
        double paddingY,
        RichTextDocument document,
        double zoom,
        double scrollY) =>
        new(bounds, paddingX, paddingY, PageFor(document, zoom), ShapeGutterFor(document, zoom), scrollY);

    /// <summary>
    /// The page a document is written for, at the size it is drawn. A document
    /// that says nothing is laid out to the width of the control, as it always
    /// was.
    /// </summary>
    public static PageGeometry? PageFor(RichTextDocument document, double zoom) =>
        document.PageGeometry is PageGeometry geometry && geometry.IsUsable ? Zoomed(geometry, zoom) : null;

    /// <summary>
    /// How far left of the text column a document's shapes reach.
    /// </summary>
    /// <remarks>
    /// A letterhead anchors its stripe about 111 points left of the column,
    /// because on the printed page that is the margin it stands in. This surface
    /// has no page and fills whatever width it is given, so without a gutter the
    /// stripe would be drawn off the left edge and clipped away. A document with
    /// no margin content - which is nearly all of them - gets no gutter and the
    /// full width, so nothing changes for ordinary text.
    /// </remarks>
    public static double ShapeGutterFor(RichTextDocument document, double zoom)
    {
        double gutter = 0;
        foreach (DocumentShape shape in document.Shapes)
        {
            if (shape.OffsetX < 0)
                gutter = Math.Max(gutter, -shape.OffsetX * zoom);
        }

        return gutter;
    }

    /// <summary>
    /// A page as it is drawn. The paper is scaled with the text on it, or a
    /// zoomed-in document would run off a sheet that stayed the size it was.
    /// </summary>
    private static PageGeometry Zoomed(PageGeometry page, double zoom) =>
        zoom == 1
            ? page
            : new PageGeometry(
                page.Width * zoom,
                page.Height * zoom,
                page.MarginLeft * zoom,
                page.MarginRight * zoom,
                page.MarginTop * zoom,
                page.MarginBottom * zoom,
                page.HeaderDistance * zoom,
                page.FooterDistance * zoom);
}
