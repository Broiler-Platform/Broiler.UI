using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Resources;
using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// A rich edit's document laid out: wrapped into visual lines, with its table
/// cells, list markers, and indents placed, and the measurements that put a caret,
/// a selection, or a click against those lines.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is in content space: y from the top of the content before it is
/// scrolled, x from the left edge of the text column. A query that answers in the
/// control's space takes the column's control-space left as <c>contentLeft</c> and
/// starts its sum from it, which keeps the arithmetic in the order it has always
/// been done in - see <see cref="RichEditViewport"/>.
/// </para>
/// <para>
/// The lines are rebuilt only when the document or the
/// <see cref="RichEditLayoutSettings"/> change, so every frame and every key press
/// between two edits reads the same layout. A picture with no stated size is
/// measured by decoding it, through the <see cref="RichEditImageCache"/> that also
/// draws it, so it is decoded once.
/// </para>
/// </remarks>
internal sealed class RichEditLayout
{
    /// <summary>The box an image with no known size is drawn in, and the minimum for any image.</summary>
    private const double FallbackImageExtent = 72;

    /// <summary>The glyph a bulleted paragraph is marked with, the one the DOCX and PDF writers emit.</summary>
    private const string BulletMarker = "\u2022";

    /// <summary>Space kept between a list marker and the text it introduces.</summary>
    private const double MarkerGap = 4;

    private readonly RichEditImageCache _images;
    private readonly List<VisualLine> _lines = [];
    private readonly List<ParagraphDecoration> _decorations = [];
    private readonly List<CellFrame> _frames = [];
    private readonly List<CellBox> _cells = [];

    /// <summary>The boxes wrapping shapes keep this layout's lines out of.</summary>
    private TextWrapExclusions _wrap = new();
    private RichTextDocument? _document;
    private RichEditLayoutSettings _settings;
    private bool _isValid;
    private double _contentHeight;
    private double _contentExtentWidth;

    private readonly struct CharAdvanceCacheKey(BFontStyle font, char character, TextCapitalization capitalization) : IEquatable<CharAdvanceCacheKey>
    {
        public readonly BFontStyle Font = font;
        public readonly char Character = character;
        public readonly TextCapitalization Capitalization = capitalization;

        public bool Equals(CharAdvanceCacheKey other) =>
            Character == other.Character && Capitalization == other.Capitalization && Font.Equals(other.Font);

        public override int GetHashCode() =>
            HashCode.Combine(Font, Character, (int)Capitalization);

        public override bool Equals(object? obj) =>
            obj is CharAdvanceCacheKey other && Equals(other);
    }

    private readonly Dictionary<CharAdvanceCacheKey, double> _charAdvanceCache = new(256);
    private const int MaxCharAdvanceCacheSize = 1024;

    public RichEditLayout(RichEditImageCache images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
    }

    /// <summary>The settings the current lines were built with.</summary>
    public RichEditLayoutSettings Settings => _settings;

    /// <summary>
    /// The visual lines, top to bottom. Never empty once built: a document with no
    /// text still has a line for the caret to stand on.
    /// </summary>
    public IReadOnlyList<VisualLine> Lines => _lines;

    /// <summary>The table cells, in the order they were laid out.</summary>
    public IReadOnlyList<CellBox> Cells => _cells;

    /// <summary>
    /// How tall the content is - what there is to scroll through. A page's margins
    /// are part of it: a page's last line sits a bottom margin above the end of the
    /// paper, not at it.
    /// </summary>
    public double ContentHeight => _contentHeight;

    /// <summary>
    /// The maximum horizontal extent of laid-out content across all visual lines.
    /// </summary>
    public double ContentExtentWidth => _contentExtentWidth;

    private RichTextDocument Document =>
        _document ?? throw new InvalidOperationException("The rich edit has not been laid out.");

