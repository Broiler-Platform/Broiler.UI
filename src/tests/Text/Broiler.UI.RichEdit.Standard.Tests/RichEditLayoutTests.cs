using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// The layout on its own: no control, no session, no frame. Until it was split
/// out of the control, the only way to ask where a line wrapped was to render a
/// whole editor and read its draw calls back.
/// </summary>
public sealed class RichEditLayoutTests
{
    private static readonly BFontStyle Font = BFontStyle.Default;

    private static RichEditLayoutSettings Settings(double width = 200, double zoom = 1, double indent = 24, double tab = 48) =>
        new(width, zoom, Font, indent, tab);

    private static RichEditLayout Layout(RichTextDocument document, RichEditLayoutSettings? settings = null, TestHost? host = null)
    {
        var layout = new RichEditLayout(new RichEditImageCache(() => host));
        layout.Update(document, settings ?? Settings());
        return layout;
    }

    private static string LineText(RichTextDocument document, VisualLine line) =>
        document.Paragraphs[line.ParagraphIndex].Text[line.Start..line.End];

    [Fact]
    public void Line_Segments_Are_Reused_Across_Frames_Until_The_Lines_Are_Rebuilt()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("alpha beta gamma");
        RichEditLayout layout = Layout(document);
        VisualLine line = layout.Lines[0];

        LineSegment[] first = layout.LineSegments(line, contentLeft: 10);
        Assert.Same(first, layout.LineSegments(line, contentLeft: 10));

        // Another origin, for example after scrolling sideways, is measured again.
        LineSegment[] shifted = layout.LineSegments(line, contentLeft: 30);
        Assert.NotSame(first, shifted);
        Assert.Equal(first[0].X + 20, shifted[0].X, 6);

        RichTextDocument edited = RichTextDocument.FromPlainText("omega beta gamma");
        layout.Update(edited, Settings());
        LineSegment[] rebuilt = layout.LineSegments(layout.Lines[0], contentLeft: 10);
        Assert.StartsWith("omega", string.Concat(rebuilt.Select(segment => segment.Text)));

