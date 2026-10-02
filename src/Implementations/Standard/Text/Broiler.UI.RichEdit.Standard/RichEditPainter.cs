using System;
using System.Collections.Generic;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Resources;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>The colours a rich edit paints its document with, read from the control once per frame.</summary>
internal readonly record struct RichEditPalette(
    BColor Background,
    BColor Foreground,
    BColor PlaceholderForeground,
    BColor BorderColor,
    BColor SelectionBackground,
    BColor SecondarySelectionBackground,
    BColor CaretColor,
    BColor PageSurround);

/// <summary>
/// Everything one frame of a rich edit's document depends on besides its layout:
/// what is shown, where, in which colours, and what the user is doing to it.
/// </summary>
/// <param name="CompositionStyle">
/// The style the composition will take when committed. Only read while
/// <paramref name="IsFocused"/> and composing, and only worth computing then.
/// </param>
internal readonly record struct RichEditPaintFrame(
    RichTextDocument Document,
    RichEditViewport View,
    RichEditPalette Palette,
    bool IsEnabled,
    bool IsFocused,
    RichTextRange Selection,
    RichTextRange? SecondarySelection,
    string PlaceholderText,
    string CompositionText,
    InlineStyle CompositionStyle,
    bool IsReadOnly = false);

/// <summary>
/// Paints a laid-out rich-text document: the sheet, the floating and running
/// shapes, table cells, run backgrounds, selections, list markers, text and
/// pictures, the IME composition, and the caret, in the order they stack.
/// </summary>
/// <remarks>
/// It keeps no state between frames. What it draws is what the
/// <see cref="RichEditLayout"/> measured and the <see cref="RichEditPaintFrame"/>
/// says, so painting cannot move the caret, scroll, or change the layout it is
/// drawing - it can only disagree with them, and that is what the tests check.
/// The control's own chrome and its scrollbar are drawn by the control, outside
/// the clip this paints inside.
/// </remarks>
internal sealed class RichEditPainter
{
    private readonly RichEditLayout _layout;
    private readonly RichEditImageCache _images;