    /// <summary>
    /// Brings the layout up to date with a document and settings, rebuilding the
    /// lines only when either differs from what they were built with.
    /// </summary>
    /// <returns>True when the lines were rebuilt.</returns>
    public bool Update(RichTextDocument document, RichEditLayoutSettings settings)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_isValid && ReferenceEquals(_document, document) && _settings == settings)
            return false;

        // Invalid until the build finishes, so a build that throws is retried
        // rather than trusted half done.
        _isValid = false;
        _document = document;
        _settings = settings;
        Build();
        _isValid = true;
        return true;
    }

    /// <summary>Makes the next <see cref="Update"/> rebuild whatever it is given.</summary>
    public void Invalidate() => _isValid = false;

    // --- Queries -----------------------------------------------------------

    /// <summary>The indent and list decoration of a paragraph layout has seen, else none.</summary>
    public ParagraphDecoration Decoration(int paragraphIndex) =>
        (uint)paragraphIndex < (uint)_decorations.Count ? _decorations[paragraphIndex] : ParagraphDecoration.None;

    /// <summary>The box a paragraph is laid out in, or the whole column when layout has not seen it.</summary>
    public CellFrame Frame(int paragraphIndex) =>
        (uint)paragraphIndex < (uint)_frames.Count
            ? _frames[paragraphIndex]
            : new CellFrame(0, Math.Max(1, _settings.ContentWidth));

    /// <summary>
    /// Where a line of text starts: past its paragraph's indent and list gutter,
    /// then along by whatever its alignment pushes it.
    /// </summary>
    public double LineLeft(VisualLine line, double contentLeft) =>
        contentLeft + Frame(line.ParagraphIndex).Left +
        Decoration(line.ParagraphIndex).TextIndent + line.AlignmentOffset;

    /// <summary>
    /// Where a paragraph's list marker starts: the same box and alignment its
    /// text gets, at the marker's own indent rather than the text's.
    /// </summary>
    /// <remarks>
    /// Written as a sibling of <see cref="LineLeft"/> on purpose. The marker used
    /// to be positioned from the column's left directly, which is the same thing
    /// for an ordinary paragraph — an ordinary paragraph's frame starts at zero —
    /// and wrong for every paragraph in a table cell, where the text moved over by
    /// the cell's offset and the bullet stayed at the page margin. The two origins
    /// differ in one term and now say so.
    /// </remarks>
    public double MarkerLeft(VisualLine line, double contentLeft) =>
        contentLeft + Frame(line.ParagraphIndex).Left +
        Decoration(line.ParagraphIndex).MarkerIndent + line.AlignmentOffset;

    /// <summary>
    /// The line a position is on. A position at the end of a wrapped line is on
    /// that line rather than the start of the next, which is where a caret after
    /// the last character of a line belongs.
    /// </summary>
    public (VisualLine Line, int Index) LineForPosition(RichTextPosition position)
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            VisualLine line = _lines[i];
            if (line.ParagraphIndex == position.ParagraphIndex && position.Offset <= line.End)
                return (line, i);
        }

        for (int i = _lines.Count - 1; i >= 0; i--)
        {
            if (_lines[i].ParagraphIndex == position.ParagraphIndex)
                return (_lines[i], i);
        }

        return (_lines[^1], _lines.Count - 1);
    }

    /// <summary>
    /// The line under a content-space y: the first whose bottom is below it, or
    /// the last line for a point past the end.
    /// </summary>
    public VisualLine LineAt(double y)
    {
        if (_lines.Count == 0)
            return default;

        int low = 0;
        int high = _lines.Count - 1;
        int result = _lines.Count - 1;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            if (y < _lines[mid].Top + _lines[mid].Height)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return _lines[result];
    }

    /// <summary>
    /// Gets the contiguous range of visual lines overlapping the vertical span
    /// [<paramref name="contentMinY"/>..<paramref name="contentMaxY"/>] using binary search.
    /// </summary>
    public (int StartIndex, int Count) GetVisibleLineRange(double contentMinY, double contentMaxY)
    {
        if (_lines.Count == 0 || contentMaxY < 0)
            return (0, 0);

        int low = 0;
        int high = _lines.Count - 1;
        int firstVisible = _lines.Count;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            VisualLine line = _lines[mid];
            if (line.Top + line.Height >= contentMinY)
            {
                firstVisible = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        if (firstVisible >= _lines.Count)
            return (0, 0);

        int lastVisible = firstVisible;
        while (lastVisible < _lines.Count && _lines[lastVisible].Top <= contentMaxY)
        {
            lastVisible++;
        }

        return (firstVisible, lastVisible - firstVisible);
    }

    /// <summary>The position in a line nearest a control-space x.</summary>
    public RichTextPosition PositionInLineAtX(VisualLine line, double x, double contentLeft)
    {
        int offset = OffsetAtX(line, x - LineLeft(line, contentLeft));
        return new RichTextPosition(line.ParagraphIndex, line.Start + offset);
    }

    /// <summary>The control-space x a caret at a position is drawn at.</summary>
    public double CaretX(RichTextPosition position, double contentLeft)
    {
        VisualLine line = LineForPosition(position).Line;
        int end = Math.Clamp(position.Offset, line.Start, line.End);
        return LineLeft(line, contentLeft) + AdvanceInLine(line, Document.Paragraphs[line.ParagraphIndex], end);
    }

    /// <summary>The caret at a position, in the control space of a viewport.</summary>
    public BRect CaretBounds(RichTextPosition position, RichEditViewport view)
    {
        VisualLine line = LineForPosition(position).Line;
        double x = CaretX(position, view.ContentLeft);
        double y = view.ContentTop + line.Top - view.ScrollY;
        return new BRect(x, y + 1, 1, Math.Max(1, line.Height - 2));
    }

    /// <summary>
    /// The stretch of a line a range covers, in control-space x. A line the range
    /// covers entirely but that has no width of its own - an empty paragraph -
    /// still gets a space's width, so a selection across it shows that it does.
    /// </summary>
    /// <returns>False when the range does not reach into the line.</returns>
    /// <remarks>
    /// Painting a selection and deciding whether a right-click landed in one both
    /// ask this, so what a click treats as selected is what was drawn as selected.
    /// </remarks>
    public bool TrySelectionSpan(
        VisualLine line,
        RichTextRange range,
        double contentLeft,
        out double left,
        out double width)
    {
        RichTextPosition start = range.Start;
        RichTextPosition end = range.End;
        var lineStart = new RichTextPosition(line.ParagraphIndex, line.Start);
        var lineEnd = new RichTextPosition(line.ParagraphIndex, line.End);

        left = 0;
        width = 0;
        bool fullyInside = start <= lineStart && end >= lineEnd;
        if (!fullyInside && (end <= lineStart || start >= lineEnd))
            return false;

        int subStart = start.ParagraphIndex == line.ParagraphIndex ? Math.Clamp(start.Offset, line.Start, line.End) : line.Start;
        int subEnd = end.ParagraphIndex == line.ParagraphIndex ? Math.Clamp(end.Offset, line.Start, line.End) : line.End;
        if (start.ParagraphIndex < line.ParagraphIndex)
            subStart = line.Start;
        if (end.ParagraphIndex > line.ParagraphIndex)
            subEnd = line.End;

        RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
        double x1 = LineLeft(line, contentLeft) + AdvanceInLine(line, paragraph, subStart);
        double x2 = LineLeft(line, contentLeft) + AdvanceInLine(line, paragraph, subEnd);
        width = x2 - x1;
        if (width <= 0)
        {
            if (!fullyInside)
                return false;
            width = BTextMeasurer.MeasureAdvance(" ", _settings.ZoomedFont); // sliver marking an empty selected line
        }

        left = x1;
        return true;
    }

    /// <summary>The top of a paragraph's first line, which is what a shape hangs from.</summary>
    public bool TryParagraphTop(int paragraphIndex, out double top)
    {
        foreach (VisualLine line in _lines)
        {
            if (line.ParagraphIndex == paragraphIndex)
            {
                top = line.Top;
                return true;
            }
        }

        top = 0;
        return false;
    }

    /// <summary>
    /// Splits a visual line into contiguous styled segments, each carrying its
    /// resolved font, control-space x origin, and advance. A tab yields a segment
    /// with no glyphs whose advance reaches the next tab stop, so the run
    /// background and underline it carries are still drawn across the gap it opens.
    /// </summary>
    public IEnumerable<LineSegment> LineSegments(VisualLine line, double contentLeft)
    {
        RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
        double left = LineLeft(line, contentLeft);
        double x = left;
        int pos = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            int runStart = pos;
            int runEnd = pos + run.Length;
            pos = runEnd;

            int segStart = Math.Max(runStart, line.Start);
            int segEnd = Math.Min(runEnd, line.End);
            if (segEnd <= segStart)
                continue;

            string text = paragraph.Text.Substring(segStart, segEnd - segStart);
            if (run.Style.Image is InlineImage image)
            {
                // Every placeholder character in an image run draws the image, so
                // a run that happens to hold two of them draws two pictures
                // rather than one stretched across both character positions.
                foreach (char character in text)
                {
                    if (character == InlineImage.Placeholder)
                    {
                        double width = ImageDisplaySize(image).Width;
                        yield return LineSegment.ForImage(image, run.Style, x, width);
                        x += width;
                        continue;
                    }

                    string single = character.ToString();
                    double advance = RichEditTextShaping.MeasurePieces(single, run.Style, _settings.RunFont(run.Style));
                    yield return new LineSegment(single, run.Style, _settings.RunFont(run.Style), x, advance);
                    x += advance;
                }

                continue;
            }

            foreach ((string piece, bool isTab) in RichEditTextShaping.SplitTabs(paragraph.Text, segStart, segEnd))
            {
                if (isTab)
                {
                    double stop = left + _settings.NextTabStop(x - left);
                    yield return LineSegment.ForTab(run.Style, _settings.RunFont(run.Style), x, stop - x);
                    x = stop;
                    continue;
                }

                foreach (ShapedPiece shaped in RichEditTextShaping.ShapePieces(piece, run.Style, _settings.RunFont(run.Style)))
                {
                    // A justified line is drawn a word at a time. Widening a
                    // segment's advance alone would move only what comes after it,
                    // and a line drawn as one string has nothing after it - the
                    // backend would set it with its own spacing and the line would
                    // stay ragged. Each chunk carrying its own origin is what makes
                    // the gap real.
                    foreach (string chunk in RichEditTextShaping.StretchChunks(shaped.Text, line.WordSpacing))
                    {
                        double advance = BTextMeasurer.MeasureAdvance(chunk, shaped.Font) +
                                         (RichEditTextShaping.CountSpaces(chunk, 0, chunk.Length) * line.WordSpacing);
                        yield return new LineSegment(chunk, run.Style, shaped.Font, x, advance);
                        x += advance;
                    }
                }
            }
        }
    }

    /// <summary>
    /// The size an inline image is drawn at: the display size the document
    /// states, else the decoded pixel size, else a fixed box. The box keeps a
    /// picture the backend could not decode visible and selectable instead of
    /// collapsing it to nothing.
    /// </summary>
    public BSize ImageDisplaySize(InlineImage image)
    {
        // The model resolves this without touching the payload whenever the
        // document states a size or the resource knows its own pixels, so the
        // decode below is now only for a picture whose intrinsic size nothing
        // established — an encoded payload no registered codec could inspect.
        if (image.TryGetDisplaySize(out double width, out double height))
            return _settings.Zoomed(new BSize(width, height));

        BImageHandle handle = _images.Resolve(image);
        if (handle.IsValid && handle.PixelSize.Width > 0 && handle.PixelSize.Height > 0)
            return _settings.Zoomed(handle.PixelSize);

        return _settings.Zoomed(new BSize(FallbackImageExtent, FallbackImageExtent));
    }

    /// <summary>
    /// How far into a line an offset sits, counting the extra width word spacing
    /// gave the spaces before it. Everything that has to land on the same pixel
    /// as the drawn glyphs — the caret, the selection, a click — measures here.
    /// </summary>
    private double AdvanceInLine(VisualLine line, RichTextParagraph paragraph, int offset)
    {
        double advance = MeasureAdvance(paragraph, line.Start, offset);
        if (line.WordSpacing == 0)
            return advance;

        int end = Math.Clamp(offset, line.Start, line.End);
        return advance + (RichEditTextShaping.CountSpaces(paragraph.Text, line.Start, end) * line.WordSpacing);
    }

    private int OffsetAtX(VisualLine line, double localX)
    {
        RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
        double advance = 0;
        int index = line.Start;
        while (index < line.End)
        {
            double charAdvance = CharAdvance(paragraph, index, advance, out int step);
            if (paragraph.Text[index] == ' ')
                charAdvance += line.WordSpacing;
            if (localX < advance + (charAdvance / 2))
                break;
            advance += charAdvance;
            index += step;
        }

        return index - line.Start;
    }

    // --- Building ----------------------------------------------------------

    private void Build()
    {
        _lines.Clear();
        _cells.Clear();
        _wrap = new TextWrapExclusions();
        RichTextDocument document = Document;
        double contentWidth = _settings.ContentWidth;
        BuildFrames(document, contentWidth);
        BuildDecorations(document);

        double y = LayoutBlocks(
            document,
            document.Tables,
            0,
            document.ParagraphCount,
            0,
            new CellFrame(0, Math.Max(1, contentWidth)));

        if (_lines.Count == 0)
        {
            _lines.Add(new VisualLine(0, 0, 0, 0, _settings.DefaultLineHeight, 0));
            y = _settings.DefaultLineHeight;
        }

        // The margins are part of what scrolls: a page's last line sits a bottom
        // margin above the end of the paper, not at it.
        _contentHeight = RichEditViewport.PageFor(document, _settings.Zoom) is PageGeometry page
            ? y + page.MarginTop + page.MarginBottom
            : y;

        double maxLineWidth = 0;
        foreach (VisualLine line in _lines)
        {
            double lineWidth = LineLeft(line, 0) + AdvanceInLine(line, document.Paragraphs[line.ParagraphIndex], line.End);
            if (lineWidth > maxLineWidth)
                maxLineWidth = lineWidth;
        }

        _contentExtentWidth = Math.Max(contentWidth, maxLineWidth);
    }

    /// <summary>
    /// Lays out a range of block content from <paramref name="y"/> down, and
    /// returns where it ends. A table goes through <see cref="LayoutTable"/>,
    /// which comes back here for each of its cells - so a table inside a cell
    /// costs nothing but the recursion.
    /// </summary>
    private double LayoutBlocks(
        RichTextDocument document,
        IReadOnlyList<DocumentTable> tables,
        int from,
        int to,
        double y,
        CellFrame frame)
    {
        int index = Math.Max(0, from);
        int end = Math.Min(to, document.ParagraphCount);
        while (index < end)
        {
            if (DocumentTable.StartingAt(tables, index) is DocumentTable table)
            {
                y = LayoutTable(document, table, y, frame);
                index = table.ParagraphEnd;
                continue;
            }

            y = LayoutParagraph(document, index, y);
            index++;
        }

        return y;
    }

    /// <summary>Wraps one paragraph into visual lines from <paramref name="y"/> down.</summary>
    private double LayoutParagraph(RichTextDocument document, int paragraphIndex, double y)
    {
        RichTextParagraph paragraph = document.Paragraphs[paragraphIndex];
        double defaultLineHeight = _settings.DefaultLineHeight;
        double frame = Math.Max(1, Frame(paragraphIndex).Width - Decoration(paragraphIndex).TextIndent);

        // A shape's box is known once the paragraph it hangs from has a top, so
        // this paragraph's shapes join the exclusions before its own lines are
        // wrapped. One anchored further down cannot narrow a line above it, which
        // is what a single forward pass can honestly say.
        RegisterWrapShapes(document, paragraphIndex, y);

        foreach ((int segmentStart, int segmentEnd) in RichEditTextShaping.HardSegments(paragraph.Text))
        {
            if (segmentStart == segmentEnd)
            {
                TextBand empty = LineBand(ref y, defaultLineHeight, frame);
                _lines.Add(new VisualLine(
                    paragraphIndex, segmentStart, segmentEnd, y, defaultLineHeight,
                    empty.Left + AlignmentOffset(paragraph, segmentStart, segmentEnd, empty.Width),
                    LineWordSpacing(paragraph, segmentStart, segmentEnd, empty.Width)));
                y += defaultLineHeight;
                continue;
            }

            int i = segmentStart;
            while (i < segmentEnd)
            {
                // The band is asked for at the default height rather than the
                // line's own, which is not known until the line has been wrapped
                // to a width. A taller line can therefore reach a little into a
                // shape it only just cleared.
                TextBand band = LineBand(ref y, defaultLineHeight, frame);
                int lineEnd = MeasureWrap(paragraph, i, segmentEnd, band.Width);
                double lineHeight = MeasureLineHeight(paragraph, i, lineEnd, defaultLineHeight);
                _lines.Add(new VisualLine(
                    paragraphIndex, i, lineEnd, y, lineHeight,
                    band.Left + AlignmentOffset(paragraph, i, lineEnd, band.Width),
                    LineWordSpacing(paragraph, i, lineEnd, band.Width)));
                y += lineHeight;
                i = lineEnd;
            }
        }

        return y;
    }

    /// <summary>Adds the wrapping shapes anchored to one paragraph, now that it has a top.</summary>
    private void RegisterWrapShapes(RichTextDocument document, int paragraphIndex, double top)
    {
        foreach (DocumentShape shape in document.Shapes)
        {
            if (shape.Wraps && shape.ParagraphIndex == paragraphIndex)
                _wrap.Add(shape, top + (shape.OffsetY * _settings.Zoom), _settings.Zoom);
        }
    }

    /// <summary>
    /// The span left for a line at <paramref name="y"/>, moving it down past
    /// anything that leaves it no room at all.
    /// </summary>
    /// <remarks>
    /// The clearing and the bound both live in <see cref="TextWrapExclusions"/>,
    /// so this surface and the two paginating renderers answer the question the
    /// same way.
    /// </remarks>
    private TextBand LineBand(ref double y, double height, double frame) =>
        _wrap.Resolve(ref y, height, frame, out _);

    /// <summary>
    /// Lays a table out row by row: every cell of a row starts at the row's top,
    /// and the tallest of them says where the next row starts.
    /// </summary>
    /// <remarks>
    /// The boxes are recorded as the rows are measured and then grown, because a
    /// cell that spans rows only knows how tall it is once the rows below it have
    /// been laid out.
    /// </remarks>
    private double LayoutTable(RichTextDocument document, DocumentTable table, double top, CellFrame frame)
    {
        double[] edges = ColumnEdges(table, frame);
        double padding = table.CellPadding * _settings.Zoom;
        double defaultLineHeight = _settings.DefaultLineHeight;
        var heights = new List<double>(table.Rows.Count);
        var spans = new List<(int Row, int Index, int RowSpan)>();

        // A cell that spans rows is not as tall as its first row. Its content is
        // laid out from that row's top and may reach far past it, so its height
        // is claimed by the *last* row it covers rather than the first: that is
        // where the space it needs has to exist by. Charging it to the first row
        // instead made a two-row layout table - the shape every CV template uses,
        // content merged down the right column and the sidebar in the left cell of
        // row two - put the whole document into row one and start row two beneath
        // it, so the sidebar arrived after the content instead of beside it.
        var reaches = new List<(int LastRow, double Bottom)>();
        double y = top;

        foreach (TableRow row in table.Rows)
        {
            int rowIndex = heights.Count;
            double bottom = y;
            foreach (TableCell cell in row.Cells)
            {
                (double left, double width) = ColumnSpanBox(edges, cell);
                double cellBottom = LayoutBlocks(
                    document,
                    cell.Tables,
                    cell.ParagraphIndex,
                    cell.ParagraphEnd,
                    y,
                    new CellFrame(left + padding, Math.Max(1, width - (padding * 2))));

                if (cell.IsRowSpanContinuation)
                {
                    // The continuation holds no content of its own - what it covers
                    // lives in the cell that started the span - so whatever empty
                    // paragraph it carries does not get to set a height.
                    continue;
                }

                int rowSpan = Math.Max(1, cell.RowSpan);
                if (rowSpan > 1)
                    reaches.Add((rowIndex + rowSpan - 1, cellBottom));
                else
                    bottom = Math.Max(bottom, cellBottom);

                spans.Add((rowIndex, _cells.Count, cell.RowSpan));
                _cells.Add(new CellBox(new BRect(left, y, width, 0), cell.Shading, cell.Borders));
            }

            // Now the spanning cells that end here, which is the row that has to
            // be tall enough for them. Rows they merely pass through keep the
            // height their own cells asked for.
            foreach ((int lastRow, double reach) in reaches)
            {
                if (lastRow == rowIndex)
                    bottom = Math.Max(bottom, reach);
            }

            // A row is never shorter than a line, so an empty one is still a row,
            // nor shorter than the height it asked for. That height is a floor and
            // not a measurement: content that does not fit still makes the row
            // taller, because clipping a row's own text would lose it. It is what
            // holds the empty first row of a page-layout table open, and with it
            // the block the template put underneath.
            heights.Add(Math.Max(
                Math.Max(bottom - y, defaultLineHeight),
                row.MinHeight * _settings.Zoom));
            y += heights[^1];
        }

        foreach ((int row, int index, int rowSpan) in spans)
        {
            double height = 0;
            for (int r = row; r < Math.Min(heights.Count, row + Math.Max(1, rowSpan)); r++)
                height += heights[r];

            CellBox box = _cells[index];
            _cells[index] = box with
            {
                Bounds = new BRect(box.Bounds.Left, box.Bounds.Top, box.Bounds.Width, height),
            };
        }

        return y;
    }

    /// <summary>
    /// Works out the box every paragraph is laid out in: the content column for
    /// an ordinary paragraph, and the cell it sits in for one inside a table.
    /// </summary>
    /// <remarks>
    /// This runs before wrapping because wrapping needs the width, and before the
    /// list decorations because an indent is capped against the box it is in - a
    /// list inside a narrow cell would otherwise be capped against the page.
    /// </remarks>
    private void BuildFrames(RichTextDocument document, double contentWidth)
    {
        _frames.Clear();
        var full = new CellFrame(0, Math.Max(1, contentWidth));
        for (int i = 0; i < document.ParagraphCount; i++)
            _frames.Add(full);

        FrameBlocks(document, document.Tables, 0, document.ParagraphCount, full);
    }

    private void FrameBlocks(
        RichTextDocument document,
        IReadOnlyList<DocumentTable> tables,
        int from,
        int to,
        CellFrame frame)
    {
        int index = Math.Max(0, from);
        int end = Math.Min(to, _frames.Count);
        while (index < end)
        {
            if (DocumentTable.StartingAt(tables, index) is DocumentTable table)
            {
                double[] edges = ColumnEdges(table, frame);
                foreach (TableRow row in table.Rows)
                {
                    foreach (TableCell cell in row.Cells)
                    {
                        (double left, double width) = ColumnSpanBox(edges, cell);
                        double padding = table.CellPadding * _settings.Zoom;
                        var inner = new CellFrame(left + padding, Math.Max(1, width - (padding * 2)));

                        for (int i = cell.ParagraphIndex; i < Math.Min(cell.ParagraphEnd, _frames.Count); i++)
                            _frames[i] = inner;

                        FrameBlocks(document, cell.Tables, cell.ParagraphIndex, cell.ParagraphEnd, inner);
                    }
                }

                index = table.ParagraphEnd;
                continue;
            }

            index++;
        }
    }

    /// <summary>
    /// The x of every column boundary within <paramref name="frame"/>, left to
    /// right. A grid wider than the box it is in is scaled to fit rather than
    /// drawn off the edge, and one that states no widths divides the box evenly.
    /// </summary>
    private double[] ColumnEdges(DocumentTable table, CellFrame frame)
    {
        int columns = ColumnCount(table);
        var edges = new double[columns + 1];
        double total = table.TotalWidth * _settings.Zoom;
        double scale = total > 0 && total > frame.Width ? frame.Width / total : 1.0;

        double x = frame.Left;
        edges[0] = x;
        for (int i = 0; i < columns; i++)
        {
            x += i < table.ColumnWidths.Count && table.ColumnWidths[i] > 0
                ? table.ColumnWidths[i] * _settings.Zoom * scale
                : frame.Width / columns;
            edges[i + 1] = x;
        }

        return edges;
    }

    /// <summary>How many columns the grid has: what it states, or what its widest row uses.</summary>
    private static int ColumnCount(DocumentTable table)
    {
        int columns = table.ColumnWidths.Count;
        foreach (TableRow row in table.Rows)
        {
            foreach (TableCell cell in row.Cells)
                columns = Math.Max(columns, cell.ColumnIndex + cell.ColumnSpan);
        }

        return Math.Max(1, columns);
    }

    private static (double Left, double Width) ColumnSpanBox(double[] edges, TableCell cell)
    {
        int start = Math.Clamp(cell.ColumnIndex, 0, edges.Length - 1);
        int end = Math.Clamp(cell.ColumnIndex + cell.ColumnSpan, start + 1, edges.Length - 1);
        return (edges[start], Math.Max(1, edges[end] - edges[start]));
    }

    /// <summary>
    /// Works out every paragraph's list marker and left offsets, once per layout.
    /// Numbering runs on while consecutive paragraphs stay numbered and restarts
    /// otherwise, which is how the PDF writer numbers the same document. An indent
    /// is capped at half the width of the box the paragraph is in, so a deeply
    /// indented paragraph keeps a usable line to wrap into instead of one
    /// character per line.
    /// </summary>
    private void BuildDecorations(RichTextDocument document)
    {
        _decorations.Clear();
        int number = 0;
        ListKind previous = ListKind.None;

        for (int i = 0; i < document.ParagraphCount; i++)
        {
            RichTextParagraph paragraph = document.Paragraphs[i];
            ParagraphStyle style = paragraph.Style;
            number = style.ListKind == ListKind.Numbered && previous == ListKind.Numbered ? number + 1 : 1;
            previous = style.ListKind;

            string marker = style.ListKind switch
            {
                ListKind.Bullet => BulletMarker,
                ListKind.Numbered => string.Create(CultureInfo.InvariantCulture, $"{number}."),
                _ => string.Empty,
            };

            double indent = Math.Max(0, style.IndentLevel) * _settings.ZoomedIndentWidth;
            _decorations.Add(new ParagraphDecoration(marker, _settings.RunFont(paragraph.StyleAt(0)), indent, indent));
        }

        ApplyMarkerGutters(document);

        for (int i = 0; i < _decorations.Count; i++)
        {
            // Capped against the box the paragraph is in, not against the page: a
            // list in a narrow cell has less room to give away than the page does.
            double frameWidth = Frame(i).Width;
            double limit = frameWidth > 0 ? frameWidth / 2 : double.MaxValue;
            ParagraphDecoration decoration = _decorations[i];
            if (decoration.TextIndent <= limit)
                continue;

            _decorations[i] = decoration with
            {
                MarkerIndent = Math.Min(decoration.MarkerIndent, limit),
                TextIndent = limit,
            };
        }
    }

    /// <summary>
    /// Indents the text of each list item past a gutter wide enough for the widest
    /// marker in its own list, so the items stay lined up with each other where a
    /// list runs from item 9 into item 10. A run of items ends at the first
    /// paragraph that is not a list item at the same level, which is also where
    /// numbering restarts.
    /// </summary>
    private void ApplyMarkerGutters(RichTextDocument document)
    {
        int start = 0;
        while (start < _decorations.Count)
        {
            if (_decorations[start].Marker.Length == 0)
            {
                start++;
                continue;
            }

            int level = document.Paragraphs[start].Style.IndentLevel;
            double gutter = _settings.ZoomedIndentWidth;
            int end = start;
            while (end < _decorations.Count &&
                   _decorations[end].Marker.Length > 0 &&
                   document.Paragraphs[end].Style.IndentLevel == level)
            {
                ParagraphDecoration decoration = _decorations[end];
                gutter = Math.Max(gutter, BTextMeasurer.MeasureAdvance(decoration.Marker, decoration.Font) + (MarkerGap * _settings.Zoom));
                end++;
            }

            for (int i = start; i < end; i++)
                _decorations[i] = _decorations[i] with { TextIndent = _decorations[i].MarkerIndent + gutter };

            start = end;
        }
    }

    private int MeasureWrap(RichTextParagraph paragraph, int start, int segmentEnd, double contentWidth)
    {
        if (contentWidth <= 0 || _settings.Wrapping == RichEditWrapping.NoWrap)
            return segmentEnd;

        string text = paragraph.Text;
        double width = 0;
        int lastBreak = -1;
        int j = start;
        while (j < segmentEnd)
        {
            double advance = CharAdvance(paragraph, j, width, out int step);
            if (width + advance > contentWidth && j > start)
                break;

            width += advance;
            if (char.IsWhiteSpace(text[j]))
                lastBreak = j;
            j += step;
        }

        if (j >= segmentEnd)
            return segmentEnd;
        if (lastBreak >= start && lastBreak + 1 > start && lastBreak + 1 <= j)
            return lastBreak + 1;
        return j;
    }

    /// <summary>
    /// The extra width every space on a line is given so the line fills its
    /// column. Justification spends a line's slack inside the line instead of
    /// moving the line, which is what separates it from the other alignments.
    /// </summary>
    /// <remarks>
    /// A paragraph's last line is never stretched: its slack is only where the
    /// text happened to stop, and pulling a short closing line across the column
    /// is the one thing no typesetter does. Neither is a line with no spaces to
    /// spend the slack on, which would otherwise have its glyphs prised apart.
    /// This is the rule PdfPageLayout justifies with, so the editor and the
    /// printed page agree.
    /// </remarks>
    private double LineWordSpacing(RichTextParagraph paragraph, int start, int end, double available)
    {
        if (paragraph.Style.Alignment != TextAlignment.Justify || end >= paragraph.Text.Length)
            return 0;

        // Trailing whitespace does not count toward the line's width, so the
        // space a line wrapped on is not one of the gaps that gets widened.
        string text = paragraph.Text;
        int trimmed = end;
        while (trimmed > start && char.IsWhiteSpace(text[trimmed - 1]))
            trimmed--;

        int spaces = RichEditTextShaping.CountSpaces(text, start, trimmed);
        if (spaces == 0)
            return 0;

        double slack = available - MeasureAdvance(paragraph, start, trimmed);
        return slack > 0 ? slack / spaces : 0;
    }

    /// <summary>
    /// How far a visual line is pushed right by its paragraph's alignment: the
    /// slack left over on the line, halved for centered text and taken whole for
    /// right-aligned text. Trailing whitespace does not count toward the line's
    /// width, so a centered line does not drift left by the space it wrapped on,
    /// and the offset never goes negative, so a line too wide for its column still
    /// starts at the margin. This is the arithmetic the PDF writer places a line
    /// with, so the screen and the printed page agree.
    /// </summary>
    private double AlignmentOffset(RichTextParagraph paragraph, int start, int end, double available)
    {
        TextAlignment alignment = paragraph.Style.Alignment;
        // Justification starts at the margin like Left and spends its slack in
        // the line's own gaps; only Center and Right move the line as a whole.
        if (alignment is TextAlignment.Left or TextAlignment.Justify)
            return 0;

        string text = paragraph.Text;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;

        double slack = available - MeasureAdvance(paragraph, start, end);
        if (slack <= 0)
            return 0;

        return alignment == TextAlignment.Center ? slack / 2 : slack;
    }

    private double MeasureLineHeight(RichTextParagraph paragraph, int start, int end, double fallback)
    {
        if (start >= end || paragraph.Runs.Count == 0)
            return fallback;

        double height = fallback;
        int position = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            int runStart = position;
            int runEnd = position + run.Length;
            position = runEnd;
            if (Math.Max(start, runStart) >= Math.Min(end, runEnd))
                continue;

            // A picture makes its line as tall as it needs to be, or the image
            // would be clipped by the surrounding text's line height.
            height = run.Style.Image is InlineImage image
                ? Math.Max(height, ImageDisplaySize(image).Height + (_settings.ZoomedImageMargin * 2))
                : Math.Max(height, BTextMeasurer.GetLineHeight(_settings.RunFont(run.Style)));
        }

        return height;
    }

    // --- Measuring ---------------------------------------------------------

    /// <summary>
    /// The advance from the start of a visual line to <paramref name="end"/>.
    /// <paramref name="start"/> is the line's first offset, not an arbitrary one:
    /// a tab advances to the next tab stop, so its width is only defined once the
    /// distance from the line's text origin is known.
    /// </summary>
    private double MeasureAdvance(RichTextParagraph paragraph, int start, int end)
    {
        start = Math.Clamp(start, 0, paragraph.Length);
        end = Math.Clamp(end, start, paragraph.Length);
        if (end <= start)
            return 0;

        double advance = 0;
        int position = 0;
        foreach (StyleRun run in paragraph.Runs)
        {
            int runStart = position;
            int runEnd = position + run.Length;
            position = runEnd;

            int segmentStart = Math.Max(start, runStart);
            int segmentEnd = Math.Min(end, runEnd);
            if (segmentEnd <= segmentStart)
                continue;

            foreach ((string text, bool isTab) in RichEditTextShaping.SplitTabs(paragraph.Text, segmentStart, segmentEnd))
                advance = isTab ? _settings.NextTabStop(advance) : advance + MeasureRunText(text, run.Style);
        }

        return advance;
    }

    /// <summary>
    /// The advance of a substring of one run, counting a picture as the width it
    /// is drawn at. Caret placement, selection rectangles, and wrapping all come
    /// through here, so an image occupies the same horizontal space in each.
    /// </summary>
    private double MeasureRunText(string text, InlineStyle style)
    {
        if (style.Image is not InlineImage image)
            return RichEditTextShaping.MeasurePieces(text, style, _settings.RunFont(style));

        double advance = 0;
        foreach (char character in text)
        {
            advance += character == InlineImage.Placeholder
                ? ImageDisplaySize(image).Width
                : RichEditTextShaping.MeasurePieces(character.ToString(), style, _settings.RunFont(style));
        }

        return advance;
    }

    /// <summary>
    /// The advance of the character at <paramref name="index"/>, given the
    /// <paramref name="advance"/> already used on its visual line. Only a tab needs
    /// that context, and it needs it: what a tab is worth is the distance to the
    /// stop it lands on.
    /// </summary>
    private double CharAdvance(RichTextParagraph paragraph, int index, double advance, out int step)
    {
        string text = paragraph.Text;
        if (text[index] == '\t')
        {
            step = 1;
            return _settings.NextTabStop(advance) - advance;
        }

        InlineStyle style = paragraph.StyleAt(index);
        BFontStyle font = _settings.RunFont(style);
        if (text[index] == InlineImage.Placeholder && style.Image is InlineImage image)
        {
            step = 1;
            return ImageDisplaySize(image).Width;
        }

        if (index + 1 < text.Length && char.IsHighSurrogate(text[index]) && char.IsLowSurrogate(text[index + 1]))
        {
            step = 2;
            return RichEditTextShaping.MeasurePieces(text.Substring(index, 2), style, font);
        }

        step = 1;
        var key = new CharAdvanceCacheKey(font, text[index], style.Capitalization);
        if (_charAdvanceCache.TryGetValue(key, out double cachedAdvance))
            return cachedAdvance;

        double measuredAdvance = RichEditTextShaping.MeasurePieces(text[index].ToString(), style, font);
        if (_charAdvanceCache.Count >= MaxCharAdvanceCacheSize)
            _charAdvanceCache.Clear();

        _charAdvanceCache[key] = measuredAdvance;
        return measuredAdvance;
    }
}
