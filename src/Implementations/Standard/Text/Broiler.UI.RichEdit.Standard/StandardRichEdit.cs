using System;
using System.Collections.Generic;
using System.Globalization;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using Broiler.Input.Touch;
using Broiler.UI.Standard;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// The Broiler-drawn standard <see cref="UiRichEdit"/>. It lays out the document
/// into wrapped visual lines, renders per-run styled text (family, size, bold,
/// italic, underline, strike, foreground, and background), the selection, caret, and placeholder,
/// resolves tabs against the paragraph's tab stops, supports vertical scrolling,
/// and hit-tests points to positions. Keyboard, text,
/// and IME input drive caret/selection navigation plus editing and formatting
/// through the <see cref="UiRichEdit"/> command surface and its single undo model.
/// No native control or OS API is used.
/// </summary>
public sealed class StandardRichEdit : UiRichEdit, IStandardThemedControl, IUiTextEditor
{
    public void ApplyTheme(StandardThemeTokens theme)
    {
        Background = theme.Surface;
        Foreground = theme.Text;
        PlaceholderForeground = theme.TextDisabled;
        BorderColor = theme.Border;
        FocusRing = theme.FocusRing;
        SelectionBackground = theme.AccentSoft;
        SecondarySelectionBackground = theme.Warning;
        CaretColor = theme.Text;
    }

    private readonly List<VisualLine> _lines = [];

    /// <summary>The boxes wrapping shapes keep this layout's lines out of.</summary>
    private TextWrapExclusions _wrap = new();
    private readonly List<ParagraphDecoration> _decorations = [];
    private readonly List<CellFrame> _frames = [];
    private readonly List<CellBox> _cells = [];
    private readonly Dictionary<InlineImage, BImageHandle> _imageHandles = new(ReferenceEqualityComparer.Instance);
    private RichTextDocument? _layoutDocument;
    private BFontStyle? _layoutFont;
    private double _layoutZoom = double.NaN;
    private double _layoutWidth = double.NaN;
    private double _zoom = 1;
    private bool _layoutValid;
    private double _contentHeight;
    private double _scrollY;
    private UiTimestamp _lastClickTime;
    private BPoint _lastClickPosition;
    private bool _hasClicked;
    private string _compositionText = string.Empty;
    private bool _isDraggingScrollbar;
    private double _scrollbarDragOffset;
    private long? _touchContactId;
    private BPoint _touchStart;
    private BPoint _touchLast;
    private bool _isTouchScrolling;

    private const double TouchScrollThreshold = 6;

    /// <summary>The box an image with no known size is drawn in, and the minimum for any image.</summary>
    private const double FallbackImageExtent = 72;

    /// <summary>Space left around an inline image so it does not touch the text above and below it.</summary>
    private const double ImageMargin = 2;

    /// <summary>The glyph a bulleted paragraph is marked with, the one the DOCX and PDF writers emit.</summary>
    private const string BulletMarker = "\u2022";

    /// <summary>Space kept between a list marker and the text it introduces.</summary>
    private const double MarkerGap = 4;

    /// <summary>
    /// The default distance between tab stops: half an inch at 96 dpi, which is
    /// the tab every word processor starts a document with, and twice the default
    /// <see cref="IndentWidth"/> so tabs and indent levels share a grid.
    /// </summary>
    private const double DefaultTabStopWidth = 48;

    public BColor Background { get; set; } = StandardControlPaint.Surface;

    public BColor Foreground { get; set; } = StandardControlPaint.Text;

    public BColor PlaceholderForeground { get; set; } = StandardControlPaint.TextDisabled;

    public BColor BorderColor { get; set; } = StandardControlPaint.Border;

    public BColor FocusRing { get; set; } = StandardControlPaint.Focus;

    /// <summary>
    /// How thick the border is while the editor does not have focus. The default keeps the hairline
    /// every other Standard control draws; an editor that wants to read as paper rather than as a
    /// form field is the reason this is settable at all.
    /// </summary>
    public double BorderThickness { get; set; } = 1;

    /// <summary>
    /// How thick the border is while the editor has focus. Wider than
    /// <see cref="BorderThickness"/> is what makes the ring read as focus rather than as a colour
    /// change, but a host that wants a quieter frame can set the two equal.
    /// </summary>
    public double FocusRingThickness { get; set; } = 2;

    public BColor SelectionBackground { get; set; } = BColor.FromArgb(0xFF, 0xC7, 0xDD, 0xFA);

    public BColor SecondarySelectionBackground { get; set; } = BColor.FromArgb(0xFF, 0xFF, 0xF0, 0xB3);

    public BColor CaretColor { get; set; } = BColor.Black;

    public BColor ScrollbarTrack { get; set; } = BColor.FromArgb(0x33, 0x94, 0xA3, 0xB8);

    public BColor ScrollbarThumb { get; set; } = BColor.FromArgb(0xAA, 0x7D, 0x8D, 0xA3);

    public double ScrollbarThickness { get; set; } = 12;

    public double MinimumScrollbarThumbLength { get; set; } = 18;

    public BFontStyle Font { get; set; } = BFontStyle.Default;

    /// <summary>The smallest <see cref="Zoom"/> the surface will take.</summary>
    public const double MinimumZoom = 0.1;

    /// <summary>The largest <see cref="Zoom"/> the surface will take.</summary>
    public const double MaximumZoom = 10;