    public RichEditPainter(RichEditLayout layout, RichEditImageCache images)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(images);
        _layout = layout;
        _images = images;
    }

    /// <summary>
    /// Paints the document into the control's inner bounds, which the caller has
    /// already clipped to.
    /// </summary>
    public void Paint(BRenderList renderList, in RichEditPaintFrame frame)
    {
        RichEditViewport view = frame.View;
        BRect inner = view.Inner;

        DrawPage(renderList, frame, inner);
        if (frame.Document.PlainText.Length == 0 && frame.CompositionText.Length == 0 && frame.PlaceholderText.Length > 0)
        {
            renderList.DrawText(
                new BTextRun(frame.PlaceholderText, _layout.Settings.ZoomedFont, frame.Palette.PlaceholderForeground),
                new BPoint(view.ContentLeft, view.ContentTop - view.ScrollY));
        }
        else
        {
            DrawRunningShapes(renderList, frame, inner, behindText: true);
            DrawShapes(renderList, frame, inner, behindText: true);
            DrawCells(renderList, frame, inner);
            DrawRunBackgrounds(renderList, frame, inner);
            DrawRange(renderList, frame, inner, frame.SecondarySelection, frame.Palette.SecondarySelectionBackground);
            DrawRange(renderList, frame, inner, frame.Selection, frame.Palette.SelectionBackground);
            DrawListMarkers(renderList, frame, inner);
            DrawText(renderList, frame, inner);
            DrawComposition(renderList, frame, inner);

            // The other half of the shapes: a document that says a picture sits in
            // front of the text gets one, and it covers the text the way it does
            // in the word processor the file came from. The caret is drawn after
            // this, so it stays findable under a shape.
            DrawShapes(renderList, frame, inner, behindText: false);
            DrawRunningShapes(renderList, frame, inner, behindText: false);
            DrawRunningText(renderList, frame, inner);
        }

        DrawCaret(renderList, frame);
    }

    /// <summary>
    /// Draws the sheet the document is written on, when it states one.
    /// </summary>
    /// <remarks>
    /// The surround is painted first so the paper reads as paper: without a
    /// different colour behind it the margins are indistinguishable from the
    /// control, and a page that cannot be told from its background is not worth
    /// laying out. A document that states no page paints neither, and looks
    /// exactly as it did.
    /// </remarks>
    private void DrawPage(BRenderList renderList, in RichEditPaintFrame frame, BRect inner)
    {
        if (frame.View.Page is not PageGeometry page)
            return;

        renderList.FillRect(inner, frame.IsEnabled ? frame.Palette.PageSurround : StandardControlPaint.SurfaceDisabled);

        BRect sheet = frame.View.Sheet(page, _layout.ContentHeight);
        renderList.FillRect(sheet, frame.IsEnabled ? frame.Palette.Background : StandardControlPaint.SurfaceDisabled);
        renderList.StrokeRect(sheet, frame.Palette.BorderColor, 1);
    }

    /// <summary>
    /// Draws one stacking layer of the running content's shapes: a letterhead's
    /// stripe belongs to the header, not to the first line of the letter.
    /// </summary>
    /// <remarks>
    /// The offsets are measured against the page rather than a paragraph, which
    /// is what running content is: it repeats, so it has no line of the body to
    /// hang from. This surface draws one sheet rather than paginating, so the
    /// header is drawn once at its top instead of once per page - and the
    /// first-page selection is the one a single sheet takes.
    /// </remarks>
    private void DrawRunningShapes(BRenderList renderList, in RichEditPaintFrame frame, BRect inner, bool behindText)
    {
        RichEditViewport view = frame.View;
        if (view.Page is not PageGeometry page || frame.Document.RunningContent.IsEmpty)
            return;

        double zoom = _layout.Settings.Zoom;
        double sheetTop = view.Sheet(page, _layout.ContentHeight).Top;
        foreach (DocumentShape shape in RunningShapes(frame.Document))
        {
            if (shape.BehindText != behindText || shape.Width <= 0 || shape.Height <= 0)
                continue;

            var bounds = new BRect(
                view.ContentLeft + (shape.OffsetX * zoom),
                sheetTop + (shape.OffsetY * zoom),
                shape.Width * zoom,
                shape.Height * zoom);
            if (bounds.Bottom < inner.Top || bounds.Top > inner.Bottom)
                continue;

            if (shape.Fill is ShapeFill fill)
                FillShape(renderList, bounds, fill);

            if (shape.Image is InlineImage image)
                DrawShapeImage(renderList, frame, image, bounds);

            if (!shape.Outline.IsEmpty && shape.Outline.A > 0)
                renderList.StrokeRect(bounds, shape.Outline, 1);

            DrawParagraphsInBox(renderList, frame, shape.Paragraphs, bounds);
        }
    }

    /// <summary>The header's and footer's shapes for the sheet, in draw order.</summary>
    private static IEnumerable<DocumentShape> RunningShapes(RichTextDocument document)
    {
        RunningContent running = document.RunningContent;
        foreach (DocumentShape shape in running.EffectiveHeaderShapes(PageSelection.First))
            yield return shape;

        foreach (DocumentShape shape in running.EffectiveFooterShapes(PageSelection.First))
            yield return shape;
    }

    /// <summary>
    /// Draws the header and the footer in the sheet's own margins, centred in the
    /// band each belongs to - the same placement the paginating renderers make.
    /// </summary>
    /// <remarks>
    /// A band too short for what it holds is left empty rather than drawn over
    /// the letter, which is what those renderers report and this one shows.
    /// </remarks>
    private void DrawRunningText(BRenderList renderList, in RichEditPaintFrame frame, BRect inner)
    {
        RichEditViewport view = frame.View;
        if (view.Page is not PageGeometry page || frame.Document.RunningContent.IsEmpty)
            return;

        RunningContent running = frame.Document.RunningContent;
        BRect sheet = view.Sheet(page, _layout.ContentHeight);
        double width = page.ContentWidth;

        DrawRunningBand(
            renderList,
            frame,
            running.EffectiveHeader(PageSelection.First),
            new BRect(view.ContentLeft, sheet.Top, width, page.MarginTop),
            inner);

        DrawRunningBand(
            renderList,
            frame,
            running.EffectiveFooter(PageSelection.First),
            new BRect(view.ContentLeft, sheet.Bottom - page.MarginBottom, width, page.MarginBottom),
            inner);
    }

    private void DrawRunningBand(
        BRenderList renderList,
        in RichEditPaintFrame frame,
        IReadOnlyList<RichTextParagraph> paragraphs,
        BRect band,
        BRect inner)
    {
        if (paragraphs.Count == 0 || band.Height <= 0 || band.Width <= 0)
            return;

        if (band.Bottom < inner.Top || band.Top > inner.Bottom)
            return;

        double height = MeasureParagraphs(paragraphs, band.Width);
        if (height > band.Height)
            return;

        DrawParagraphsInBox(
            renderList,
            frame,
            paragraphs,
            new BRect(band.Left, band.Top + ((band.Height - height) / 2), band.Width, height));
    }

    /// <summary>How tall the paragraphs are once wrapped to a width.</summary>
    private double MeasureParagraphs(IReadOnlyList<RichTextParagraph> paragraphs, double width)
    {
        double height = 0;
        foreach (RichTextParagraph paragraph in paragraphs)
        {
            BFontStyle font = _layout.RunFont(paragraph.Length > 0 ? RichEditLayout.StyleAt(paragraph, 0) : InlineStyle.Default);
            double lineHeight = BTextMeasurer.GetLineHeight(font);
            foreach (string _ in WrapToWidth(paragraph.Text, font, width))
                height += lineHeight;
        }

        return height;
    }

    /// <summary>
    /// Draws one stacking layer of the floating shapes, each against the
    /// paragraph it is anchored to: the ones under the body text before it is
    /// drawn, the ones over it afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two passes rather than one sorted list, because the layers are separated by
    /// everything else the control paints - cells, backgrounds, the selection, the
    /// text - and there is no single point in that sequence to sort against.
    /// </para>
    /// <para>
    /// The same arithmetic the other engines place a shape with: x from the text
    /// column's left edge, y from the top of the anchoring paragraph. A gradient
    /// is banded because the render list fills rectangles and has no gradient of
    /// its own.
    /// </para>
    /// </remarks>
    private void DrawShapes(BRenderList renderList, in RichEditPaintFrame frame, BRect inner, bool behindText)
    {
        RichEditViewport view = frame.View;
        double zoom = _layout.Settings.Zoom;
        foreach (DocumentShape shape in frame.Document.Shapes)
        {
            if (shape.BehindText != behindText)
                continue;

            if (shape.Width <= 0 || shape.Height <= 0)
                continue;

            if (!_layout.TryParagraphTop(shape.ParagraphIndex, out double paragraphTop))
                continue;

            var bounds = new BRect(
                view.ContentLeft + (shape.OffsetX * zoom),
                view.ContentTop + paragraphTop + (shape.OffsetY * zoom) - view.ScrollY,
                shape.Width * zoom,
                shape.Height * zoom);
            if (bounds.Bottom < inner.Top || bounds.Top > inner.Bottom)
                continue;

            if (shape.Fill is ShapeFill fill)
                FillShape(renderList, bounds, fill);

            // Over the fill and under the outline, so a framed picture keeps its
            // frame.
            if (shape.Image is InlineImage image)
                DrawShapeImage(renderList, frame, image, bounds);

            if (!shape.Outline.IsEmpty && shape.Outline.A > 0)
                renderList.StrokeRect(bounds, shape.Outline, 1);

            DrawParagraphsInBox(renderList, frame, shape.Paragraphs, bounds);
        }
    }

    /// <summary>
    /// Draws a floating picture into its box. The box is the size the document
    /// stated for the frame, so unlike an inline picture there is nothing to
    /// measure: it fills what it was given.
    /// </summary>
    private void DrawShapeImage(BRenderList renderList, in RichEditPaintFrame frame, InlineImage image, BRect bounds)
    {
        BImageHandle handle = _images.Resolve(image);
        if (!handle.IsValid)
        {
            // Same as an inline picture the backend could not decode: show where
            // it is rather than leaving a hole the reader cannot see.
            StandardControlPaint.StrokeRounded(
                renderList,
                bounds,
                frame.IsEnabled ? frame.Palette.Foreground : frame.Palette.PlaceholderForeground,
                StandardControlPaint.ControlRadius,
                1);
            return;
        }

        renderList.DrawImage(
            handle,
            new BRect(0, 0, handle.PixelSize.Width, handle.PixelSize.Height),
            bounds,
            frame.IsEnabled ? 1.0 : 0.5);
    }

    /// <summary>
    /// Paints the table cells: their backgrounds, then the edges they state.
    /// Under the text and over the shapes that draw behind it, so a shaded cell
    /// sits on a letterhead's stripe rather than under it.
    /// </summary>
    private void DrawCells(BRenderList renderList, in RichEditPaintFrame frame, BRect inner)
    {
        RichEditViewport view = frame.View;
        foreach (CellBox cell in _layout.Cells)
        {
            var bounds = new BRect(
                view.ContentLeft + cell.Bounds.Left,
                view.ToControlY(cell.Bounds.Top),
                cell.Bounds.Width,
                cell.Bounds.Height);
            if (bounds.Width <= 0 || bounds.Height <= 0 ||
                bounds.Bottom < inner.Top || bounds.Top > inner.Bottom)
            {
                continue;
            }

            if (!cell.Shading.IsEmpty && cell.Shading.A > 0)
                renderList.FillRect(bounds, cell.Shading);

            DrawCellBorders(renderList, bounds, cell.Borders);
        }
    }

    /// <summary>
    /// Draws a cell's four edges as filled rectangles. A cell states its sides
    /// separately and any of them may be turned off, so a stroked box would draw
    /// edges the document asked not to have.
    /// </summary>
    private static void DrawCellBorders(BRenderList renderList, BRect bounds, CellBorders borders)
    {
        if (borders.Top.IsVisible)
            renderList.FillRect(new BRect(bounds.Left, bounds.Top, bounds.Width, borders.Top.Width), borders.Top.Color);

        if (borders.Bottom.IsVisible)
        {
            renderList.FillRect(
                new BRect(bounds.Left, bounds.Bottom - borders.Bottom.Width, bounds.Width, borders.Bottom.Width),
                borders.Bottom.Color);
        }

        if (borders.Left.IsVisible)
        {
            renderList.FillRect(
                new BRect(bounds.Left, bounds.Top, borders.Left.Width, bounds.Height),
                borders.Left.Color);
        }

        if (borders.Right.IsVisible)
        {
            renderList.FillRect(
                new BRect(bounds.Right - borders.Right.Width, bounds.Top, borders.Right.Width, bounds.Height),
                borders.Right.Color);
        }
    }

    private static void FillShape(BRenderList renderList, BRect bounds, ShapeFill fill)
    {
        if (!fill.IsGradient)
        {
            renderList.FillRect(bounds, fill.Start);
            return;
        }

        double radians = fill.AngleDegrees * Math.PI / 180.0;
        bool vertical = Math.Abs(Math.Sin(radians)) >= Math.Abs(Math.Cos(radians));
        double extent = vertical ? bounds.Height : bounds.Width;
        int bands = (int)Math.Clamp(Math.Round(extent), 2, 512);

        for (int i = 0; i < bands; i++)
        {
            double t = bands == 1 ? 0 : (double)i / (bands - 1);
            BColor color = MixColor(fill.Start, fill.End, t);
            double offset = extent * i / bands;
            double size = (extent / bands) + 0.5;

            renderList.FillRect(
                vertical
                    ? new BRect(bounds.Left, bounds.Top + offset, bounds.Width, size)
                    : new BRect(bounds.Left + offset, bounds.Top, size, bounds.Height),
                color);
        }
    }

    private static BColor MixColor(BColor from, BColor to, double t) =>
        new(
            (byte)Math.Round(from.R + ((to.R - from.R) * t)),
            (byte)Math.Round(from.G + ((to.G - from.G) * t)),
            (byte)Math.Round(from.B + ((to.B - from.B) * t)),
            (byte)Math.Round(from.A + ((to.A - from.A) * t)));

    /// <summary>
    /// Draws a shape's own text inside its box, wrapped to the box rather than to
    /// the page column, and clipped where it runs past the bottom.
    /// </summary>
    private void DrawParagraphsInBox(
        BRenderList renderList,
        in RichEditPaintFrame frame,
        IReadOnlyList<RichTextParagraph> paragraphs,
        BRect bounds)
    {
        if (paragraphs.Count == 0 || bounds.Width <= 0)
            return;

        double y = bounds.Top;
        foreach (RichTextParagraph paragraph in paragraphs)
        {
            InlineStyle inline = paragraph.Length > 0 ? RichEditLayout.StyleAt(paragraph, 0) : InlineStyle.Default;
            BFontStyle font = _layout.RunFont(inline);
            double lineHeight = BTextMeasurer.GetLineHeight(font);
            BColor color = inline.Foreground.IsEmpty ? frame.Palette.Foreground : inline.Foreground;

            foreach (string line in WrapToWidth(paragraph.Text, font, bounds.Width))
            {
                if (y + lineHeight > bounds.Bottom)
                    return;

                double advance = BTextMeasurer.MeasureAdvance(line, font);
                double slack = Math.Max(0, bounds.Width - advance);
                double x = paragraph.Style.Alignment switch
                {
                    TextAlignment.Center => bounds.Left + (slack / 2),
                    TextAlignment.Right => bounds.Left + slack,
                    _ => bounds.Left,
                };

                renderList.DrawText(new BTextRun(line, font, color), new BPoint(x, y));
                y += lineHeight;
            }
        }
    }

    /// <summary>Greedy word wrap to a width, for text laid out inside a shape.</summary>
    private static IEnumerable<string> WrapToWidth(string text, BFontStyle font, double width)
    {
        if (text.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        string[] words = text.Split(' ');
        var line = new System.Text.StringBuilder();
        foreach (string word in words)
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && BTextMeasurer.MeasureAdvance(candidate, font) > width)
            {
                yield return line.ToString();
                line.Clear();
                line.Append(word);
                continue;
            }

            line.Clear();
            line.Append(candidate);
        }

        if (line.Length > 0)
            yield return line.ToString();
    }

    private (int Start, int Count) VisibleLines(RichEditViewport view, BRect inner)
    {
        double contentMinY = Math.Max(0, inner.Top - view.ContentTop + view.ScrollY);
        double contentMaxY = inner.Bottom - view.ContentTop + view.ScrollY;
        return _layout.GetVisibleLineRange(contentMinY, contentMaxY);
    }

    private void DrawRunBackgrounds(BRenderList renderList, in RichEditPaintFrame frame, BRect inner)
    {
        if (!frame.IsEnabled)
            return;

        RichEditViewport view = frame.View;
        (int start, int count) = VisibleLines(view, inner);
        for (int i = start; i < start + count; i++)
        {
            VisualLine line = _layout.Lines[i];
            if (line.End <= line.Start)
                continue;

            double y = view.ToControlY(line.Top);
            foreach (LineSegment segment in _layout.LineSegments(line, view.ContentLeft))
            {
                if (!segment.Style.Background.IsEmpty && segment.Advance > 0)
                    renderList.FillRect(new BRect(segment.X, y, segment.Advance, line.Height), segment.Style.Background);
            }
        }
    }

    private void DrawRange(BRenderList renderList, in RichEditPaintFrame frame, BRect inner, RichTextRange? range, BColor color)
    {
        if (range is not RichTextRange selection || selection.IsEmpty)
            return;

        RichEditViewport view = frame.View;
        (int start, int count) = VisibleLines(view, inner);
        for (int i = start; i < start + count; i++)
        {
            VisualLine line = _layout.Lines[i];
            double y = view.ToControlY(line.Top);
            if (_layout.TrySelectionSpan(line, selection, view.ContentLeft, out double left, out double width))
                renderList.FillRect(new BRect(left, y, width, line.Height), color);
        }
    }

    private void DrawText(BRenderList renderList, in RichEditPaintFrame frame, BRect inner)
    {
        RichEditViewport view = frame.View;
        BColor fallback = frame.IsEnabled ? frame.Palette.Foreground : frame.Palette.PlaceholderForeground;
        (int start, int count) = VisibleLines(view, inner);
        for (int i = start; i < start + count; i++)
        {
            VisualLine line = _layout.Lines[i];
            if (line.End <= line.Start)
                continue;

            double y = view.ToControlY(line.Top);
            foreach (LineSegment segment in _layout.LineSegments(line, view.ContentLeft))
            {
                BColor color = frame.IsEnabled && !segment.Style.Foreground.IsEmpty ? segment.Style.Foreground : fallback;
                if (segment.Image is InlineImage image)
                {
                    DrawInlineImage(renderList, frame, image, segment, y, line.Height, color);
                    continue;
                }

                // A tab has width but no glyphs; its underline and strike still run
                // across the gap, the way a word processor rules a tabbed line.
                if (segment.Text.Length > 0)
                    renderList.DrawText(new BTextRun(segment.Text, segment.Font, color), new BPoint(segment.X, y));

                DrawDecorations(renderList, segment, y, line.Height, color);
            }
        }
    }

    /// <summary>
    /// Draws each list paragraph's bullet or number in the gutter its text is
    /// indented for. A marker belongs to the paragraph rather than to a line, so it
    /// is drawn once, on the paragraph's first visual line, and the wrapped lines
    /// below it keep clear of it. It takes the font and color of the paragraph's
    /// first run, the way a word processor draws the marker in the formatting of
    /// the item it introduces.
    /// </summary>
    private void DrawListMarkers(BRenderList renderList, in RichEditPaintFrame frame, BRect inner)
    {
        RichEditViewport view = frame.View;
        BColor fallback = frame.IsEnabled ? frame.Palette.Foreground : frame.Palette.PlaceholderForeground;
        (int start, int count) = VisibleLines(view, inner);
        int drawnParagraph = -1;
        for (int i = start; i < start + count; i++)
        {
            VisualLine line = _layout.Lines[i];
            if (line.ParagraphIndex == drawnParagraph)
                continue;

            drawnParagraph = line.ParagraphIndex;
            ParagraphDecoration decoration = _layout.Decoration(line.ParagraphIndex);
            if (decoration.Marker.Length == 0)
                continue;

            double y = view.ToControlY(line.Top);
            InlineStyle style = RichEditLayout.StyleAt(frame.Document.Paragraphs[line.ParagraphIndex], 0);
            BColor color = frame.IsEnabled && !style.Foreground.IsEmpty ? style.Foreground : fallback;
            // The marker travels with the text it introduces, so a centered or
            // right-aligned item keeps its bullet against the item, not the margin.
            renderList.DrawText(
                new BTextRun(decoration.Marker, decoration.Font, color),
                new BPoint(_layout.MarkerLeft(line, view.ContentLeft), y));
        }
    }

    /// <summary>
    /// Draws one inline picture, bottom-aligned in its line the way Word sits an
    /// inline image on the text baseline. An image the backend could not decode
    /// is drawn as an outlined box, so the document still shows where it is.
    /// </summary>
    private void DrawInlineImage(
        BRenderList renderList,
        in RichEditPaintFrame frame,
        InlineImage image,
        LineSegment segment,
        double lineTop,
        double lineHeight,
        BColor color)
    {
        BSize size = _layout.ImageDisplaySize(image);
        double margin = _layout.Settings.ZoomedImageMargin;
        double height = Math.Min(size.Height, Math.Max(0, lineHeight - (margin * 2)));
        double top = lineTop + Math.Max(margin, lineHeight - height - margin);
        var destination = new BRect(segment.X, top, segment.Advance, height);

        BImageHandle handle = _images.Resolve(image);
        if (handle.IsValid)
        {
            var source = new BRect(0, 0, handle.PixelSize.Width, handle.PixelSize.Height);
            renderList.DrawImage(handle, source, destination, frame.IsEnabled ? 1.0 : 0.5);
            return;
        }

        StandardControlPaint.StrokeRounded(renderList, destination, color, StandardControlPaint.ControlRadius, 1);
    }

    private static void DrawDecorations(BRenderList renderList, LineSegment segment, double y, double lineHeight, BColor color)
    {
        if (segment.Advance <= 0 || (!segment.Style.Underline && !segment.Style.Strikethrough))
            return;

        double thickness = Math.Max(1, Math.Round(segment.Font.Size / 14));
        if (segment.Style.Underline)
            renderList.FillRect(new BRect(segment.X, y + lineHeight - thickness - 1, segment.Advance, thickness), color);
        if (segment.Style.Strikethrough)
            renderList.FillRect(new BRect(segment.X, y + (lineHeight / 2), segment.Advance, thickness), color);
    }

    private void DrawComposition(BRenderList renderList, in RichEditPaintFrame frame, BRect inner)
    {
        if (!frame.IsFocused || frame.CompositionText.Length == 0)
            return;

        RichEditViewport view = frame.View;
        VisualLine line = _layout.LineForPosition(frame.Selection.Focus).Line;
        double y = view.ToControlY(line.Top);
        if (y + line.Height < inner.Top || y > inner.Bottom)
            return;

        double x = _layout.CaretX(frame.Selection.Focus, view.ContentLeft);
        InlineStyle style = frame.CompositionStyle;
        BFontStyle font = _layout.RunFont(style);
        double advance = RichEditTextShaping.MeasurePieces(frame.CompositionText, style, font);
        BColor color = frame.IsEnabled ? frame.Palette.Foreground : frame.Palette.PlaceholderForeground;

        // Preedit text is drawn with the capitalization it will take once
        // committed, so the text does not jump when the IME finishes.
        double pieceX = x;
        foreach (ShapedPiece piece in RichEditTextShaping.ShapePieces(frame.CompositionText, style, font))
        {
            renderList.DrawText(new BTextRun(piece.Text, piece.Font, color), new BPoint(pieceX, y));
            pieceX += BTextMeasurer.MeasureAdvance(piece.Text, piece.Font);
        }

        renderList.FillRect(new BRect(x, y + line.Height - 2, advance, 1), color); // composition underline
    }

    private void DrawCaret(BRenderList renderList, in RichEditPaintFrame frame)
    {
        if (!frame.IsFocused || !frame.IsEnabled || frame.IsReadOnly)
            return;

        renderList.FillRect(_layout.CaretBounds(frame.Selection.Focus, frame.View), frame.Palette.CaretColor);
    }
}