        layout.Invalidate();
        layout.Update(edited, Settings());
        Assert.NotSame(rebuilt, layout.LineSegments(layout.Lines[0], contentLeft: 10));
    }

    [Fact]
    public void Run_Fonts_Are_Resolved_Once_Per_Style_And_Follow_The_Zoom()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("text");
        RichEditLayout layout = Layout(document);
        var bold = InlineStyle.Default with { Bold = true };

        BFontStyle font = layout.RunFont(bold);
        Assert.Same(font, layout.RunFont(bold));
        Assert.Equal(Settings().RunFont(bold), font);

        layout.Update(document, Settings(zoom: 2));
        BFontStyle zoomed = layout.RunFont(bold);
        Assert.Equal(Settings(zoom: 2).RunFont(bold), zoomed);
        Assert.Equal(font.Size * 2, zoomed.Size, 6);
    }

    [Fact]
    public void An_Empty_Document_Still_Has_A_Line_For_The_Caret()
    {
        RichEditLayout layout = Layout(RichTextDocument.FromPlainText(string.Empty));

        VisualLine line = Assert.Single(layout.Lines);
        Assert.Equal(0, line.ParagraphIndex);
        Assert.Equal(Settings().DefaultLineHeight, line.Height);
        Assert.Equal(line.Height, layout.ContentHeight);
    }

    [Fact]
    public void Wraps_A_Paragraph_To_The_Column_And_Stacks_The_Lines()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("alpha beta gamma delta epsilon zeta eta theta iota");
        double width = BTextMeasurer.MeasureAdvance("alpha beta gamma", Font);

        RichEditLayout layout = Layout(document, Settings(width));

        Assert.True(layout.Lines.Count > 1);
        double top = 0;
        foreach (VisualLine line in layout.Lines)
        {
            Assert.True(BTextMeasurer.MeasureAdvance(LineText(document, line).TrimEnd(), Font) <= width);
            Assert.Equal(top, line.Top, 6);
            top += line.Height;
        }

        Assert.Equal(top, layout.ContentHeight, 6);
    }

    [Fact]
    public void Rebuilds_Only_When_The_Document_Or_A_Setting_Changes()
    {
        var layout = new RichEditLayout(new RichEditImageCache(() => null));
        RichTextDocument document = RichTextDocument.FromPlainText("text");

        Assert.True(layout.Update(document, Settings()));
        Assert.False(layout.Update(document, Settings()));

        // Tab and indent widths are part of the snapshot, so changing either
        // after the first frame cannot keep the old line breaks.
        Assert.True(layout.Update(document, Settings(tab: 30)));
        Assert.True(layout.Update(document, Settings(tab: 30, indent: 10)));

        // Identity, not text: an edit produces a new document object.
        Assert.True(layout.Update(RichTextDocument.FromPlainText("text"), Settings(tab: 30, indent: 10)));

        layout.Invalidate();
        Assert.True(layout.Update(document, Settings()));
    }

    [Fact]
    public void A_Caret_At_The_End_Of_A_Wrapped_Line_Stays_On_That_Line()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("alpha beta gamma delta epsilon zeta");
        RichEditLayout layout = Layout(document, Settings(BTextMeasurer.MeasureAdvance("alpha beta", Font)));
        VisualLine first = layout.Lines[0];

        (VisualLine line, int index) = layout.LineForPosition(new RichTextPosition(0, first.End));

        Assert.Equal(0, index);
        Assert.Equal(first, line);
        Assert.Equal(1, layout.LineForPosition(new RichTextPosition(0, first.End + 1)).Index);
    }

    [Fact]
    public void A_Point_Above_Or_Below_The_Text_Lands_On_The_Nearest_Line()
    {
        RichEditLayout layout = Layout(RichTextDocument.FromPlainText("one\ntwo\nthree"));

        Assert.Equal(layout.Lines[0], layout.LineAt(-50));
        Assert.Equal(layout.Lines[^1], layout.LineAt(1_000_000));
        Assert.Equal(layout.Lines[1], layout.LineAt(layout.Lines[1].Top + 1));
    }

    [Fact]
    public void Hit_Testing_A_Caret_Position_Finds_That_Position()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("Hit\ttest me");
        RichEditLayout layout = Layout(document);
        VisualLine line = Assert.Single(layout.Lines);

        for (int offset = 0; offset <= line.End; offset++)
        {
            var position = new RichTextPosition(0, offset);
            double x = layout.CaretX(position, contentLeft: 17);
            Assert.Equal(position, layout.PositionInLineAtX(line, x, contentLeft: 17));
        }
    }

    [Fact]
    public void The_Caret_Sits_On_Its_Line_In_The_Viewports_Space()
    {
        RichEditLayout layout = Layout(RichTextDocument.FromPlainText("one\ntwo"));
        var view = new RichEditViewport(new BRect(100, 50, 300, 200), 8, 6, page: null, shapeGutter: 0, scrollY: 5);
        VisualLine second = layout.Lines[1];

        BRect caret = layout.CaretBounds(new RichTextPosition(1, 0), view);

        Assert.Equal(view.ContentLeft, caret.Left, 6);
        Assert.Equal(view.ContentTop + second.Top - 5 + 1, caret.Top, 6);
        Assert.Equal(1, caret.Width);
        Assert.Equal(second.Height - 2, caret.Height, 6);
    }

    [Fact]
    public void A_Selection_Across_An_Empty_Paragraph_Marks_It_With_A_Sliver()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("one\n\nthree");
        RichEditLayout layout = Layout(document);
        var selection = new RichTextRange(new RichTextPosition(0, 1), new RichTextPosition(2, 2));

        Assert.True(layout.TrySelectionSpan(layout.Lines[1], selection, 0, out double left, out double width));
        Assert.Equal(0, left, 6);
        Assert.Equal(BTextMeasurer.MeasureAdvance(" ", Font), width, 6);

        Assert.True(layout.TrySelectionSpan(layout.Lines[0], selection, 0, out left, out width));
        Assert.Equal(BTextMeasurer.MeasureAdvance("o", Font), left, 3);
        Assert.Equal(BTextMeasurer.MeasureAdvance("ne", Font), width, 3);
    }

    [Fact]
    public void A_Selection_Ending_At_A_Line_Start_Does_Not_Reach_Into_It()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("one\nthree");
        RichEditLayout layout = Layout(document);

        Assert.False(layout.TrySelectionSpan(
            layout.Lines[1],
            new RichTextRange(new RichTextPosition(0, 1), new RichTextPosition(1, 0)),
            0,
            out _,
            out _));

        // A caret is not a selection of anything.
        Assert.False(layout.TrySelectionSpan(layout.Lines[0], RichTextRange.Caret(new RichTextPosition(0, 1)), 0, out _, out _));
    }

    [Fact]
    public void Lays_The_Cells_Of_A_Row_Side_By_Side_And_Spans_Rows()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Plain("a1 has enough text to wrap in its cell"),
            RichTextParagraph.Plain("b1"),
            RichTextParagraph.Plain("a2"),
            RichTextParagraph.Plain("b2"),
        ]).WithTables([
            new DocumentTable(
                0,
                4,
                [
                    new TableRow([new TableCell(0, 1, 0, 1, 2, default, default), new TableCell(1, 1, 1)]),
                    new TableRow([new TableCell(2, 1, 0, isRowSpanContinuation: true), new TableCell(3, 1, 1)]),
                ],
                [100, 100],
                cellPadding: 0),
        ]);

        RichEditLayout layout = Layout(document, Settings(400));

        Assert.Equal(0, layout.Frame(0).Left);
        Assert.Equal(100, layout.Frame(1).Left, 6);
        Assert.True(layout.TryParagraphTop(0, out double a1));
        Assert.True(layout.TryParagraphTop(1, out double b1));
        Assert.Equal(a1, b1, 6);

        // Three boxes: the spanning cell, then the right column's two cells. The
        // spanning cell starts with the first row and ends with the second.
        Assert.Equal(3, layout.Cells.Count);
        CellBox spanning = layout.Cells[0];
        Assert.Equal(layout.Cells[1].Bounds.Top, spanning.Bounds.Top, 6);
        Assert.Equal(layout.Cells[2].Bounds.Bottom, spanning.Bounds.Bottom, 6);
    }

    [Fact]
    public void Numbers_A_List_And_Restarts_It_After_A_Break()
    {
        ParagraphStyle numbered = ParagraphStyle.Default with { ListKind = ListKind.Numbered };
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Create("first", InlineStyle.Default, numbered),
            RichTextParagraph.Create("second", InlineStyle.Default, numbered),
            RichTextParagraph.Plain("break"),
            RichTextParagraph.Create("again", InlineStyle.Default, numbered),
        ]);

        RichEditLayout layout = Layout(document);

        Assert.Equal(["1.", "2.", "", "1."], Enumerable.Range(0, 4).Select(i => layout.Decoration(i).Marker));
    }

    [Fact]
    public void The_Items_Of_One_List_Share_A_Gutter_Wide_Enough_For_Every_Marker()
    {
        ParagraphStyle numbered = ParagraphStyle.Default with { ListKind = ListKind.Numbered };
        RichTextDocument document = RichTextDocument.FromParagraphs(
            Enumerable.Range(0, 12).Select(i => RichTextParagraph.Create("item " + i, InlineStyle.Default, numbered)));

        RichEditLayout layout = Layout(document, Settings(400, indent: 4));

        double[] indents = Enumerable.Range(0, 12).Select(i => layout.Decoration(i).TextIndent).Distinct().ToArray();
        double indent = Assert.Single(indents);
        Assert.True(indent >= BTextMeasurer.MeasureAdvance("12.", Font) + 4);
    }

    [Fact]
    public void An_Indent_Is_Capped_At_Half_The_Box_It_Is_In()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Create("deep", InlineStyle.Default, ParagraphStyle.Default with { IndentLevel = 20 }),
        ]);

        RichEditLayout layout = Layout(document, Settings(200));

        Assert.Equal(100, layout.Decoration(0).TextIndent, 6);
    }

    [Fact]
    public void Zoom_Scales_Indents_And_Line_Heights()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Create("indented", InlineStyle.Default, ParagraphStyle.Default with { IndentLevel = 1 }),
        ]);

        RichEditLayout plain = Layout(document, Settings(400));
        RichEditLayout zoomed = Layout(document, Settings(400, zoom: 2));

        Assert.Equal(plain.Decoration(0).TextIndent * 2, zoomed.Decoration(0).TextIndent, 6);
        Assert.Equal(BTextMeasurer.GetLineHeight(Font with { Size = Font.Size * 2 }), zoomed.Lines[0].Height, 6);
    }

    [Fact]
    public void A_Page_Adds_Its_Top_And_Bottom_Margins_To_What_Scrolls()
    {
        RichTextDocument text = RichTextDocument.FromPlainText("one\ntwo");
        RichTextDocument paged = text.WithPageGeometry(new PageGeometry(300, 400, 20, 20, 30, 40, 10, 10));

        double plain = Layout(text, Settings(260)).ContentHeight;

        Assert.Equal(plain + 70, Layout(paged, Settings(260)).ContentHeight, 6);
    }

    [Fact]
    public void A_Picture_Without_A_Stated_Size_Is_Decoded_Once_To_Measure_It()
    {
        var host = new TestHost(new BSize(100, 100)) { ImagePixelSize = new BSize(20, 10) };
        var picture = new InlineImage(new byte[] { 1, 2, 3 }, "image/png", 0, 0);
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Create(InlineImage.PlaceholderText, InlineStyle.Default with { Image = picture }),
        ]);

        RichEditLayout layout = Layout(document, Settings(), host);
        layout.Update(document, Settings(zoom: 2));

        Assert.Equal(new BSize(40, 20), layout.ImageDisplaySize(picture));
        Assert.Equal(1, host.CreatedImages);
    }

    [Fact]
    public void A_Picture_Nothing_Can_Decode_Keeps_A_Box_The_Reader_Can_See()
    {
        var picture = new InlineImage(new byte[] { 1, 2, 3 }, "image/png", 0, 0);
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Create(InlineImage.PlaceholderText, InlineStyle.Default with { Image = picture }),
        ]);

        RichEditLayout layout = Layout(document, Settings(zoom: 0.5), host: null);

        Assert.Equal(new BSize(36, 36), layout.ImageDisplaySize(picture));
        Assert.True(layout.Lines[0].Height >= 36);
    }

    [Fact]
    public void Segments_Carry_A_Tab_As_Width_Without_Glyphs()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Create("a\tb", InlineStyle.Default with { Underline = true }),
        ]);
        RichEditLayout layout = Layout(document);

        LineSegment[] segments = layout.LineSegments(layout.Lines[0], contentLeft: 10).ToArray();

        Assert.Equal(["a", string.Empty, "b"], segments.Select(segment => segment.Text));
        Assert.Equal(10, segments[0].X, 6);
        Assert.Equal(10 + 48, segments[2].X, 6);
        Assert.Equal(48 - segments[0].Advance, segments[1].Advance, 6);
        Assert.True(segments[1].Style.Underline);
    }

    [Fact]
    public void Querying_A_Layout_That_Was_Never_Built_Says_So()
    {
        var layout = new RichEditLayout(new RichEditImageCache(() => null));

        Assert.Throws<InvalidOperationException>(() => layout.LineSegments(new VisualLine(0, 0, 0, 0, 10, 0), 0).ToArray());
    }

    [Fact]
    public void Shading_Reaches_The_Cell_Boxes()
    {
        BColor shading = BColor.FromArgb(0xFF, 0x10, 0x20, 0x30);
        RichTextDocument document = RichTextDocument.FromParagraphs([RichTextParagraph.Plain("cell")])
            .WithTables([
                new DocumentTable(0, 1, [new TableRow([new TableCell(0, 1, 0, 1, 1, shading, default)])], [80], cellPadding: 0),
            ]);

        CellBox cell = Assert.Single(Layout(document).Cells);

        Assert.Equal(shading, cell.Shading);
        Assert.Equal(80, cell.Bounds.Width, 6);
    }
}
