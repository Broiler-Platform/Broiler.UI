using System;
using Broiler.Documents.Model;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
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
/// <remarks>
/// The control coordinates collaborators that each own one kind of state: the
/// <see cref="RichEditLayout"/> owns the lines and measures against them, the
/// <see cref="RichEditImageCache"/> owns the pictures' backend handles, the
/// <see cref="RichEditPainter"/> draws what those two describe, and the
/// <see cref="RichEditScroller"/> owns the scroll offset and the gestures that move
/// it. What stays here is the public surface, input routing, and deciding when
/// the layout has to be brought up to date.
/// </remarks>
public sealed partial class StandardRichEdit : UiRichEdit, IStandardThemedControl, IUiTextEditor
{
    private readonly RichEditImageCache _images;
    private readonly RichEditLayout _layout;
    private readonly RichEditPainter _painter;
    private readonly RichEditScroller _scroller = new();
    private readonly RichEditClickTracker _clicks = new();
    private double _indentWidth = 24;
    private double _tabStopWidth = RichEditLayoutSettings.DefaultTabStopWidth;
    private double _zoom = 1;
    private string _compositionText = string.Empty;

    public StandardRichEdit()
    {
        _images = new RichEditImageCache(() => Session?.Host as IUiImageHost);
        _layout = new RichEditLayout(_images);
        _painter = new RichEditPainter(_layout, _images);
    }

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
        ContextMenuBackground = theme.Surface;
        ContextMenuForeground = theme.Text;
        ContextMenuDisabledForeground = theme.TextDisabled;
        ContextMenuHighlight = theme.AccentSoft;
        ContextMenuBorderColor = theme.Border;
    }

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

            _scroller.ScaleOffset(zoom / _zoom);
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
    public double IndentWidth
    {
        get => _indentWidth;
        set => SetLayoutSpacing(ref _indentWidth, value);
    }

    /// <summary>
    /// The distance between the default tab stops a tab character advances to,
    /// measured from where the paragraph's text starts rather than from the
    /// control, so a tab lines up the same way in an indented or listed paragraph
    /// as in a plain one. It is the tab stop the PDF writer lays out with, so a
    /// tabbed paragraph prints where it sits on screen.
    /// </summary>
    public double TabStopWidth
    {
        get => _tabStopWidth;
        set => SetLayoutSpacing(ref _tabStopWidth, value);
    }

    public double CornerRadius { get; set; } = StandardControlPaint.ControlRadius;

    public double VerticalScrollOffset => _scroller.Offset;

    public bool HasVerticalScrollbar => ScrollMetrics.HasScrollbar;

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
        if (VerticalScrollPolicy == RichEditScrollPolicy.Never)
        {
            double contentWidth = double.IsFinite(availableSize.Width) && availableSize.Width > 0
                ? Math.Max(0, availableSize.Width - (PaddingX * 2))
                : PreferredSize.Width;

            _layout.Update(Document, new RichEditLayoutSettings(contentWidth, _zoom, Font, IndentWidth, TabStopWidth, Wrapping));
            double measuredHeight = _layout.ContentHeight + (PaddingY * 2);
            double width = double.IsFinite(availableSize.Width) && availableSize.Width > 0
                ? availableSize.Width
                : (Wrapping == RichEditWrapping.NoWrap ? _layout.ContentExtentWidth + (PaddingX * 2) : PreferredSize.Width);
            return new BSize(Math.Max(1, width), Math.Max(1, measuredHeight));
        }

        double w = ClampDesired(PreferredSize.Width, availableSize.Width);
        double h = ClampDesired(PreferredSize.Height, availableSize.Height);
        return new BSize(w, h);
    }

    protected override void RenderCore(UiRenderContext context)
    {
        // Layout first: it can move the scroll offset, which the viewport reads.
        EnsureLayout();
        BRenderList renderList = context.RenderList;
        RichEditViewport view = View;
        bool focused = Session?.FocusedElement == this;

        StandardControlPaint.FillRounded(renderList, Bounds, IsEnabled ? Background : StandardControlPaint.SurfaceDisabled, CornerRadius);
        StandardControlPaint.StrokeRounded(
            renderList,
            Bounds,
            focused ? FocusRing : BorderColor,
            CornerRadius,
            focused ? FocusRingThickness : BorderThickness);

        renderList.PushClip(view.Inner);
        _painter.Paint(renderList, new RichEditPaintFrame(
            Document,
            view,
            new RichEditPalette(
                Background,
                Foreground,
                PlaceholderForeground,
                BorderColor,
                SelectionBackground,
                SecondarySelectionBackground,
                CaretColor,
                PageSurround),
            IsEnabled,
            focused,
            Selection,
            SecondarySelection,
            PlaceholderText,
            _compositionText,
            focused && _compositionText.Length > 0 ? CaretInlineStyle : InlineStyle.Default,
            IsReadOnly));
        renderList.PopClip();

        _scroller.PaintScrollbar(renderList, ScrollMetrics, ScrollbarTrack, ScrollbarThumb);
        _scroller.PaintHorizontalScrollbar(renderList, HorizontalScrollMetrics, ScrollbarTrack, ScrollbarThumb);
        PublishCaret(focused, view);
        if (IsContextMenuOpen)
            context.Defer(RenderContextMenu);
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (!IsEnabled)
        {
            CloseContextMenu();
            return false;
        }

        if (IsContextMenuOpen && HandleContextMenuInput(input))
            return true;

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
        _layout.Invalidate();
    }

    protected override void OnDetached()
    {
        CloseContextMenu();
        if (Session?.Host is IUiTextInputHost textInput)
            textInput.ClearCaret(this);

        // The handles belong to the detaching session's renderer, so they are
        // released here rather than kept for a session that cannot draw them.
        _images.ReleaseAll();
        base.OnDetached();
    }

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticNode node = base.GetSemanticNodeCore();
        return IsContextMenuOpen ? node with { Children = [CreateContextMenuSemanticNode()] } : node;
    }

    // --- Layout and geometry -----------------------------------------------

    /// <summary>
    /// Where the content sits in the control right now. Made fresh on every use,
    /// because the document, the bounds, the zoom, and the scroll offset can all
    /// have moved since the last one.
    /// </summary>
    private RichEditViewport View =>
        RichEditViewport.Create(Bounds, PaddingX, PaddingY, Document, _zoom, _scroller.Offset, _scroller.OffsetX);

    private RichEditScrollMetrics ScrollMetrics =>
        new(VerticalScrollPolicy, InnerBounds, _layout.ContentHeight, ScrollbarThickness, MinimumScrollbarThumbLength);

    private RichEditHorizontalScrollMetrics HorizontalScrollMetrics =>
        new(HorizontalScrollPolicy, InnerBounds, _layout.ContentExtentWidth, ScrollbarThickness, MinimumScrollbarThumbLength);

    public double HorizontalScrollOffset => _scroller.OffsetX;

    public bool HasHorizontalScrollbar => HorizontalScrollMetrics.HasScrollbar;

    public override void ScrollToStart()
    {
        _scroller.ScrollTo(0, ScrollMetrics);
        _scroller.ScrollToX(0, HorizontalScrollMetrics);
        Invalidate(UiInvalidationKind.Render);
    }

    public override void ScrollToEnd()
    {
        _scroller.ScrollTo(ScrollMetrics.MaxOffset, ScrollMetrics);
        Invalidate(UiInvalidationKind.Render);
    }

    public override bool MakeVisible(BRect targetRect)
    {
        EnsureLayout();
        bool scrolled = false;
        double viewportHeight = InnerBounds.Height;
        if (viewportHeight > 0 && VerticalScrollPolicy != RichEditScrollPolicy.Never)
        {
            double newScrollY = _scroller.Offset;
            double top = targetRect.Top - View.ContentTop + _scroller.Offset;
            double bottom = targetRect.Bottom - View.ContentTop + _scroller.Offset;
            if (top < newScrollY)
                newScrollY = top;
            else if (bottom > newScrollY + viewportHeight)
                newScrollY = bottom - viewportHeight;

            if (_scroller.ScrollTo(newScrollY, ScrollMetrics))
                scrolled = true;
        }

        double viewportWidth = InnerBounds.Width;
        if (viewportWidth > 0 && HorizontalScrollPolicy != RichEditScrollPolicy.Never)
        {
            double newScrollX = _scroller.OffsetX;
            double left = targetRect.Left - View.ContentLeft + _scroller.OffsetX;
            double right = targetRect.Right - View.ContentLeft + _scroller.OffsetX;
            if (left < newScrollX)
                newScrollX = left;
            else if (right > newScrollX + viewportWidth)
                newScrollX = right - viewportWidth;

            if (_scroller.ScrollToX(newScrollX, HorizontalScrollMetrics))
                scrolled = true;
        }

        if (scrolled)
            Invalidate(UiInvalidationKind.Render);

        return scrolled;
    }

    private BRect InnerBounds => RichEditViewport.InnerOf(Bounds, PaddingX, PaddingY);

    /// <summary>
    /// The height of a line of the control's own font as it is drawn now, which is
    /// what the wheel and paging move by.
    /// </summary>
    private double DefaultLineHeight =>
        BTextMeasurer.GetLineHeight(RichEditLayoutSettings.ZoomedFontFor(Font, _zoom));

    /// <summary>
    /// Brings the layout up to date with the document and the view. When it had to
    /// be rebuilt, the pictures the document no longer holds are released and the
    /// scroll offset is pulled back inside the new content height.
    /// </summary>
    private void EnsureLayout()
    {
        RichTextDocument document = Document;
        var settings = new RichEditLayoutSettings(View.ContentWidth, _zoom, Font, IndentWidth, TabStopWidth, Wrapping);
        if (!_layout.Update(document, settings))
            return;

        _images.ReleaseUnused(document);
        _scroller.Clamp(ScrollMetrics);
        _scroller.ClampX(HorizontalScrollMetrics);
    }

    private void SetLayoutSpacing(ref double field, double value)
    {
        ThrowIfDisposed();
        if (field.Equals(value))
            return;
        field = value;
        _layout.Invalidate();
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private RichTextPosition PositionFromPoint(BPoint point)
    {
        EnsureLayout();
        RichEditViewport view = View;
        double localY = point.Y - view.ContentTop + view.ScrollY;
        return _layout.PositionInLineAtX(_layout.LineAt(localY), point.X, view.ContentLeft);
    }

    private BRect CaretRect(RichTextPosition position) => _layout.CaretBounds(position, View);

    private void PublishCaret(bool focused, RichEditViewport view)
    {
        if (!focused || Session?.Host is not IUiTextInputHost textInput)
            return;

        int caret = FlatIndex(Selection.Focus);
        int start = FlatIndex(Selection.Start);
        int end = FlatIndex(Selection.End);
        textInput.PublishCaret(new UiTextCaretInfo(
            this, _layout.CaretBounds(Selection.Focus, view), caret, start, end - start, IsCompositionActive));
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

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
