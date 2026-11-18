using System;
using System.Collections.Generic;
using System.Linq;
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
internal sealed partial class RichEditLayout : IRichEditGeometry
{
    /// <summary>The box an image with no known size is drawn in, and the minimum for any image.</summary>
    private const double FallbackImageExtent = 72;

    /// <summary>The glyph a bulleted paragraph is marked with, the one the DOCX and PDF writers emit.</summary>

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

    private readonly struct CharAdvanceCacheKey(BFontStyle font, char character, TextCapitalization capitalization, string? language) : IEquatable<CharAdvanceCacheKey>
    {
        public readonly BFontStyle Font = font;
        public readonly char Character = character;
        public readonly TextCapitalization Capitalization = capitalization;
        public readonly string? Language = language;

        public bool Equals(CharAdvanceCacheKey other) =>
            Character == other.Character && Capitalization == other.Capitalization && Language == other.Language && Font.Equals(other.Font);

        public override int GetHashCode() =>
            HashCode.Combine(Font, Character, (int)Capitalization, Language);

        public override bool Equals(object? obj) =>
            obj is CharAdvanceCacheKey other && Equals(other);
    }

    private readonly Dictionary<CharAdvanceCacheKey, double> _charAdvanceCache = new(256);
    private const int MaxCharAdvanceCacheSize = 1024;

    // Resolved fonts by run style. RichEditLayoutSettings.RunFont builds a new BFontStyle on every
    // call, and layout and painting asked for one per run, segment, and measurement.
    private readonly Dictionary<InlineStyle, BFontStyle> _runFonts = new();
    private (BFontStyle? Font, double Zoom) _runFontsFor;
    private const int MaxRunFontCacheSize = 256;

    // A visual line's segments, kept across frames: painting split, shaped, and copied the text of
    // every visible line again on each frame. Cleared whenever the lines are rebuilt.
    private readonly Dictionary<VisualLine, (double ContentLeft, LineSegment[] Segments)> _segments = new();
    private const int MaxSegmentCacheSize = 4096;

    public RichEditLayout(RichEditImageCache images)
    {
        ArgumentNullException.ThrowIfNull(images);
        _images = images;
    }

    /// <summary>The settings the current lines were built with.</summary>
    public RichEditLayoutSettings Settings => _settings;

    /// <summary>
    /// The inline style at an offset, like <see cref="RichTextParagraph.StyleAt"/> but without its
    /// foreach over an IReadOnlyList, which boxed an enumerator for every character wrapping measured.
    /// </summary>
    public static InlineStyle StyleAt(RichTextParagraph paragraph, int offset)
    {
        IReadOnlyList<StyleRun> runs = paragraph.Runs;
        if (runs.Count == 0)
            return InlineStyle.Default;

        int position = 0;
        for (int index = 0; index < runs.Count; index++)
        {
            StyleRun run = runs[index];
            if (offset < position + run.Length)
                return run.Style;
            position += run.Length;
        }

        return runs[^1].Style;
    }

    /// <summary>The font a run style is drawn with under the current settings; resolved once per style.</summary>
    public BFontStyle RunFont(InlineStyle style)
    {
        if (!Equals(_runFontsFor.Font, _settings.Font) || _runFontsFor.Zoom != _settings.Zoom || _runFonts.Count >= MaxRunFontCacheSize)
        {
            _runFonts.Clear();
            _runFontsFor = (_settings.Font, _settings.Zoom);
        }

        if (!_runFonts.TryGetValue(style, out BFontStyle? font))
            _runFonts[style] = font = _settings.RunFont(style);
        return font;
    }

    /// <summary>
    /// The visual lines, top to bottom. Never empty once built: a document with no
    /// text still has a line for the caret to stand on.
    /// </summary>
    public IReadOnlyList<VisualLine> Lines => _lines;