    /// <summary>
    /// How large the document is drawn against the size it states: 1 is the size
    /// it states, 2 is twice that. A value outside
    /// <see cref="MinimumZoom"/>..<see cref="MaximumZoom"/> is clamped into it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Zoom is a property of the view, not of the document. It multiplies every
    /// measurement layout reads from the document - font sizes, indents, tab
    /// stops, picture sizes, the page and its margins - and nothing it reads from
    /// the control - the padding, the border, the scrollbar. So the text grows
    /// inside chrome that stays where it is, and wraps to the column the window
    /// actually has rather than to one that grew with it. Nothing scaled here is
    /// written back, so a document saves at the size it was authored at whatever
    /// it is being read at.
    /// </para>
    /// <para>
    /// It is applied in layout rather than as a transform over the drawing, which
    /// is what keeps the caret, the selection, hit-testing and wrapping agreeing
    /// with the glyphs at every level.
    /// </para>
    /// </remarks>
    public double Zoom
    {
        get => _zoom;
        set
        {
            ThrowIfDisposed();
            double zoom = double.IsFinite(value) ? Math.Clamp(value, MinimumZoom, MaximumZoom) : 1;
            if (_zoom == zoom)
                return;

            // The content height scales with the zoom, so the scroll offset is
            // scaled with it: the reader stays on the passage they were reading
            // instead of being thrown back towards the top of a document that
            // just grew underneath them.
            _scrollY *= zoom / _zoom;
            _zoom = zoom;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public double PaddingX { get; set; } = 8;

    /// <summary>The desk the sheet lies on, so the paper reads as paper.</summary>
    public BColor PageSurround { get; set; } = BColor.FromArgb(0xFF, 0xE8, 0xEA, 0xEE);

    public double PaddingY { get; set; } = 6;

    /// <summary>
    /// The width of one indent level, and the smallest gutter a list marker is
    /// drawn in. It is the indent the PDF writer lays out with, so an indented or
    /// listed paragraph prints where it sits on screen.
    /// </summary>
    public double IndentWidth { get; set; } = 24;

    /// <summary>
    /// The distance between the default tab stops a tab character advances to,
    /// measured from where the paragraph's text starts rather than from the
    /// control, so a tab lines up the same way in an indented or listed paragraph
    /// as in a plain one. It is the tab stop the PDF writer lays out with, so a
    /// tabbed paragraph prints where it sits on screen.
    /// </summary>
    public double TabStopWidth { get; set; } = DefaultTabStopWidth;

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public double VerticalScrollOffset => _scrollY;

    /// <summary>The in-progress IME composition text, or empty when not composing.</summary>
    public string CompositionText => _compositionText;

    protected override bool IsCompositionActive => _compositionText.Length > 0;

    public UiTextEditorMetrics GetTextEditorMetrics()
    {
        int start = FlatIndex(Selection.Start);
        int end = FlatIndex(Selection.End);
        int compositionStart = _compositionText.Length > 0 ? FlatIndex(Selection.Focus) : -1;
        int compositionEnd = compositionStart < 0 ? -1 : compositionStart + _compositionText.Length;
        return new UiTextEditorMetrics(Document.PlainText.Length, start, end, compositionStart, compositionEnd);
    }

    /// <summary>
    /// A formatted document materializes its plain text to answer this. That is
    /// acceptable for RichEdit's document sizes and would not be for a source
    /// buffer, which is why the contract is a bounded range rather than a whole
    /// string: the cost stays with the implementation that can afford it.
    /// </summary>
    public string GetTextEditorRange(int start, int maxLength) =>
        UiTextEditorRange.Slice(Document.PlainText, start, maxLength);

    public bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        if (IsReadOnly || !IsEnabled)
            return false;

        string text = Document.PlainText;
        int caret = FlatIndex(Selection.Focus);
        int start = Math.Max(0, caret - Math.Max(0, beforeLength));
        int end = Math.Min(text.Length, caret + Math.Max(0, afterLength));
        if (!Selection.IsEmpty)
        {
            start = Math.Min(start, FlatIndex(Selection.Start));
            end = Math.Max(end, FlatIndex(Selection.End));
        }
        if (end <= start)
            return false;

        Selection = new RichTextRange(PositionFromFlatIndex(start), PositionFromFlatIndex(end));
        bool changed = DeleteCurrentSelection();
        EnsureCaretVisible();
        return changed;
    }

    public bool SetEditorSelection(int start, int end)
    {
        int textLength = Document.PlainText.Length;
        int clampedStart = Math.Clamp(Math.Min(start, end), 0, textLength);
        int clampedEnd = Math.Clamp(Math.Max(start, end), clampedStart, textLength);
        Selection = new RichTextRange(PositionFromFlatIndex(clampedStart), PositionFromFlatIndex(clampedEnd));
        EnsureCaretVisible();
        return true;
    }

    public bool SetComposingRegion(int start, int end) => SetEditorSelection(start, end);

    public bool PerformEditorAction(UiTextEditorAction action)
    {
        if (action == UiTextEditorAction.None)
            return false;

        if (AcceptsReturn && action is not UiTextEditorAction.Next and not UiTextEditorAction.Previous)
            return RunCommand(RichEditCommand.InsertParagraphBreak);

        Submit();
        return true;
    }

    protected override BSize MeasureCore(BSize availableSize)
    {
        double width = ClampDesired(PreferredSize.Width, availableSize.Width);
        double height = ClampDesired(PreferredSize.Height, availableSize.Height);
        return new BSize(width, height);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        EnsureLayout();
        BRenderList renderList = context.RenderList;
        BRect inner = InnerBounds;
        bool focused = Session?.FocusedElement == this;

        StandardControlPaint.FillRounded(renderList, Bounds, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled, CornerRadius);
        StandardControlPaint.StrokeRounded(
            renderList,
            Bounds,
            focused ? FocusRing : BorderColor,
            CornerRadius,
            focused ? FocusRingThickness : BorderThickness);

        renderList.PushClip(inner);
        DrawPage(renderList, inner);
        if (Document.PlainText.Length == 0 && _compositionText.Length == 0 && PlaceholderText.Length > 0)
        {
            renderList.DrawText(new BTextRun(PlaceholderText, ZoomedFont, PlaceholderForeground), new BPoint(ContentLeft, ContentTop - _scrollY));
        }
        else
        {
            DrawRunningShapes(renderList, inner, behindText: true);
            DrawShapes(renderList, inner, behindText: true);
            DrawCells(renderList, inner);
            DrawRunBackgrounds(renderList, inner);
            DrawRange(renderList, inner, SecondarySelection, SecondarySelectionBackground);
            DrawSelection(renderList, inner);
            DrawListMarkers(renderList, inner);
            DrawText(renderList, inner);
            DrawComposition(renderList, inner, focused);

            // The other half of the shapes: a document that says a picture sits in
            // front of the text gets one, and it covers the text the way it does
            // in the word processor the file came from. The caret is drawn after
            // this, so it stays findable under a shape.
            DrawShapes(renderList, inner, behindText: false);
            DrawRunningShapes(renderList, inner, behindText: false);
            DrawRunningText(renderList, inner);
        }

        DrawCaret(renderList, focused);
        renderList.PopClip();
        DrawScrollbar(renderList);
        PublishCaret(focused);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
            return false;

        return input.Kind switch
        {
            UiInputEventKind.PointerButton => HandlePointerButton(input),
            UiInputEventKind.PointerMove => HandlePointerMove(input),
            UiInputEventKind.PointerWheel => HandleWheel(input),
            UiInputEventKind.TouchContact => HandleTouch(input),
            UiInputEventKind.TextInput => HandleTextInput(input),
            UiInputEventKind.TextComposition => HandleTextComposition(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        // A layout built before the session existed sized its images from the
        // fallback box, because no host was there to decode them. Rebuild it now
        // that one is, or those sizes would stick until the document changes.
        _layoutValid = false;
    }

    protected override void OnDetached()
    {
        if (Session?.Host is IUiTextInputHost textInput)
            textInput.ClearCaret(this);

        // The handles belong to the detaching session's renderer, so they are
        // released here rather than kept for a session that cannot draw them.
        ReleaseImages();
        base.OnDetached();
    }

    // --- Rendering ---------------------------------------------------------

    private void DrawScrollbar(BRenderList renderList)
    {
        if (!HasVerticalScrollbar)
            return;

        BRect track = ScrollbarTrackBounds;
        BRect thumb = ScrollbarThumbBounds;
        StandardControlPaint.FillRounded(renderList, track, ScrollbarTrack, StandardControlPaint.PillRadius);
        StandardControlPaint.FillRounded(renderList, thumb, ScrollbarThumb, StandardControlPaint.PillRadius);
    }

    private void DrawSelection(BRenderList renderList, BRect inner)
    {
        DrawRange(renderList, inner, Selection, SelectionBackground);
    }

    private void DrawRange(BRenderList renderList, BRect inner, RichTextRange? range, BColor color)
    {
        if (range is not RichTextRange selection || selection.IsEmpty)
            return;

        RichTextPosition start = selection.Start;
        RichTextPosition end = selection.End;
        foreach (VisualLine line in _lines)
        {
            var lineStart = new RichTextPosition(line.ParagraphIndex, line.Start);
            var lineEnd = new RichTextPosition(line.ParagraphIndex, line.End);

            bool fullyInside = start <= lineStart && end >= lineEnd;
            if (!fullyInside && (end <= lineStart || start >= lineEnd))
                continue;

            double y = ContentTop + line.Top - _scrollY;
            if (y + line.Height < inner.Top || y > inner.Bottom)
                continue;

            int subStart = start.ParagraphIndex == line.ParagraphIndex ? Math.Clamp(start.Offset, line.Start, line.End) : line.Start;
            int subEnd = end.ParagraphIndex == line.ParagraphIndex ? Math.Clamp(end.Offset, line.Start, line.End) : line.End;
            if (start.ParagraphIndex < line.ParagraphIndex)
                subStart = line.Start;
            if (end.ParagraphIndex > line.ParagraphIndex)
                subEnd = line.End;

            RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
            double x1 = LineLeft(line) + AdvanceInLine(line, paragraph, subStart);
            double x2 = LineLeft(line) + AdvanceInLine(line, paragraph, subEnd);
            double width = x2 - x1;
            if (width <= 0)
            {
                if (!fullyInside)
                    continue;
                width = BTextMeasurer.MeasureAdvance(" ", ZoomedFont); // sliver marking an empty selected line
            }

            renderList.FillRect(new BRect(x1, y, width, line.Height), color);
        }
    }

    /// <summary>
    /// Draws the document's floating shapes under its text, each against the
    /// paragraph it is anchored to.
    /// </summary>
    /// <remarks>
    /// The same arithmetic the other engines place a shape with: x from the text
    /// column's left edge, y from the top of the anchoring paragraph. A gradient
    /// is banded because the render list fills rectangles and has no gradient of
    /// its own.
    /// </remarks>
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
    private void DrawPage(BRenderList renderList, BRect inner)
    {
        if (Page is not PageGeometry page)
            return;

        renderList.FillRect(inner, IsEnabled ? PageSurround : StandardControlPaint.SurfaceDisabled);

        BRect sheet = Sheet(page);
        renderList.FillRect(sheet, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled);
        renderList.StrokeRect(sheet, BorderColor, 1);
    }

    /// <summary>
    /// The paper, in device units. A sheet is at least a page tall, and taller
    /// when the text runs past the bottom - this surface flows rather than
    /// paginating, so the paper grows instead of a second sheet starting.
    /// </summary>
    private BRect Sheet(PageGeometry page) => new(
        PageLeft,
        Bounds.Top + PaddingY - _scrollY,
        page.Width,
        Math.Max(page.Height, _contentHeight + page.MarginTop + page.MarginBottom));

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
    private void DrawRunningShapes(BRenderList renderList, BRect inner, bool behindText)
    {
        if (Page is not PageGeometry page || Document.RunningContent.IsEmpty)
            return;

        double sheetTop = Sheet(page).Top;
        foreach (DocumentShape shape in RunningShapes())
        {
            if (shape.BehindText != behindText || shape.Width <= 0 || shape.Height <= 0)
                continue;

            var bounds = new BRect(
                ContentLeft + (shape.OffsetX * _zoom),
                sheetTop + (shape.OffsetY * _zoom),
                shape.Width * _zoom,
                shape.Height * _zoom);
            if (bounds.Bottom < inner.Top || bounds.Top > inner.Bottom)
                continue;

            if (shape.Fill is ShapeFill fill)
                FillShape(renderList, bounds, fill);

            if (shape.Image is InlineImage image)
                DrawShapeImage(renderList, image, bounds);

            if (!shape.Outline.IsEmpty && shape.Outline.A > 0)
                renderList.StrokeRect(bounds, shape.Outline, 1);

            DrawParagraphsInBox(renderList, shape.Paragraphs, bounds);
        }
    }

    /// <summary>The header's and footer's shapes for the sheet, in draw order.</summary>
    private IEnumerable<DocumentShape> RunningShapes()
    {
        RunningContent running = Document.RunningContent;
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
    private void DrawRunningText(BRenderList renderList, BRect inner)
    {
        if (Page is not PageGeometry page || Document.RunningContent.IsEmpty)
            return;

        RunningContent running = Document.RunningContent;
        BRect sheet = Sheet(page);
        double width = page.ContentWidth;

        DrawRunningBand(
            renderList,
            running.EffectiveHeader(PageSelection.First),
            new BRect(ContentLeft, sheet.Top, width, page.MarginTop),
            inner);

        DrawRunningBand(
            renderList,
            running.EffectiveFooter(PageSelection.First),
            new BRect(ContentLeft, sheet.Bottom - page.MarginBottom, width, page.MarginBottom),
            inner);
    }

    private void DrawRunningBand(
        BRenderList renderList,
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
            paragraphs,
            new BRect(band.Left, band.Top + ((band.Height - height) / 2), band.Width, height));
    }

    /// <summary>How tall the paragraphs are once wrapped to a width.</summary>
    private double MeasureParagraphs(IReadOnlyList<RichTextParagraph> paragraphs, double width)
    {
        double height = 0;
        foreach (RichTextParagraph paragraph in paragraphs)
        {
            BFontStyle font = RunFont(paragraph.Length > 0 ? paragraph.StyleAt(0) : InlineStyle.Default);
            double lineHeight = BTextMeasurer.GetLineHeight(font);
            foreach (string _ in WrapToWidth(paragraph.Text, font, width))
                height += lineHeight;
        }

        return height;
    }

    /// <summary>
    /// Draws one stacking layer of the floating shapes: the ones under the body
    /// text before it is drawn, the ones over it afterwards.
    /// </summary>
    /// <remarks>
    /// Two passes rather than one sorted list, because the layers are separated by
    /// everything else the control paints - cells, backgrounds, the selection, the
    /// text - and there is no single point in that sequence to sort against.
    /// </remarks>
    private void DrawShapes(BRenderList renderList, BRect inner, bool behindText)
    {
        foreach (DocumentShape shape in Document.Shapes)
        {
            if (shape.BehindText != behindText)
                continue;

            if (shape.Width <= 0 || shape.Height <= 0)
                continue;

            if (!TryParagraphTop(shape.ParagraphIndex, out double paragraphTop))
                continue;

            var bounds = new BRect(
                ContentLeft + (shape.OffsetX * _zoom),
                ContentTop + paragraphTop + (shape.OffsetY * _zoom) - _scrollY,
                shape.Width * _zoom,
                shape.Height * _zoom);
            if (bounds.Bottom < inner.Top || bounds.Top > inner.Bottom)
                continue;

            if (shape.Fill is ShapeFill fill)
                FillShape(renderList, bounds, fill);

            // Over the fill and under the outline, so a framed picture keeps its
            // frame.
            if (shape.Image is InlineImage image)
                DrawShapeImage(renderList, image, bounds);

            if (!shape.Outline.IsEmpty && shape.Outline.A > 0)
                renderList.StrokeRect(bounds, shape.Outline, 1);

            DrawParagraphsInBox(renderList, shape.Paragraphs, bounds);
        }
    }

    /// <summary>
    /// Draws a floating picture into its box. The box is the size the document
    /// stated for the frame, so unlike an inline picture there is nothing to
    /// measure: it fills what it was given.
    /// </summary>
    private void DrawShapeImage(BRenderList renderList, InlineImage image, BRect bounds)
    {
        BImageHandle handle = ResolveImage(image);
        if (!handle.IsValid)
        {
            // Same as an inline picture the backend could not decode: show where
            // it is rather than leaving a hole the reader cannot see.
            StandardControlPaint.StrokeRounded(
                renderList,
                bounds,
                IsEnabled ? Foreground : PlaceholderForeground,
                StandardControlPaint.ControlRadius,
                1);
            return;
        }

        renderList.DrawImage(
            handle,
            new BRect(0, 0, handle.PixelSize.Width, handle.PixelSize.Height),
            bounds,
            IsEnabled ? 1.0 : 0.5);
    }

    /// <summary>
    /// Paints the table cells: their backgrounds, then the edges they state.
    /// Under the text and over the shapes that draw behind it, so a shaded cell
    /// sits on a letterhead's stripe rather than under it.
    /// </summary>
    private void DrawCells(BRenderList renderList, BRect inner)
    {
        foreach (CellBox cell in _cells)
        {
            var bounds = new BRect(
                ContentLeft + cell.Bounds.Left,
                ContentTop + cell.Bounds.Top - _scrollY,
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

    /// <summary>
    /// Draws a shape's own text inside its box, wrapped to the box rather than to
    /// the page column, and clipped where it runs past the bottom.
    /// </summary>
    private void DrawParagraphsInBox(
        BRenderList renderList,
        IReadOnlyList<RichTextParagraph> paragraphs,
        BRect bounds)
    {
        if (paragraphs.Count == 0 || bounds.Width <= 0)
            return;

        double y = bounds.Top;
        foreach (RichTextParagraph paragraph in paragraphs)
        {
            InlineStyle inline = paragraph.Length > 0 ? paragraph.StyleAt(0) : InlineStyle.Default;
            BFontStyle font = RunFont(inline);
            double lineHeight = BTextMeasurer.GetLineHeight(font);
            BColor color = inline.Foreground.IsEmpty ? Foreground : inline.Foreground;

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

    /// <summary>The top of a paragraph's first line, which is what a shape hangs from.</summary>
    private bool TryParagraphTop(int paragraphIndex, out double top)
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

    private static BColor MixColor(BColor from, BColor to, double t) =>
        new(
            (byte)Math.Round(from.R + ((to.R - from.R) * t)),
            (byte)Math.Round(from.G + ((to.G - from.G) * t)),
            (byte)Math.Round(from.B + ((to.B - from.B) * t)),
            (byte)Math.Round(from.A + ((to.A - from.A) * t)));

    private void DrawRunBackgrounds(BRenderList renderList, BRect inner)
    {
        if (!IsEnabled)
            return;

        foreach (VisualLine line in _lines)
        {
            double y = ContentTop + line.Top - _scrollY;
            if (y + line.Height < inner.Top || y > inner.Bottom || line.End <= line.Start)
                continue;

            foreach (LineSegment segment in LineSegments(line))
            {
                if (!segment.Style.Background.IsEmpty && segment.Advance > 0)
                    renderList.FillRect(new BRect(segment.X, y, segment.Advance, line.Height), segment.Style.Background);
            }
        }
    }

    private void DrawText(BRenderList renderList, BRect inner)
    {
        BColor fallback = IsEnabled ? Foreground : PlaceholderForeground;
        foreach (VisualLine line in _lines)
        {
            double y = ContentTop + line.Top - _scrollY;
            if (y + line.Height < inner.Top || y > inner.Bottom || line.End <= line.Start)
                continue;

            foreach (LineSegment segment in LineSegments(line))
            {
                BColor color = IsEnabled && !segment.Style.Foreground.IsEmpty ? segment.Style.Foreground : fallback;
                if (segment.Image is InlineImage image)
                {
                    DrawInlineImage(renderList, image, segment, y, line.Height, color);
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
    private void DrawListMarkers(BRenderList renderList, BRect inner)
    {
        BColor fallback = IsEnabled ? Foreground : PlaceholderForeground;
        int drawnParagraph = -1;
        foreach (VisualLine line in _lines)
        {
            if (line.ParagraphIndex == drawnParagraph)
                continue;

            drawnParagraph = line.ParagraphIndex;
            ParagraphDecoration decoration = Decoration(line.ParagraphIndex);
            if (decoration.Marker.Length == 0)
                continue;

            double y = ContentTop + line.Top - _scrollY;
            if (y + line.Height < inner.Top || y > inner.Bottom)
                continue;

            InlineStyle style = Document.Paragraphs[line.ParagraphIndex].StyleAt(0);
            BColor color = IsEnabled && !style.Foreground.IsEmpty ? style.Foreground : fallback;
            // The marker travels with the text it introduces, so a centered or
            // right-aligned item keeps its bullet against the item, not the margin.
            renderList.DrawText(
                new BTextRun(decoration.Marker, decoration.Font, color),
                new BPoint(ContentLeft + decoration.MarkerIndent + line.AlignmentOffset, y));
        }
    }

    /// <summary>
    /// Draws one inline picture, bottom-aligned in its line the way Word sits an
    /// inline image on the text baseline. An image the backend could not decode
    /// is drawn as an outlined box, so the document still shows where it is.
    /// </summary>
    private void DrawInlineImage(
        BRenderList renderList,
        InlineImage image,
        LineSegment segment,
        double lineTop,
        double lineHeight,
        BColor color)
    {
        BSize size = ImageDisplaySize(image);
        double margin = ZoomedImageMargin;
        double height = Math.Min(size.Height, Math.Max(0, lineHeight - (margin * 2)));
        double top = lineTop + Math.Max(margin, lineHeight - height - margin);
        var destination = new BRect(segment.X, top, segment.Advance, height);

        BImageHandle handle = ResolveImage(image);
        if (handle.IsValid)
        {
            var source = new BRect(0, 0, handle.PixelSize.Width, handle.PixelSize.Height);
            renderList.DrawImage(handle, source, destination, IsEnabled ? 1.0 : 0.5);
            return;
        }

        StandardControlPaint.StrokeRounded(renderList, destination, color, StandardControlPaint.ControlRadius, 1);
    }

    private void DrawDecorations(BRenderList renderList, LineSegment segment, double y, double lineHeight, BColor color)
    {
        if (segment.Advance <= 0 || (!segment.Style.Underline && !segment.Style.Strikethrough))
            return;

        double thickness = Math.Max(1, Math.Round(segment.Font.Size / 14));
        if (segment.Style.Underline)
            renderList.FillRect(new BRect(segment.X, y + lineHeight - thickness - 1, segment.Advance, thickness), color);
        if (segment.Style.Strikethrough)
            renderList.FillRect(new BRect(segment.X, y + (lineHeight / 2), segment.Advance, thickness), color);
    }

    private void DrawComposition(BRenderList renderList, BRect inner, bool focused)
    {
        if (!focused || _compositionText.Length == 0)
            return;

        VisualLine line = LineForPosition(Selection.Focus).Line;
        double y = ContentTop + line.Top - _scrollY;
        if (y + line.Height < inner.Top || y > inner.Bottom)
            return;

        double x = CaretX(Selection.Focus);
        InlineStyle style = CaretInlineStyle;
        BFontStyle font = RunFont(style);
        double advance = MeasurePieces(_compositionText, style, font);
        BColor color = IsEnabled ? Foreground : PlaceholderForeground;

        // Preedit text is drawn with the capitalization it will take once
        // committed, so the text does not jump when the IME finishes.
        double pieceX = x;
        foreach (ShapedPiece piece in ShapePieces(_compositionText, style, font))
        {
            renderList.DrawText(new BTextRun(piece.Text, piece.Font, color), new BPoint(pieceX, y));
            pieceX += BTextMeasurer.MeasureAdvance(piece.Text, piece.Font);
        }

        renderList.FillRect(new BRect(x, y + line.Height - 2, advance, 1), color); // composition underline
    }

    /// <summary>
    /// Splits a visual line into contiguous styled segments, each carrying its
    /// resolved font, on-screen x origin, and advance. A tab yields a segment with
    /// no glyphs whose advance reaches the next tab stop, so the run background and
    /// underline it carries are still drawn across the gap it opens.
    /// </summary>
    private IEnumerable<LineSegment> LineSegments(VisualLine line)
    {
        RichTextParagraph paragraph = Document.Paragraphs[line.ParagraphIndex];
        double left = LineLeft(line);
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
                    double advance = MeasurePieces(single, run.Style, RunFont(run.Style));
                    yield return new LineSegment(single, run.Style, RunFont(run.Style), x, advance);
                    x += advance;
                }

                continue;
            }

            foreach ((string piece, bool isTab) in SplitTabs(paragraph.Text, segStart, segEnd))
            {
                if (isTab)
                {
                    double stop = left + NextTabStop(x - left);
                    yield return LineSegment.ForTab(run.Style, RunFont(run.Style), x, stop - x);
                    x = stop;
                    continue;
                }

                foreach (ShapedPiece shaped in ShapePieces(piece, run.Style, RunFont(run.Style)))
                {
                    // A justified line is drawn a word at a time. Widening a
                    // segment's advance alone would move only what comes after it,
                    // and a line drawn as one string has nothing after it - the
                    // backend would set it with its own spacing and the line would
                    // stay ragged. Each chunk carrying its own origin is what makes
                    // the gap real.
                    foreach (string chunk in StretchChunks(shaped.Text, line.WordSpacing))
                    {
                        double advance = BTextMeasurer.MeasureAdvance(chunk, shaped.Font) +
                                         (CountSpaces(chunk, 0, chunk.Length) * line.WordSpacing);
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
    private BSize ImageDisplaySize(InlineImage image)
    {
        // The model resolves this without touching the payload whenever the
        // document states a size or the resource knows its own pixels, so the
        // decode below is now only for a picture whose intrinsic size nothing
        // established — an encoded payload no registered codec could inspect.
        if (image.TryGetDisplaySize(out double width, out double height))
            return Zoomed(new BSize(width, height));

        BImageHandle handle = ResolveImage(image);
        if (handle.IsValid && handle.PixelSize.Width > 0 && handle.PixelSize.Height > 0)
            return Zoomed(handle.PixelSize);

        return Zoomed(new BSize(FallbackImageExtent, FallbackImageExtent));
    }

    /// <summary>A size the document states, as it is drawn.</summary>
    private BSize Zoomed(BSize size) =>
        _zoom == 1 ? size : new BSize(size.Width * _zoom, size.Height * _zoom);

    /// <summary>
    /// The backend handle for an image, created once per image object and kept
    /// until the control is detached. A host with no image capability, or bytes
    /// the backend cannot decode, cache as invalid so the failure is not retried
    /// on every frame.
    /// </summary>
    private BImageHandle ResolveImage(InlineImage image)
    {
        if (_imageHandles.TryGetValue(image, out BImageHandle cached))
            return cached;

        BImageHandle handle = BImageHandle.Invalid;
        if (Session?.Host is IUiImageHost imageHost && !image.Data.IsEmpty)
            handle = imageHost.CreateImage(image.Data.Span);

        _imageHandles[image] = handle;
        return handle;
    }

    private void ReleaseImages()
    {
        if (Session?.Host is IUiImageHost imageHost)
        {
            foreach (BImageHandle handle in _imageHandles.Values)
            {
                if (handle.IsValid)
                    imageHost.ReleaseImage(handle);
            }
        }

        _imageHandles.Clear();
    }

    /// <summary>How much smaller a small-caps letter is drawn than a full capital.</summary>
    private const double SmallCapsScale = 0.8;

    /// <summary>A stretch of a run as it is drawn: the glyphs, and the font to draw them with.</summary>
    private readonly record struct ShapedPiece(string Text, BFontStyle Font);

    /// <summary>
    /// The pieces a stored substring is drawn as. Capitalization is a display
    /// property — the document keeps the casing the author typed, and this is the
    /// only place it becomes capitals, so turning it off restores the original
    /// text exactly. Small caps additionally splits the substring wherever the
    /// stored case changes, so letters typed in lower case can be drawn smaller
    /// than the ones typed as capitals.
    /// </summary>
    private static IEnumerable<ShapedPiece> ShapePieces(string text, InlineStyle style, BFontStyle font)
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
    /// <see cref="DrawText"/> puts on screen.
    /// </summary>
    private static double MeasurePieces(string text, InlineStyle style, BFontStyle font)
    {
        if (style.Capitalization == TextCapitalization.None)
            return BTextMeasurer.MeasureAdvance(text, font);

        double advance = 0;
        foreach (ShapedPiece piece in ShapePieces(text, style, font))
            advance += BTextMeasurer.MeasureAdvance(piece.Text, piece.Font);

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
            return MeasurePieces(text, style, RunFont(style));

        double advance = 0;
        foreach (char character in text)
        {
            advance += character == InlineImage.Placeholder
                ? ImageDisplaySize(image).Width
                : MeasurePieces(character.ToString(), style, RunFont(style));
        }

        return advance;
    }

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
    /// in the units this control measures in, because a host set it here.
    /// </remarks>
    private BFontStyle RunFont(InlineStyle style)
    {
        double size = style.FontSize is > 0
            ? BFontStyle.PointsToPixels(style.FontSize.Value)
            : Font.Size;

        return Font with
        {
            FamilyName = string.IsNullOrWhiteSpace(style.FontFamily) ? Font.FamilyName : style.FontFamily,
            Size = ZoomedFontSize(size),
            Weight = style.Bold ? BFontWeight.Bold : Font.Weight,
            Slant = style.Italic ? BFontSlant.Italic : Font.Slant,
        };
    }

    /// <summary>
    /// The control's own font at the current zoom, which is what text with no run
    /// of its own - the placeholder, an empty line - is measured and drawn with.
    /// </summary>
    private BFontStyle ZoomedFont => Font with { Size = ZoomedFontSize(Font.Size) };

    /// <summary>
    /// A stated font size as it is drawn. A whole pixel is the floor: zoomed far
    /// enough out, a size that rounded away would leave a document that is laid
    /// out but not legible.
    /// </summary>
    private double ZoomedFontSize(double size) => Math.Max(1, size * _zoom);

    /// <summary>One indent level as it is drawn.</summary>
    private double ZoomedIndentWidth => IndentWidth * _zoom;

    /// <summary>The space kept above and below an inline picture, as it is drawn.</summary>
    private double ZoomedImageMargin => ImageMargin * _zoom;

    private void DrawCaret(BRenderList renderList, bool focused)
    {
        if (!focused || !IsEnabled)
            return;

        renderList.FillRect(CaretRect(Selection.Focus), CaretColor);
    }

    private void PublishCaret(bool focused)
    {
        if (!focused || Session?.Host is not IUiTextInputHost textInput)
            return;

        int caret = FlatIndex(Selection.Focus);
        int start = FlatIndex(Selection.Start);
        int end = FlatIndex(Selection.End);
        textInput.PublishCaret(new UiTextCaretInfo(this, CaretRect(Selection.Focus), caret, start, end - start, IsCompositionActive));
    }

    private bool HandleTextInput(UiInputEvent input) => InsertCommittedText(input.Text ?? string.Empty);

    private bool HandleTextComposition(UiInputEvent input)
    {
        TextCompositionState state = input.CompositionState ?? TextCompositionState.Updated;
        if (state is TextCompositionState.Started or TextCompositionState.Updated)
        {
            _compositionText = input.Text ?? string.Empty;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            return true;
        }

        if (state == TextCompositionState.Committed)
        {
            _compositionText = string.Empty;
            return InsertCommittedText(input.Text ?? string.Empty);
        }

        _compositionText = string.Empty;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    private bool InsertCommittedText(string text)
    {
        text = SanitizeCommittedText(text);
        if (text.Length == 0)
            return false;

        bool changed = ExecuteCommand(RichEditCommand.InsertText, text);
        EnsureCaretVisible();
        return changed;
    }

    // --- Input -------------------------------------------------------------

    private bool HandlePointerButton(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left)
            return false;

        if (input.MouseButtonTransition == MouseButtonTransition.Down)
        {
            Session?.SetFocus(this);
            Session?.CaptureInput(this);
            if (TryBeginScrollbarInteraction(input.Position))
                return true;

            RichTextPosition position = PositionFromPoint(input.Position);
            if (IsDoubleClick(input.Position))
            {
                SelectWordAt(position);
            }
            else
            {
                Selection = RichTextRange.Caret(position);
                EnsureCaretVisible();
            }

            UpdateClickState(input.Position);
            return true;
        }

        if (input.MouseButtonTransition == MouseButtonTransition.Up)
        {
            _isDraggingScrollbar = false;
            Session?.ReleaseInputCapture(this);
            return true;
        }

        return false;
    }

    private bool HandlePointerMove(UiInputEvent input)
    {
        if (Session?.CapturedElement != this)
            return false;

        if (_isDraggingScrollbar)
        {
            DragScrollbar(input.Position.Y);
            return true;
        }

        RichTextPosition position = PositionFromPoint(input.Position);
        Selection = new RichTextRange(Selection.Anchor, position);
        EnsureCaretVisible();
        return true;
    }

    private bool HandleWheel(UiInputEvent input)
    {
        if (VerticalScrollPolicy == RichEditScrollPolicy.Never)
            return false;

        double newScroll = ClampScroll(_scrollY - input.WheelDeltaNotches * DefaultLineHeight * 3);
        if (newScroll == _scrollY)
            return false;

        _scrollY = newScroll;
        Invalidate(UiInvalidationKind.Render);
        return true;
    }

    private bool HandleTouch(UiInputEvent input)
    {
        if (input.TouchContactState is not TouchContactState state)
            return false;

        if (state == TouchContactState.Pressed)
        {
            if (_touchContactId is not null)
                return false;

            _touchContactId = input.ContactId;
            _touchStart = input.Position;
            _touchLast = input.Position;
            _isTouchScrolling = false;
            return false;
        }

        if (_touchContactId != input.ContactId)
            return false;

        if (state == TouchContactState.Moved)
        {
            double totalX = input.Position.X - _touchStart.X;
            double totalY = input.Position.Y - _touchStart.Y;
            if (!_isTouchScrolling && Math.Sqrt((totalX * totalX) + (totalY * totalY)) >= TouchScrollThreshold)
                _isTouchScrolling = true;

            if (!_isTouchScrolling)
            {
                _touchLast = input.Position;
                return false;
            }

            SetVerticalScroll(_scrollY + (_touchLast.Y - input.Position.Y));
            _touchLast = input.Position;
            return true;
        }

        if (state is TouchContactState.Released or TouchContactState.Cancelled)
        {
            bool handled = _isTouchScrolling;
            _touchContactId = null;
            _isTouchScrolling = false;
            return handled;
        }

        return false;
    }

    private bool TryBeginScrollbarInteraction(BPoint position)
    {
        if (!HasVerticalScrollbar || !ScrollbarTrackBounds.Contains(position))
            return false;

        BRect thumb = ScrollbarThumbBounds;
        if (thumb.Contains(position))
        {
            _isDraggingScrollbar = true;
            _scrollbarDragOffset = position.Y - thumb.Top;
        }
        else
        {
            double page = ContentHeight * 0.85;
            SetVerticalScroll(_scrollY + (position.Y < thumb.Top ? -page : page));
        }

        return true;
    }

    private void DragScrollbar(double pointerY)
    {
        BRect track = ScrollbarTrackBounds;
        BRect thumb = ScrollbarThumbBounds;
        double travel = track.Height - thumb.Height;
        if (travel <= 0)
            return;

        double normalized = (pointerY - _scrollbarDragOffset - track.Top) / travel;
        SetVerticalScroll(Math.Clamp(normalized, 0, 1) * MaxScroll);
    }

    private void SetVerticalScroll(double value)
    {
        double newScroll = ClampScroll(value);
        if (newScroll == _scrollY)
            return;

        _scrollY = newScroll;
        Invalidate(UiInvalidationKind.Render);
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        bool control = input.KeyModifiers.HasFlag(KeyboardModifierState.Control);
        bool shift = input.KeyModifiers.HasFlag(KeyboardModifierState.Shift);

        if (control && HandleControlChord(input))
            return true;

        // Ctrl+Tab and Alt+Tab belong to the application and the desktop, not to
        // the text, so only a plain or shifted Tab is the editor's to answer.
        bool alt = input.KeyModifiers.HasFlag(KeyboardModifierState.Alt);
        if (!control && !alt && IsKey(input, BVirtualKey.Tab, "Tab"))
            return HandleTab(shift);

        if (IsKey(input, BVirtualKey.Enter, "Enter"))
        {
            if (shift)
                RunCommand(RichEditCommand.InsertLineBreak);
            else if (AcceptsReturn)
                RunCommand(RichEditCommand.InsertParagraphBreak);
            else
                Submit();
            return true;
        }
        if (IsKey(input, BVirtualKey.Back, "Backspace"))
        {
            if (DeleteBackward())
                EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, 0x2E, "Delete"))
        {
            if (DeleteForward())
                EnsureCaretVisible();
            return true;
        }
        if (IsKey(input, BVirtualKey.Left, "Left"))
        {
            MoveFocusTo(control ? WordLeft(Selection.Focus) : Document.PositionLeftOf(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Right, "Right"))
        {
            MoveFocusTo(control ? WordRight(Selection.Focus) : Document.PositionRightOf(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Up, "Up"))
        {
            MoveVertical(-1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Down, "Down"))
        {
            MoveVertical(1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.Home, "Home"))
        {
            MoveFocusTo(control ? Document.Start : VisualLineStart(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.End, "End"))
        {
            MoveFocusTo(control ? Document.End : VisualLineEnd(Selection.Focus), shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageUp, "PageUp"))
        {
            PageMove(-1, shift);
            return true;
        }
        if (IsKey(input, BVirtualKey.PageDown, "PageDown"))
        {
            PageMove(1, shift);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles the Tab key. In running text it types a tab, and the text after it
    /// is laid out at the next tab stop. Where a tab sets the level of a paragraph
    /// rather than the position of a word — a list item whose text the caret sits
    /// in front of, or a selection covering more than one paragraph — Tab demotes
    /// and Shift+Tab promotes, which is what Tab does in a word processor's lists.
    /// Shift+Tab in running text takes back the tab in front of the caret, and
    /// outdents the paragraph when there is no tab to take back.
    /// </summary>
    /// <remarks>
    /// The key is answered here rather than through the tab that some platforms
    /// also deliver as text input: <see cref="SanitizeCommittedText"/> drops that
    /// one, so a single press types a single tab on every head.
    /// </remarks>
    private bool HandleTab(bool shift)
    {
        if (TabSetsParagraphLevel())
            return RunCommand(shift ? RichEditCommand.Outdent : RichEditCommand.Indent);

        if (!shift)
            return RunCommand(RichEditCommand.InsertText, "\t");

        if (Selection.IsEmpty && IsTabBeforeCaret())
        {
            if (DeleteBackward())
                EnsureCaretVisible();
            return true;
        }

        if (CaretParagraph.Style.IndentLevel > 0)
            return RunCommand(RichEditCommand.Outdent);

        return true;
    }

    /// <summary>
    /// Whether Tab should change paragraph levels instead of typing a tab: the
    /// selection covers more than one paragraph, or the caret sits in front of the
    /// text of a list item, where a word processor demotes the item.
    /// </summary>
    private bool TabSetsParagraphLevel()
    {
        RichTextRange selection = Selection;
        if (selection.Start.ParagraphIndex != selection.End.ParagraphIndex)
            return true;

        return selection.IsEmpty &&
               CaretParagraph.Style.ListKind != ListKind.None &&
               Document.ClampPosition(selection.Focus).Offset == 0;
    }

    private bool IsTabBeforeCaret()
    {
        RichTextPosition caret = Document.ClampPosition(Selection.Focus);
        return caret.Offset > 0 && CaretParagraph.Text[caret.Offset - 1] == '\t';
    }

    private RichTextParagraph CaretParagraph =>
        Document.Paragraphs[Document.ClampPosition(Selection.Focus).ParagraphIndex];

    /// <summary>
    /// Handles the Ctrl-modified editing, clipboard, history, and inline-format
    /// shortcuts. Ctrl with a navigation key (arrows, Home, End) is left to the
    /// navigation handlers. Returns true when the chord was recognized.
    /// </summary>
    private bool HandleControlChord(UiInputEvent input)
    {
        if (IsKey(input, BVirtualKey.A, "A"))
        {
            SelectAllInternal();
            return true;
        }
        if (IsKey(input, BVirtualKey.C, "C"))
        {
            RunCommand(RichEditCommand.Copy);
            return true;
        }
        if (IsKey(input, 0x58, "X"))
        {
            RunCommand(RichEditCommand.Cut);
            return true;
        }
        if (IsKey(input, 0x56, "V"))
        {
            RunCommand(RichEditCommand.Paste);
            return true;
        }
        if (IsKey(input, 0x5A, "Z"))
        {
            RunCommand(RichEditCommand.Undo);
            return true;
        }
        if (IsKey(input, 0x59, "Y"))
        {
            RunCommand(RichEditCommand.Redo);
            return true;
        }
        if (IsKey(input, 0x42, "B"))
        {
            RunCommand(RichEditCommand.Bold);
            return true;
        }
        if (IsKey(input, 0x49, "I"))
        {
            RunCommand(RichEditCommand.Italic);
            return true;
        }
        if (IsKey(input, 0x55, "U"))
        {
            RunCommand(RichEditCommand.Underline);
            return true;
        }

        return false;
    }

    /// <summary>Runs a command through the shared undo model and keeps the caret in view.</summary>
    private bool RunCommand(RichEditCommand command, object? parameter = null)
    {
        bool changed = ExecuteCommand(command, parameter);
        EnsureCaretVisible();
        return changed;
    }

    /// <summary>
    /// Drops the control characters from text a platform commits. Tab is one of
    /// them on purpose: Windows and Android deliver a pressed Tab as a key event
    /// and again as committed text, and <see cref="HandleTab"/> already answered
    /// the key, so keeping it here would type two tabs for one press.
    /// </summary>
    private static string SanitizeCommittedText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (!char.IsControl(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    // --- Navigation --------------------------------------------------------

    private void SelectAllInternal()
    {
        Selection = new RichTextRange(Document.Start, Document.End);
        EnsureCaretVisible();
    }

    private void MoveFocusTo(RichTextPosition target, bool extend)
    {
        RichTextPosition anchor = extend ? Selection.Anchor : target;
        Selection = new RichTextRange(anchor, target);
        EnsureCaretVisible();
    }

    private void MoveVertical(int direction, bool extend)
    {
        EnsureLayout();
        (VisualLine _, int index) = LineForPosition(Selection.Focus);
        double caretX = CaretX(Selection.Focus);
        int target = index + direction;
        if (target < 0)
        {
            MoveFocusTo(Document.Start, extend);
            return;
        }
        if (target >= _lines.Count)
        {
            MoveFocusTo(Document.End, extend);
            return;
        }

        MoveFocusTo(PositionInLineAtX(_lines[target], caretX), extend);
    }

    private void PageMove(int direction, bool extend)
    {
        EnsureLayout();
        int linesPerPage = Math.Max(1, (int)(ContentHeight / DefaultLineHeight));
        (VisualLine _, int index) = LineForPosition(Selection.Focus);
        double caretX = CaretX(Selection.Focus);
        int target = Math.Clamp(index + (direction * linesPerPage), 0, _lines.Count - 1);
        MoveFocusTo(PositionInLineAtX(_lines[target], caretX), extend);
    }

    private RichTextPosition VisualLineStart(RichTextPosition position)
    {
        EnsureLayout();
        VisualLine line = LineForPosition(position).Line;
        return new RichTextPosition(line.ParagraphIndex, line.Start);
    }

    private RichTextPosition VisualLineEnd(RichTextPosition position)
    {
        EnsureLayout();
        VisualLine line = LineForPosition(position).Line;
        return new RichTextPosition(line.ParagraphIndex, line.End);
    }

    private RichTextPosition WordLeft(RichTextPosition position)
    {
        string text = Document.Paragraphs[position.ParagraphIndex].Text;
        int i = position.Offset;
        if (i <= 0)
            return Document.PositionLeftOf(position);
        i--;
        while (i > 0 && char.IsWhiteSpace(text[i]))
            i--;
        while (i > 0 && !char.IsWhiteSpace(text[i - 1]))
            i--;
        return new RichTextPosition(position.ParagraphIndex, i);
    }

    private RichTextPosition WordRight(RichTextPosition position)
    {
        string text = Document.Paragraphs[position.ParagraphIndex].Text;
        int n = text.Length;
        int i = position.Offset;
        if (i >= n)
            return Document.PositionRightOf(position);
        while (i < n && !char.IsWhiteSpace(text[i]))
            i++;
        while (i < n && char.IsWhiteSpace(text[i]))
            i++;
        return new RichTextPosition(position.ParagraphIndex, i);
    }

    private void SelectWordAt(RichTextPosition position)
    {
        string text = Document.Paragraphs[position.ParagraphIndex].Text;
        int start = Math.Clamp(position.Offset, 0, text.Length);
        int end = start;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;
        while (end < text.Length && IsWordChar(text[end]))
            end++;
        if (start == end)
        {
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
                start--;
            while (end < text.Length && char.IsWhiteSpace(text[end]))
                end++;
        }

        Selection = new RichTextRange(
            new RichTextPosition(position.ParagraphIndex, start),
            new RichTextPosition(position.ParagraphIndex, end));
        EnsureCaretVisible();
    }

    private void EnsureCaretVisible()
    {
        EnsureLayout();
        double contentHeight = ContentHeight;
        if (contentHeight <= 0)
            return;

        VisualLine line = LineForPosition(Selection.Focus).Line;
        double newScroll = _scrollY;
        if (line.Top < newScroll)
            newScroll = line.Top;
        else if (line.Top + line.Height > newScroll + contentHeight)
            newScroll = line.Top + line.Height - contentHeight;

        newScroll = ClampScroll(newScroll);
        if (newScroll != _scrollY)
        {
            _scrollY = newScroll;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    // --- Geometry and hit testing ------------------------------------------

    private (VisualLine Line, int Index) LineForPosition(RichTextPosition position)
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

    private RichTextPosition PositionFromPoint(BPoint point)
    {
        EnsureLayout();
        double localY = point.Y - ContentTop + _scrollY;
        VisualLine line = _lines[0];
        for (int i = 0; i < _lines.Count; i++)
        {
            line = _lines[i];
            if (localY < line.Top + line.Height)
                break;
        }

        return PositionInLineAtX(line, point.X);
    }

    private RichTextPosition PositionInLineAtX(VisualLine line, double x)
    {
        int offset = OffsetAtX(line, x - LineLeft(line));
        return new RichTextPosition(line.ParagraphIndex, line.Start + offset);
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

    private double CaretX(RichTextPosition position)
    {
        VisualLine line = LineForPosition(position).Line;
        int end = Math.Clamp(position.Offset, line.Start, line.End);
        return LineLeft(line) + AdvanceInLine(line, Document.Paragraphs[line.ParagraphIndex], end);
    }

    private BRect CaretRect(RichTextPosition position)
    {
        VisualLine line = LineForPosition(position).Line;
        double x = CaretX(position);
        double y = ContentTop + line.Top - _scrollY;
        return new BRect(x, y + 1, 1, Math.Max(1, line.Height - 2));
    }

    private int FlatIndex(RichTextPosition position)
    {
        RichTextDocument document = Document;
        int flat = 0;
        for (int i = 0; i < position.ParagraphIndex; i++)
            flat += document.Paragraphs[i].Length + 1;
        return flat + position.Offset;
    }

    private RichTextPosition PositionFromFlatIndex(int flatIndex)
    {
        flatIndex = Math.Clamp(flatIndex, 0, Document.PlainText.Length);
        for (int paragraphIndex = 0; paragraphIndex < Document.ParagraphCount; paragraphIndex++)
        {
            RichTextParagraph paragraph = Document.Paragraphs[paragraphIndex];
            if (flatIndex <= paragraph.Length || paragraphIndex == Document.ParagraphCount - 1)
                return new RichTextPosition(paragraphIndex, Math.Min(flatIndex, paragraph.Length));

            flatIndex -= paragraph.Length + 1;
        }

        return Document.End;
    }

    // --- Layout ------------------------------------------------------------

    private double DefaultLineHeight => BTextMeasurer.GetLineHeight(ZoomedFont);

    private BRect InnerBounds => new(
        Bounds.Left + PaddingX,
        Bounds.Top + PaddingY,
        Math.Max(0, Bounds.Width - (PaddingX * 2)),
        Math.Max(0, Bounds.Height - (PaddingY * 2)));

    /// <summary>
    /// The page this document is written for, at the size it is drawn. A document
    /// that says nothing is laid out to the width of the control, as it always
    /// was.
    /// </summary>
    private PageGeometry? Page =>
        Document.PageGeometry is PageGeometry geometry && geometry.IsUsable ? Zoomed(geometry) : null;

    /// <summary>
    /// A page as it is drawn. The paper is scaled with the text on it, or a
    /// zoomed-in document would run off a sheet that stayed the size it was.
    /// </summary>
    private PageGeometry Zoomed(PageGeometry page) =>
        _zoom == 1
            ? page
            : new PageGeometry(
                page.Width * _zoom,
                page.Height * _zoom,
                page.MarginLeft * _zoom,
                page.MarginRight * _zoom,
                page.MarginTop * _zoom,
                page.MarginBottom * _zoom,
                page.HeaderDistance * _zoom,
                page.FooterDistance * _zoom);

    /// <summary>
    /// Where the sheet starts. It is centred in whatever width the control has,
    /// and never left of the padding - a window narrower than the paper shows the
    /// left of the sheet rather than centring half of it out of view.
    /// </summary>
    private double PageLeft =>
        Page is PageGeometry page
            ? Bounds.Left + Math.Max(PaddingX, (Bounds.Width - page.Width) / 2)
            : Bounds.Left + PaddingX;

    private double ContentLeft =>
        Page is PageGeometry page
            ? PageLeft + page.MarginLeft
            : Bounds.Left + PaddingX + ShapeGutter;

    /// <summary>
    /// How far left of the text column the document's shapes reach, which the
    /// column moves over to make room for.
    /// </summary>
    /// <remarks>
    /// A letterhead anchors its stripe about 111 points left of the column,
    /// because on the printed page that is the margin it stands in. This surface
    /// has no page and fills whatever width it is given, so without a gutter the
    /// stripe would be drawn off the left edge and clipped away. A document with
    /// no margin content - which is nearly all of them - gets no gutter and the
    /// full width, so nothing changes for ordinary text.
    /// </remarks>
    private double ShapeGutter
    {
        get
        {
            double gutter = 0;
            foreach (DocumentShape shape in Document.Shapes)
            {
                if (shape.OffsetX < 0)
                    gutter = Math.Max(gutter, -shape.OffsetX * _zoom);
            }

            return gutter;
        }
    }

    private double ContentTop =>
        Bounds.Top + PaddingY + (Page is PageGeometry page ? page.MarginTop : 0);

    private double ContentWidth =>
        Page is PageGeometry page
            ? page.ContentWidth
            : Math.Max(0, Bounds.Width - (PaddingX * 2) - ShapeGutter);

    private double ContentHeight => Math.Max(0, Bounds.Height - (PaddingY * 2));

    /// <summary>The indent and list decoration of a paragraph layout has seen, else none.</summary>
    private ParagraphDecoration Decoration(int paragraphIndex) =>
        (uint)paragraphIndex < (uint)_decorations.Count ? _decorations[paragraphIndex] : ParagraphDecoration.None;

    /// <summary>
    /// Where a line of text starts: past its paragraph's indent and list gutter,
    /// then along by whatever its alignment pushes it.
    /// </summary>
    private double LineLeft(VisualLine line) =>
        ContentLeft + Frame(line.ParagraphIndex).Left +
        Decoration(line.ParagraphIndex).TextIndent + line.AlignmentOffset;

    public bool HasVerticalScrollbar => VerticalScrollPolicy == RichEditScrollPolicy.Always ||
                                        (VerticalScrollPolicy == RichEditScrollPolicy.Auto && MaxScroll > 0);

    private double MaxScroll => VerticalScrollPolicy == RichEditScrollPolicy.Never
        ? 0
        : Math.Max(0, _contentHeight - ContentHeight);

    private BRect ScrollbarTrackBounds
    {
        get
        {
            double thickness = Math.Clamp(ScrollbarThickness, 0, InnerBounds.Width);
            return new BRect(InnerBounds.Right - thickness, InnerBounds.Top, thickness, InnerBounds.Height);
        }
    }

    private BRect ScrollbarThumbBounds
    {
        get
        {
            BRect track = ScrollbarTrackBounds;
            if (track.Height <= 0)
                return BRect.Empty;

            double thumbHeight = MaxScroll <= 0
                ? track.Height
                : Math.Clamp(track.Height * (ContentHeight / Math.Max(ContentHeight, _contentHeight)),
                    Math.Min(MinimumScrollbarThumbLength, track.Height), track.Height);
            double top = track.Top;
            if (MaxScroll > 0)
                top += (track.Height - thumbHeight) * (_scrollY / MaxScroll);
            return new BRect(track.Left, top, track.Width, thumbHeight);
        }
    }

    private double ClampScroll(double value) => Math.Clamp(value, 0, MaxScroll);

    private string LineText(VisualLine line) =>
        Document.Paragraphs[line.ParagraphIndex].Text.Substring(line.Start, line.End - line.Start);

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

            foreach ((string text, bool isTab) in SplitTabs(paragraph.Text, segmentStart, segmentEnd))
                advance = isTab ? NextTabStop(advance) : advance + MeasureRunText(text, run.Style);
        }

        return advance;
    }

    /// <summary>
    /// Splits a stretch of a paragraph into the pieces between its tab characters
    /// and the tabs themselves, so each piece is measured and drawn as one string
    /// and each tab is resolved against the tab stops instead.
    /// </summary>
    private static IEnumerable<(string Text, bool IsTab)> SplitTabs(string text, int start, int end)
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
    /// The advance a tab reaching <paramref name="advance"/> lands on: the first
    /// tab stop strictly past it, so a tab always moves the text along even when
    /// it starts exactly on a stop.
    /// </summary>
    private double NextTabStop(double advance)
    {
        double width = (TabStopWidth > 0 ? TabStopWidth : DefaultTabStopWidth) * _zoom;
        return (Math.Floor(Math.Max(0, advance) / width) + 1) * width;
    }

    private void EnsureLayout()
    {
        double contentWidth = ContentWidth;
        if (_layoutValid &&
            ReferenceEquals(_layoutDocument, Document) &&
            _layoutWidth == contentWidth &&
            _layoutZoom == _zoom &&
            Equals(_layoutFont, Font))
        {
            return;
        }

        BuildLayout(contentWidth);
        _layoutValid = true;
        _layoutDocument = Document;
        _layoutWidth = contentWidth;
        _layoutZoom = _zoom;
        _layoutFont = Font;
        _scrollY = ClampScroll(_scrollY);
    }

    private void BuildLayout(double contentWidth)
    {
        _lines.Clear();
        _cells.Clear();
        _wrap = new TextWrapExclusions();
        RichTextDocument document = Document;
        BuildFrames(document, contentWidth);
        BuildDecorations(document, contentWidth);

        double y = LayoutBlocks(
            document,
            document.Tables,
            0,
            document.ParagraphCount,
            0,
            new CellFrame(0, Math.Max(1, contentWidth)));

        if (_lines.Count == 0)
        {
            _lines.Add(new VisualLine(0, 0, 0, 0, DefaultLineHeight, 0));
            y = DefaultLineHeight;
        }

        // The margins are part of what scrolls: a page's last line sits a bottom
        // margin above the end of the paper, not at it.
        _contentHeight = Page is PageGeometry page ? y + page.MarginTop + page.MarginBottom : y;
        ReleaseImagesNotIn(document);
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
        double defaultLineHeight = DefaultLineHeight;
        double frame = Math.Max(1, Frame(paragraphIndex).Width - Decoration(paragraphIndex).TextIndent);

        // A shape's box is known once the paragraph it hangs from has a top, so
        // this paragraph's shapes join the exclusions before its own lines are
        // wrapped. One anchored further down cannot narrow a line above it, which
        // is what a single forward pass can honestly say.
        RegisterWrapShapes(document, paragraphIndex, y);

        foreach ((int segmentStart, int segmentEnd) in HardSegments(paragraph.Text))
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
                _wrap.Add(shape, top + (shape.OffsetY * _zoom), _zoom);
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
        double padding = table.CellPadding * _zoom;
        double defaultLineHeight = DefaultLineHeight;
        var heights = new List<double>(table.Rows.Count);
        var spans = new List<(int Row, int Index, int RowSpan)>();
        double y = top;

        foreach (TableRow row in table.Rows)
        {
            double bottom = y;
            foreach (TableCell cell in row.Cells)
            {
                (double left, double width) = ColumnSpanBox(edges, cell);
                bottom = Math.Max(
                    bottom,
                    LayoutBlocks(
                        document,
                        cell.Tables,
                        cell.ParagraphIndex,
                        cell.ParagraphEnd,
                        y,
                        new CellFrame(left + padding, Math.Max(1, width - (padding * 2)))));

                if (cell.IsRowSpanContinuation)
                    continue;

                spans.Add((heights.Count, _cells.Count, cell.RowSpan));
                _cells.Add(new CellBox(new BRect(left, y, width, 0), cell.Shading, cell.Borders));
            }

            // A row is never shorter than a line, so an empty one is still a row.
            heights.Add(Math.Max(bottom - y, defaultLineHeight));
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
                        double padding = table.CellPadding * _zoom;
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
        double total = table.TotalWidth * _zoom;
        double scale = total > 0 && total > frame.Width ? frame.Width / total : 1.0;

        double x = frame.Left;
        edges[0] = x;
        for (int i = 0; i < columns; i++)
        {
            x += i < table.ColumnWidths.Count && table.ColumnWidths[i] > 0
                ? table.ColumnWidths[i] * _zoom * scale
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

    /// <summary>The box a paragraph is laid out in, or the whole column when layout has not seen it.</summary>
    private CellFrame Frame(int paragraphIndex) =>
        (uint)paragraphIndex < (uint)_frames.Count
            ? _frames[paragraphIndex]
            : new CellFrame(0, Math.Max(1, ContentWidth));

    /// <summary>
    /// Works out every paragraph's list marker and left offsets, once per layout.
    /// Numbering runs on while consecutive paragraphs stay numbered and restarts
    /// otherwise, which is how the PDF writer numbers the same document. An indent
    /// is capped at half the content width, so a deeply indented paragraph keeps a
    /// usable line to wrap into instead of one character per line.
    /// </summary>
    private void BuildDecorations(RichTextDocument document, double contentWidth)
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

            double indent = Math.Max(0, style.IndentLevel) * ZoomedIndentWidth;
            _decorations.Add(new ParagraphDecoration(marker, RunFont(paragraph.StyleAt(0)), indent, indent));
        }

        ApplyMarkerGutters(document);

        _ = contentWidth;
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
            double gutter = ZoomedIndentWidth;
            int end = start;
            while (end < _decorations.Count &&
                   _decorations[end].Marker.Length > 0 &&
                   document.Paragraphs[end].Style.IndentLevel == level)
            {
                ParagraphDecoration decoration = _decorations[end];
                gutter = Math.Max(gutter, BTextMeasurer.MeasureAdvance(decoration.Marker, decoration.Font) + (MarkerGap * _zoom));
                end++;
            }

            for (int i = start; i < end; i++)
                _decorations[i] = _decorations[i] with { TextIndent = _decorations[i].MarkerIndent + gutter };

            start = end;
        }
    }

    /// <summary>
    /// Releases the handles of images the document no longer contains. Layout is
    /// the right moment: it runs when the document actually changed, not once per
    /// frame, and opening a second document would otherwise leave the first
    /// document's pictures uploaded for the life of the control.
    /// </summary>
    private void ReleaseImagesNotIn(RichTextDocument document)
    {
        if (_imageHandles.Count == 0)
            return;

        var live = new HashSet<InlineImage>(ReferenceEqualityComparer.Instance);
        foreach (RichTextParagraph paragraph in document.Paragraphs)
        {
            foreach (StyleRun run in paragraph.Runs)
            {
                if (run.Style.Image is InlineImage image)
                    live.Add(image);
            }
        }

        // A floating picture is in the document without being in a paragraph, and
        // releasing its handle here would drop the logo off every letterhead the
        // moment layout ran.
        foreach (DocumentShape shape in document.Shapes)
        {
            if (shape.Image is InlineImage image)
                live.Add(image);
        }

        List<InlineImage>? stale = null;
        foreach (KeyValuePair<InlineImage, BImageHandle> entry in _imageHandles)
        {
            if (!live.Contains(entry.Key))
                (stale ??= []).Add(entry.Key);
        }

        if (stale is null)
            return;

        var imageHost = Session?.Host as IUiImageHost;
        foreach (InlineImage image in stale)
        {
            if (_imageHandles.Remove(image, out BImageHandle handle) && handle.IsValid)
                imageHost?.ReleaseImage(handle);
        }
    }

    private static IEnumerable<(int Start, int End)> HardSegments(string text)
    {
        // U+2028 (LINE SEPARATOR) is a soft break inside a paragraph; each one
        // forces a new visual line and is not itself rendered.
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

    private int MeasureWrap(RichTextParagraph paragraph, int start, int segmentEnd, double contentWidth)
    {
        if (contentWidth <= 0)
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
    /// How far a visual line is pushed right by its paragraph's alignment: the
    /// slack left over on the line, halved for centered text and taken whole for
    /// right-aligned text. Trailing whitespace does not count toward the line's
    /// width, so a centered line does not drift left by the space it wrapped on,
    /// and the offset never goes negative, so a line too wide for its column still
    /// starts at the margin. This is the arithmetic the PDF writer places a line
    /// with, so the screen and the printed page agree.
    /// </summary>
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

        int spaces = CountSpaces(text, start, trimmed);
        if (spaces == 0)
            return 0;

        double slack = available - MeasureAdvance(paragraph, start, trimmed);
        return slack > 0 ? slack / spaces : 0;
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
        return advance + (CountSpaces(paragraph.Text, line.Start, end) * line.WordSpacing);
    }

    /// <summary>
    /// Splits a piece so a justified line can be drawn one word at a time. Each
    /// chunk keeps the spaces that follow it, and the next chunk starts past the
    /// width those spaces were widened by, so the space glyph is still drawn at
    /// its own width. A line that is not justified yields its piece whole, so
    /// nothing about how it is drawn changes.
    /// </summary>
    private static IEnumerable<string> StretchChunks(string text, double wordSpacing)
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

    private static int CountSpaces(string text, int start, int end)
    {
        int spaces = 0;
        for (int i = start; i < end; i++)
        {
            if (text[i] == ' ')
                spaces++;
        }

        return spaces;
    }

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
                ? Math.Max(height, ImageDisplaySize(image).Height + (ZoomedImageMargin * 2))
                : Math.Max(height, BTextMeasurer.GetLineHeight(RunFont(run.Style)));
        }

        return height;
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
            return NextTabStop(advance) - advance;
        }

        InlineStyle style = paragraph.StyleAt(index);
        BFontStyle font = RunFont(style);
        if (text[index] == InlineImage.Placeholder && style.Image is InlineImage image)
        {
            step = 1;
            return ImageDisplaySize(image).Width;
        }

        if (index + 1 < text.Length && char.IsHighSurrogate(text[index]) && char.IsLowSurrogate(text[index + 1]))
        {
            step = 2;
            return MeasurePieces(text.Substring(index, 2), style, font);
        }

        step = 1;
        return MeasurePieces(text[index].ToString(), style, font);
    }

    private bool IsDoubleClick(BPoint point)
    {
        if (!_hasClicked || Session is null)
            return false;

        TimeSpan delta = Session.Clock.Now.Elapsed - _lastClickTime.Elapsed;
        bool quick = delta >= TimeSpan.Zero && delta <= TimeSpan.FromMilliseconds(400);
        bool near = Math.Abs(point.X - _lastClickPosition.X) <= 4 && Math.Abs(point.Y - _lastClickPosition.Y) <= 4;
        return quick && near;
    }

    private void UpdateClickState(BPoint point)
    {
        _lastClickTime = Session?.Clock.Now ?? default;
        _lastClickPosition = point;
        _hasClicked = true;
    }

    private static bool IsWordChar(char character) => char.IsLetterOrDigit(character) || character == '_';

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));

    /// <summary>
    /// One laid-out line. Its alignment offset is what the paragraph's alignment
    /// adds to the line's left origin, resolved at layout time because it depends
    /// only on what layout already knows: the document, the content width, and
    /// the font.
    /// </summary>
    private readonly record struct VisualLine(
        int ParagraphIndex,
        int Start,
        int End,
        double Top,
        double Height,
        double AlignmentOffset,
        double WordSpacing = 0);

    /// <summary>
    /// What a paragraph's list and indent add to it: the marker drawn in the
    /// gutter — empty when the paragraph is not a list item — the font that marker
    /// is drawn with, and the left offsets of the marker and of the text. Both
    /// offsets are relative to <see cref="ContentLeft"/>, so scrolling the document
    /// or moving the control does not invalidate them.
    /// </summary>
    /// <summary>
    /// The box a paragraph is laid out in: its left offset from
    /// <see cref="ContentLeft"/>, and the width it wraps into. An ordinary
    /// paragraph gets the whole content column; one in a table gets its cell.
    /// </summary>
    private readonly record struct CellFrame(double Left, double Width);

    /// <summary>One table cell's box, in the same space a line's top is measured in.</summary>
    private readonly record struct CellBox(BRect Bounds, BColor Shading, CellBorders Borders);

    private readonly record struct ParagraphDecoration(
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
    private readonly record struct LineSegment(
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
}