    public double ScriptTop(InlineStyle style)
    {
        if (style.Shift is not { IsNone: false } shift || style.Image is not null)
            return 0;

        BFontStyle full = RunFont(style with { Shift = null });
        BFontStyle scaled = RunFont(style);
        double ascentDifference = (full.Size - scaled.Size) * 0.8;
        return shift.Position switch
        {
            BaselinePosition.AutoSuperscript => 0,
            BaselinePosition.AutoSubscript => BTextMeasurer.GetLineHeight(full) - BTextMeasurer.GetLineHeight(scaled),
            _ when double.IsFinite(shift.Offset) => ascentDifference - BTextMeasurer.GetLineHeight(full) * shift.Offset,
            _ => 0,
        };
    }

    private bool IsHidden(InlineStyle style) => !_settings.ShowHiddenText && style.Hidden;

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
        _revision++;
        _isValid = true;
        return true;
    }

    /// <summary>Makes the next <see cref="Update"/> rebuild whatever it is given.</summary>
    public void Invalidate()
    {
        _isValid = false;
        _segments.Clear();
    }

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
        Decoration(line.ParagraphIndex).MarkerIndent + line.AlignmentOffset - line.TextOnlyOffset;

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
    private double[] _bottomPrefix = [];

    public VisualLine LineAt(double y) => _lines.Count == 0 ? default : _lines[LineIndexAt(y)];

    private int LineIndexAt(double y)
    {
        int low = 0;
        int high = _lines.Count - 1;
        int result = _lines.Count - 1;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            if (y < _bottomPrefix[mid])
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    public VisualLine LineAt(BPoint point) => _lines.Count == 0 ? default : _lines[LineIndexAt(point)];

    private int LineIndexAt(BPoint point)
    {
        int closest = LineIndexAt(point.Y);
        double distance = double.MaxValue;
        for (int i = 0; i < _lines.Count; i++)
        {
            VisualLine line = _lines[i];
            CellFrame frame = Frame(line.ParagraphIndex);
            if (line.CellClip is { } clip)
            {
                if (!clip.Contains(point) || line.Top >= clip.Bottom || line.Top + line.Height <= clip.Top)
                    continue;
            }
            else if (point.X < frame.Left || point.X > frame.Left + frame.Width)
                continue;
            double gap = Math.Max(line.Top - point.Y, point.Y - line.Top - line.Height);
            if (gap < distance)
            {
                distance = gap;
                closest = i;
            }
        }
        return closest;
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
            if (_bottomPrefix[mid] >= contentMinY)
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
        return CaretX(line, position.Offset, contentLeft);
    }

    private double CaretX(VisualLine line, int offset, double contentLeft)
    {
        int end = Math.Clamp(offset, line.Start, line.End);
        return LineLeft(line, contentLeft) + AdvanceInLine(line, Document.Paragraphs[line.ParagraphIndex], end);
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
    public LineSegment[] LineSegments(VisualLine line, double contentLeft)
    {
        if (_segments.TryGetValue(line, out var cached) && cached.ContentLeft == contentLeft)
            return cached.Segments;

        LineSegment[] segments = [.. ComputeLineSegments(line, contentLeft)];
        // An inline image's size can change once it decodes, so lines holding one are measured each time.
        if (!Array.Exists(segments, segment => segment.Image is not null))
        {
            if (_segments.Count >= MaxSegmentCacheSize)
                _segments.Clear();
            _segments[line] = (contentLeft, segments);
        }

        return segments;
    }

    private IEnumerable<LineSegment> ComputeLineSegments(VisualLine line, double contentLeft)
    {
        RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
        double left = LineLeft(line, contentLeft);
        double x = left;
        int pos = 0;
        // An index loop: foreach over IReadOnlyList boxes an enumerator for every paragraph and line.
        for (int runIndex = 0; runIndex < paragraph.Runs.Count; runIndex++)
        {
            StyleRun run = paragraph.Runs[runIndex];
            int runStart = pos;
            int runEnd = pos + run.Length;
            pos = runEnd;

            int segStart = Math.Max(runStart, line.Start);
            int segEnd = Math.Min(runEnd, line.End);
            if (segEnd <= segStart || IsHidden(run.Style))
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
                    double advance = RichEditTextShaping.MeasurePieces(single, run.Style, RunFont(run.Style));
                    yield return new LineSegment(single, run.Style, RunFont(run.Style), x, advance);
                    x += advance;
                }

                continue;
            }

            int offset = segStart;
            foreach ((string piece, bool isTab) in RichEditTextShaping.SplitTabs(paragraph.Text, segStart, segEnd))
            {
                int pieceStart = offset;
                offset += piece.Length;
                if (isTab)
                {
                    (double gap, string? leader) = TabAdvance(paragraph, pieceStart, line.Start, x - left);
                    yield return LineSegment.ForTab(run.Style, RunFont(run.Style), x, gap, leader);
                    x += gap;
                    continue;
                }

                foreach (ShapedPiece shaped in RichEditTextShaping.ShapePieces(piece, run.Style, RunFont(run.Style)))
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
        return advance + (CountVisibleSpaces(paragraph, line.Start, end) * line.WordSpacing);
    }

    private int OffsetAtX(VisualLine line, double localX)
    {
        RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
        double advance = 0;
        int index = line.Start;
        while (index < line.End)
        {
            double charAdvance = CharAdvance(paragraph, index, line.Start, advance, out int step);
            if (paragraph.Text[index] == ' ' && line.WordSpacing != 0 && !IsHidden(StyleAt(paragraph, index)))
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
        _segments.Clear();
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
        _lines.Sort((a, b) =>
        {
            int order = a.Top.CompareTo(b.Top);
            if (order == 0)
                order = Frame(a.ParagraphIndex).Left.CompareTo(Frame(b.ParagraphIndex).Left);
            return order == 0 ? a.Start.CompareTo(b.Start) : order;
        });
        _bottomPrefix = new double[_lines.Count];
        double lastBottom = 0;
        for (int index = 0; index < _lines.Count; index++)
            _bottomPrefix[index] = lastBottom = Math.Max(lastBottom, _lines[index].Top + _lines[index].Height);


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
        ParagraphStyle style = paragraph.Style;
        double defaultNaturalHeight = paragraph.Length == 0 && paragraph.MarkStyle.FontSize is > 0
            ? BTextMeasurer.GetLineHeight(RunFont(paragraph.MarkStyle)) : _settings.DefaultLineHeight;
        double defaultLineHeight = LineHeight(style, defaultNaturalHeight);
        double frame = Frame(paragraphIndex).Width - Decoration(paragraphIndex).TextIndent - Points(style.RightIndentIn);
        double previousAfter = paragraphIndex > 0 && Frame(paragraphIndex) == Frame(paragraphIndex - 1)
            ? document.Paragraphs[paragraphIndex - 1].Style.SpacingAfter : 0;
        y += Points(document.StyleDefaults.SpaceAbove(style.SpacingBefore, previousAfter));
        if (style.Border is { } border)
        {
            y += Points(BorderInset(border.Edges.Top, border.Spacing.Top));
            frame -= Points(BorderInset(border.Edges.Right, border.Spacing.Right));
        }
        ParagraphDecoration decoration = Decoration(paragraphIndex);
        double firstIndent = decoration.Marker.Length == 0 ? Points(style.FirstLineIndentIn)
            : HasExactListIndent(style)
                ? Math.Max(0, Points(style.FirstLineIndentIn) + BTextMeasurer.MeasureAdvance(decoration.Marker, decoration.Font) + MarkerGap * _settings.Zoom)
                : 0;
        bool firstLine = true;

        // A shape's box is known once the paragraph it hangs from has a top, so
        // this paragraph's shapes join the exclusions before its own lines are
        // wrapped. One anchored further down cannot narrow a line above it, which
        // is what a single forward pass can honestly say.
        RegisterWrapShapes(document, paragraphIndex, y);

        double bottomInset = style.Border is { } lower ? BorderInset(lower.Edges.Bottom, lower.Spacing.Bottom) : 0;
        double after = Points(bottomInset + Math.Max(0, style.SpacingAfter));
#if BROILER_SHARED_PARAGRAPH_COMPOSITION
        if (TryLayoutSharedParagraph(paragraph, paragraphIndex, frame, firstIndent, defaultLineHeight, ref y))
            return y + after;
#endif

        foreach ((int segmentStart, int segmentEnd) in RichEditTextShaping.HardSegments(paragraph.Text,
            _settings.ShowHiddenText ? null : offset => !IsHidden(StyleAt(paragraph, offset))))
        {
            if (segmentStart == segmentEnd)
            {
                double indent = firstLine ? firstIndent : 0;
                firstLine = false;
                TextBand empty = LineBand(ref y, defaultLineHeight, Math.Max(1, frame - indent));
                _lines.Add(new VisualLine(
                    paragraphIndex, segmentStart, segmentEnd, y, defaultLineHeight,
                    indent + empty.Left + AlignmentOffset(paragraph, segmentStart, segmentEnd, empty.Width),
                    LineWordSpacing(paragraph, segmentStart, segmentEnd, empty.Width), TextOnlyOffset: indent));
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
                double indent = firstLine ? firstIndent : 0;
                firstLine = false;
                TextBand band = LineBand(ref y, defaultLineHeight, Math.Max(1, frame - indent));
                int lineEnd = MeasureWrap(paragraph, i, segmentEnd, band.Width);
                (double natural, double topPadding) = MeasureLineHeight(paragraph, i, lineEnd, _settings.DefaultLineHeight);
                double lineHeight = LineHeight(style, natural);
                _lines.Add(new VisualLine(
                    paragraphIndex, i, lineEnd, y, lineHeight,
                    indent + band.Left + AlignmentOffset(paragraph, i, lineEnd, band.Width),
                    LineWordSpacing(paragraph, i, lineEnd, band.Width), topPadding, indent));
                y += lineHeight;
                i = lineEnd;
            }
        }

        return y + after;
    }

    private double Points(double value) => BFontStyle.PointsToPixels(value) * _settings.Zoom;

    internal static double BorderInset(TableBorder edge, double spacing) => edge.IsVisible ? edge.Width + Math.Max(0, spacing) : 0;

    private double LineHeight(ParagraphStyle style, double natural)
    {
        double pixelPerPoint = Points(1);
        return Math.Max(1, style.LineBoxHeight(natural / pixelPerPoint) * pixelPerPoint);
    }

    private static bool HasExactListIndent(ParagraphStyle style) => style.LeftIndent.HasValue || style.FirstLineIndent.HasValue;

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
        double defaultLineHeight = _settings.DefaultLineHeight;
        var heights = new List<double>(table.Rows.Count);
        var spans = new List<(int Row, int Index, TableCell Cell, int FirstLine, int LastLine,
            int FirstNestedBox, int LastNestedBox, double ContentHeight)>();

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
                if (cell.IsRowSpanContinuation)
                    continue;
                (double left, double width) = ColumnSpanBox(edges, cell);
                var padding = cell.PaddingFor(table);
                int firstLine = _lines.Count;
                int firstBox = _cells.Count;
                double textTop = y + padding.Top * _settings.Zoom;
                double cellBottom = LayoutBlocks(
                    document,
                    cell.Tables,
                    cell.ParagraphIndex,
                    cell.ParagraphEnd,
                    textTop,
                    new CellFrame(left + padding.Left * _settings.Zoom,
                        Math.Max(1, width - (padding.Left + padding.Right) * _settings.Zoom)));
                double contentHeight = cellBottom - textTop;
                cellBottom += padding.Bottom * _settings.Zoom;

                int rowSpan = Math.Max(1, cell.RowSpan);
                if (rowSpan > 1)
                    reaches.Add((rowIndex + rowSpan - 1, cellBottom));
                else
                    bottom = Math.Max(bottom, cellBottom);

                spans.Add((rowIndex, _cells.Count, cell, firstLine, _lines.Count, firstBox, _cells.Count, contentHeight));
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

            // Minimum heights grow for content; exact heights retain the stated
            // size and clip painting without deleting the overflowing text.
            heights.Add(row.ExactHeight ? row.MinHeight * _settings.Zoom : Math.Max(
                Math.Max(bottom - y, defaultLineHeight),
                row.MinHeight * _settings.Zoom));
            y += heights[^1];
        }

        foreach (var span in spans)
        {
            double height = 0;
            for (int r = span.Row; r < Math.Min(heights.Count, span.Row + Math.Max(1, span.Cell.RowSpan)); r++)
                height += heights[r];
            CellBox box = _cells[span.Index];
            BRect bounds = new(box.Bounds.Left, box.Bounds.Top, box.Bounds.Width, height);
            _cells[span.Index] = box with
            {
                Bounds = bounds,
            };
            TableCellPadding padding = span.Cell.PaddingFor(table);
            double extra = Math.Max(0, height - (padding.Top + padding.Bottom) * _settings.Zoom - span.ContentHeight);
            double shift = span.Cell.VerticalAlignment switch
            {
                CellVerticalAlignment.Center => extra / 2,
                CellVerticalAlignment.Bottom => extra,
                _ => 0,
            };
            for (int line = span.FirstLine; line < span.LastLine; line++)
            {
                VisualLine old = _lines[line];
                BRect clip = old.CellClip is { } nestedClip
                    ? new BRect(nestedClip.Left, nestedClip.Top + shift, nestedClip.Width, nestedClip.Height).Intersect(bounds)
                    : bounds;
                _lines[line] = old with { Top = old.Top + shift, CellClip = clip };
            }
            for (int nested = span.FirstNestedBox; nested < span.LastNestedBox; nested++)
            {
                CellBox old = _cells[nested];
                _cells[nested] = old with
                {
                    Bounds = new BRect(old.Bounds.Left, old.Bounds.Top + shift, old.Bounds.Width, old.Bounds.Height).Intersect(bounds),
                };
            }
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
                        if (cell.IsRowSpanContinuation)
                            continue;
                        TableCellPadding padding = cell.PaddingFor(table);
                        var inner = new CellFrame(left + padding.Left * _settings.Zoom,
                            Math.Max(1, width - (padding.Left + padding.Right) * _settings.Zoom));

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
        double indent = Math.Min(frame.Width - 1, Math.Max(0, table.LeftIndent * _settings.Zoom));
        double available = Math.Max(1, frame.Width - indent);
        double total = table.TotalWidth * _settings.Zoom;
        double desired = table.WidthPercent is { } percent ? available * percent / 100
            : table.PreferredWidth is { } preferred ? preferred * _settings.Zoom
            : total > 0 ? total : available;
        var weights = new double[columns];
        for (int index = 0; index < columns; index++)
            weights[index] = index < table.ColumnWidths.Count && table.ColumnWidths[index] > 0 ? table.ColumnWidths[index] : 1;
        if (table.AutoFit)
        {
            Array.Fill(weights, 1d);
            foreach (TableCell cell in table.Rows.SelectMany(row => row.Cells).Where(cell => !cell.IsRowSpanContinuation))
            {
                double natural = 0;
                for (int paragraph = cell.ParagraphIndex; paragraph < Math.Min(Document.ParagraphCount, cell.ParagraphEnd); paragraph++)
                    natural = Math.Max(natural, MeasureAdvance(Document.Paragraphs[paragraph], 0, Document.Paragraphs[paragraph].Length));
                TableCellPadding padding = cell.PaddingFor(table);
                natural = (natural + (padding.Left + padding.Right) * _settings.Zoom) / Math.Max(1, cell.ColumnSpan);
                for (int column = cell.ColumnIndex; column < Math.Min(columns, cell.ColumnIndex + cell.ColumnSpan); column++)
                    weights[column] = Math.Max(weights[column], natural);
            }
            if (table.WidthPercent is null && table.PreferredWidth is null)
                desired = weights.Sum();
        }
        double extent = Math.Clamp(desired, 1, available);
        double offset = table.Alignment switch
        {
            TableAlignment.Center => (available - extent) / 2,
            TableAlignment.End => available - extent,
            _ => 0,
        };
        double scale = extent / weights.Sum();
        double x = frame.Left + indent + offset;
        edges[0] = x;
        for (int i = 0; i < columns; i++)
        {
            x += weights[i] * scale;
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
    /// Explicit list references keep their counters across interruptions. An indent
    /// is capped at half the width of the box the paragraph is in, so a deeply
    /// indented paragraph keeps a usable line to wrap into instead of one
    /// character per line.
    /// </summary>
    private void BuildDecorations(RichTextDocument document)
    {
        _decorations.Clear();
        for (int i = 0; i < document.ParagraphCount; i++)
            _decorations.Add(ParagraphDecoration.None);

        DecorateFlow(document, document.Tables, 0, document.ParagraphCount, new ListNumberer());

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

    private void DecorateFlow(RichTextDocument document, IReadOnlyList<DocumentTable> tables, int start, int end, ListNumberer numberer)
    {
        for (int index = start; index < end; index++)
        {
            if (DocumentTable.StartingAt(tables, index) is { } table)
            {
                foreach (TableRow row in table.Rows)
                {
                    foreach (TableCell cell in row.Cells)
                        DecorateFlow(document, cell.Tables, cell.ParagraphIndex, cell.ParagraphEnd, numberer.ForNestedFlow());
                }
                index = table.ParagraphEnd - 1;
                continue;
            }

            RichTextParagraph paragraph = document.Paragraphs[index];
            ParagraphStyle style = paragraph.Style;
            string marker = numberer.Advance(style) ?? string.Empty;
            double indent = style.LeftIndent.HasValue ? Points(style.LeftIndentIn()) :
                Math.Max(0, style.IndentLevel) * _settings.ZoomedIndentWidth;
            if (style.Border is { } border)
                indent += Points(BorderInset(border.Edges.Left, border.Spacing.Left));
            double markerIndent = marker.Length != 0 && HasExactListIndent(style)
                ? indent + Points(style.FirstLineIndentIn) : indent;
            _decorations[index] = new ParagraphDecoration(marker, RunFont(StyleAt(paragraph, 0)), markerIndent, indent);
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
            if (_decorations[start].Marker.Length == 0 || HasExactListIndent(document.Paragraphs[start].Style))
            {
                start++;
                continue;
            }

            ParagraphStyle first = document.Paragraphs[start].Style;
            double gutter = _settings.ZoomedIndentWidth;
            int end = start;
            while (end < _decorations.Count &&
                   _decorations[end].Marker.Length > 0 &&
                   !HasExactListIndent(document.Paragraphs[end].Style) &&
                   document.Paragraphs[end].Style.IndentLevel == first.IndentLevel &&
                   document.Paragraphs[end].Style.ListKind == first.ListKind &&
                   ReferenceEquals(document.Paragraphs[end].Style.List?.Definition, first.List?.Definition) &&
                   Frame(end) == Frame(start))
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
            double advance = CharAdvance(paragraph, j, start, width, out int step);
            if (width + advance > contentWidth && width > 0)
                break;

            width += advance;
            if (char.IsWhiteSpace(text[j]) && !IsHidden(StyleAt(paragraph, j)))
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
        if (paragraph.Text.AsSpan(start, end - start).Contains('\t'))
            return 0;

        int trimmed = TrimLineEnd(paragraph, start, end);

        int spaces = CountVisibleSpaces(paragraph, start, trimmed);
        if (spaces == 0)
            return 0;

        double slack = available - MeasureAdvance(paragraph, start, trimmed);
        return slack > 0 ? slack / spaces : 0;
    }

    private int CountVisibleSpaces(RichTextParagraph paragraph, int start, int end)
    {
        if (_settings.ShowHiddenText)
            return RichEditTextShaping.CountSpaces(paragraph.Text, start, end);

        int spaces = 0;
        for (int offset = start; offset < end; offset++)
        {
            if (paragraph.Text[offset] == ' ' && !IsHidden(StyleAt(paragraph, offset)))
                spaces++;
        }
        return spaces;
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

        end = TrimLineEnd(paragraph, start, end);

        double slack = available - MeasureAdvance(paragraph, start, end);
        if (slack <= 0)
            return 0;

        return alignment == TextAlignment.Center ? slack / 2 : slack;
    }

    private int TrimLineEnd(RichTextParagraph paragraph, int start, int end)
    {
        while (end > start && (char.IsWhiteSpace(paragraph.Text[end - 1]) || IsHidden(StyleAt(paragraph, end - 1))))
            end--;
        return end;
    }

    private (double Height, double TopPadding) MeasureLineHeight(RichTextParagraph paragraph, int start, int end, double fallback)
    {
        if (start >= end || paragraph.Runs.Count == 0)
            return (fallback, 0);

        double height = fallback;
        double topPadding = 0;
        int position = 0;
        // An index loop: foreach over IReadOnlyList boxes an enumerator for every paragraph and line.
        for (int runIndex = 0; runIndex < paragraph.Runs.Count; runIndex++)
        {
            StyleRun run = paragraph.Runs[runIndex];
            int runStart = position;
            int runEnd = position + run.Length;
            position = runEnd;
            if (Math.Max(start, runStart) >= Math.Min(end, runEnd) || IsHidden(run.Style))
                continue;

            // A picture makes its line as tall as it needs to be, or the image
            // would be clipped by the surrounding text's line height.
            if (run.Style.Image is InlineImage image)
            {
                height = Math.Max(height, ImageDisplaySize(image).Height + (_settings.ZoomedImageMargin * 2));
                continue;
            }

            double top = ScriptTop(run.Style);
            double drawnHeight = BTextMeasurer.GetLineHeight(RunFont(run.Style));
            double fullHeight = run.Style.Shift is null ? drawnHeight : BTextMeasurer.GetLineHeight(RunFont(run.Style with { Shift = null }));
            topPadding = Math.Max(topPadding, -top);
            height = Math.Max(height, Math.Max(fullHeight, top + drawnHeight));
        }

        return (height + topPadding, topPadding);
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
        // An index loop: foreach over IReadOnlyList boxes an enumerator for every paragraph and line.
        for (int runIndex = 0; runIndex < paragraph.Runs.Count; runIndex++)
        {
            StyleRun run = paragraph.Runs[runIndex];
            int runStart = position;
            int runEnd = position + run.Length;
            position = runEnd;

            int segmentStart = Math.Max(start, runStart);
            int segmentEnd = Math.Min(end, runEnd);
            if (segmentEnd <= segmentStart || IsHidden(run.Style))
                continue;

            int offset = segmentStart;
            foreach ((string text, bool isTab) in RichEditTextShaping.SplitTabs(paragraph.Text, segmentStart, segmentEnd))
            {
                advance += isTab ? TabAdvance(paragraph, offset, start, advance).Advance : MeasureRunText(text, run.Style);
                offset += text.Length;
            }
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
            return RichEditTextShaping.MeasurePieces(text, style, RunFont(style));

        double advance = 0;
        foreach (char character in text)
        {
            advance += character == InlineImage.Placeholder
                ? ImageDisplaySize(image).Width
                : RichEditTextShaping.MeasurePieces(character.ToString(), style, RunFont(style));
        }

        return advance;
    }

    /// <summary>
    /// The advance of the character at <paramref name="index"/>, given the
    /// <paramref name="advance"/> already used on its visual line. Only a tab needs
    /// that context, and it needs it: what a tab is worth is the distance to the
    /// stop it lands on.
    /// </summary>
    private (double Advance, string? Leader) TabAdvance(RichTextParagraph paragraph, int offset, int lineStart, double advance)
    {
        ParagraphStyle style = paragraph.Style;
        if (style.TabStops is { Count: > 0 } tabs)
        {
            double lineOrigin = style.LeftIndent.HasValue ? Points(style.LeftIndentIn()) :
                Math.Max(0, style.IndentLevel) * _settings.ZoomedIndentWidth;
            if (lineStart == 0 && style.ListKind == ListKind.None)
                lineOrigin += Points(style.FirstLineIndentIn);

            int start = offset + 1;
            int end = start;
            while (end < paragraph.Length && paragraph.Text[end] is not ('\t' or '\u2028'))
                end++;

            double? fieldWidth = null;
            foreach (ParagraphTabStop tab in tabs)
            {
                double position = Points(tab.Position) - lineOrigin;
                if (position <= advance)
                    continue;

                double alignedWidth = 0;
                if (tab.Alignment == TabAlignment.Decimal)
                {
                    int separator = paragraph.Text.IndexOf(tab.DecimalCharacter, start, end - start);
                    while (separator >= 0 && IsHidden(StyleAt(paragraph, separator)))
                        separator = paragraph.Text.IndexOf(tab.DecimalCharacter, separator + 1, end - separator - 1);
                    alignedWidth = MeasureAdvance(paragraph, start, separator < 0 ? end : separator);
                }
                else if (tab.Alignment is TabAlignment.Center or TabAlignment.Right)
                {
                    fieldWidth ??= MeasureAdvance(paragraph, start, end);
#if BROILER_SHARED_PARAGRAPH_COMPOSITION
                    alignedWidth = Broiler.Documents.Model.Text.ParagraphTabGeometry.FieldAnchor(tab.Alignment, fieldWidth.Value);
#else
                    alignedWidth = tab.Alignment == TabAlignment.Center ? fieldWidth.Value / 2 : fieldWidth.Value;
#endif
                }

                double gap = position - alignedWidth - advance;
                if (gap > 0)
                    return (gap, tab.Leader);
            }
        }

        return (_settings.NextTabStop(advance) - advance, null);
    }

    private double CharAdvance(RichTextParagraph paragraph, int index, int lineStart, double advance, out int step)
    {
        string text = paragraph.Text;
        InlineStyle style = StyleAt(paragraph, index);
        if (IsHidden(style))
        {
            step = index + 1 < text.Length && char.IsSurrogatePair(text, index) ? 2 : 1;
            return 0;
        }

        if (text[index] == '\t')
        {
            step = 1;
            return TabAdvance(paragraph, index, lineStart, advance).Advance;
        }

        BFontStyle font = RunFont(style);
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
        var key = new CharAdvanceCacheKey(font, text[index], style.Capitalization,
            style.Capitalization == TextCapitalization.None ? null : style.Language);
        if (_charAdvanceCache.TryGetValue(key, out double cachedAdvance))
            return cachedAdvance;

        double measuredAdvance = RichEditTextShaping.MeasurePieces(text[index].ToString(), style, font);
        if (_charAdvanceCache.Count >= MaxCharAdvanceCacheSize)
            _charAdvanceCache.Clear();

        _charAdvanceCache[key] = measuredAdvance;
        return measuredAdvance;
    }
}
