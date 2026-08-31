using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Drawing a table on the editing surface. A table's cells are paragraph ranges,
/// so the text was always there - it was laid out one paragraph under the next,
/// down a single column, with nothing to say which cell any of it belonged to.
/// </summary>
public sealed class StandardRichEditTableTests
{
    private static readonly TableBorder Hairline = new(BColor.Black, 1);

    /// <summary>A two-by-two table over four paragraphs, in two 100-point columns.</summary>
    private static RichTextDocument Grid(
        BColor shading = default,
        CellBorders borders = default,
        int columnSpan = 1,
        int rowSpan = 1)
    {
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Plain("a1"),
            RichTextParagraph.Plain("b1"),
            RichTextParagraph.Plain("a2"),
            RichTextParagraph.Plain("b2"),
        ]);

        return document.WithTables([
            new DocumentTable(
                0,
                4,
                [
                    new TableRow([
                        new TableCell(0, 1, 0, columnSpan, rowSpan, shading, borders),
                        new TableCell(1, 1, columnSpan),
                    ]),
                    new TableRow([
                        new TableCell(2, 1, 0, isRowSpanContinuation: rowSpan > 1),
                        new TableCell(3, 1, 1),
                    ]),
                ],
                [100, 100],
                cellPadding: 0),
        ]);
    }

    private static RichEditScene Scene(RichTextDocument document, BSize? size = null)
    {
        RichEditScene scene = Create(size ?? new BSize(400, 300));
        scene.Edit.Document = document;
        return scene;
    }

    private static BRenderCommand.DrawText[] Texts(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().ToArray();

    private static BRenderCommand.DrawText Text(BRenderList list, string text) =>
        Assert.Single(Texts(list).Where(t => t.Text.Text.Contains(text, StringComparison.Ordinal)));

    private static BRenderCommand.FillRect[] Fills(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.FillRect>().ToArray();

    [Fact(Timeout = 600000)]
    public void Puts_The_Cells_Of_A_Row_Beside_Each_Other()
    {
        BRenderList list = Scene(Grid()).Session.RenderFrame();

        BRenderCommand.DrawText left = Text(list, "a1");
        BRenderCommand.DrawText right = Text(list, "b1");

        // Same line, second column: what a table is, and what laying the cells
        // out one under the next could not say.
        Assert.Equal(left.Origin.Y, right.Origin.Y, 3);
        Assert.Equal(left.Origin.X + 100, right.Origin.X, 3);
    }

    [Fact(Timeout = 600000)]
    public void Puts_The_Second_Row_Under_The_First()
    {
        BRenderList list = Scene(Grid()).Session.RenderFrame();

        Assert.True(Text(list, "a2").Origin.Y > Text(list, "a1").Origin.Y);
        Assert.Equal(Text(list, "a1").Origin.X, Text(list, "a2").Origin.X, 3);
    }

    [Fact(Timeout = 600000)]
    public void A_Row_Is_As_Tall_As_Its_Tallest_Cell()
    {
        // The left cell wraps to three lines in a 100-point column; the right one
        // is a word. The second row starts under the taller of them.
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Plain("a wrapping cell with rather more words in it than fit on one line"),
            RichTextParagraph.Plain("b1"),
            RichTextParagraph.Plain("a2"),
            RichTextParagraph.Plain("b2"),
        ]).WithTables([
            new DocumentTable(
                0,
                4,
                [
                    new TableRow([new TableCell(0, 1, 0), new TableCell(1, 1, 1)]),
                    new TableRow([new TableCell(2, 1, 0), new TableCell(3, 1, 1)]),
                ],
                [100, 100],
                cellPadding: 0),
        ]);

        BRenderList list = Scene(document).Session.RenderFrame();

        // "line" is in the last of the wrapped cell's lines, so the second row
        // starting below it is the row having taken the taller cell's height.
        double lastLineOfFirstRow = Texts(list)
            .Where(t => t.Text.Text.Contains("line", StringComparison.Ordinal))
            .Max(t => t.Origin.Y);

        Assert.True(Text(list, "a2").Origin.Y > lastLineOfFirstRow, "the second row overlapped the first");
        Assert.True(Text(list, "b1").Origin.Y < lastLineOfFirstRow, "the short cell was not at the row's top");
    }

    [Fact(Timeout = 600000)]
    public void A_Cell_Wraps_Into_Its_Own_Width()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Plain("one two three four five six seven eight nine ten"),
            RichTextParagraph.Plain("b1"),
        ]).WithTables([
            new DocumentTable(
                0,
                2,
                [new TableRow([new TableCell(0, 1, 0), new TableCell(1, 1, 1)])],
                [100, 100],
                cellPadding: 0),
        ]);

        BRenderList list = Scene(document).Session.RenderFrame();

        // Every line of the wrapped cell stays inside its column rather than
        // running the full width of the surface.
        foreach (BRenderCommand.DrawText text in Texts(list).Where(t => !t.Text.Text.Contains("b1", StringComparison.Ordinal)))
            Assert.True(text.Origin.X < 200, "a cell's text was laid out outside its column");
    }

    [Fact(Timeout = 600000)]
    public void A_Column_Span_Takes_The_Width_Of_Both_Columns()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Plain("wide"),
            RichTextParagraph.Plain("b1"),
        ]).WithTables([
            new DocumentTable(
                0,
                2,
                [
                    new TableRow([new TableCell(0, 1, 0, columnSpan: 2, borders: CellBorders.All(Hairline))]),
                    new TableRow([new TableCell(1, 1, 0)]),
                ],
                [100, 100],
                cellPadding: 0),
        ]);

        RichEditScene scene = Scene(document);
        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.FillRect top = Fills(list)
            .Where(f => f.Color == BColor.Black)
            .OrderBy(f => f.Rect.Top)
            .First();
        Assert.Equal(200, top.Rect.Width, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Paints_A_Cells_Shading_Behind_Its_Text()
    {
        var green = BColor.FromArgb(0xFF, 0xAE, 0xCF, 0x00);
        BRenderList list = Scene(Grid(shading: green)).Session.RenderFrame();

        BRenderCommand.FillRect shaded = Assert.Single(Fills(list).Where(f => f.Color == green));
        Assert.Equal(100, shaded.Rect.Width, 3);

        // Behind: the cell is painted before the text that sits on it.
        int fill = IndexOf(list, c => c is BRenderCommand.FillRect rect && rect.Color == green);
        int text = IndexOf(list, c => c is BRenderCommand.DrawText);
        Assert.True(fill < text, "the shading was painted over the text");
    }

    [Fact(Timeout = 600000)]
    public void Draws_Only_The_Edges_A_Cell_States()
    {
        // Top only: a stroked box would draw the other three as well.
        RichTextDocument document = Grid(borders: new CellBorders(
            TableBorder.None,
            Hairline,
            TableBorder.None,
            TableBorder.None));

        BRenderCommand.FillRect edge = Assert.Single(
            Fills(Scene(document).Session.RenderFrame()).Where(f => f.Color == BColor.Black));

        Assert.Equal(100, edge.Rect.Width, 3);
        Assert.Equal(1, edge.Rect.Height, 3);
    }

    [Fact(Timeout = 600000)]
    public void A_Row_Span_Reaches_Down_Over_The_Rows_It_Covers()
    {
        RichTextDocument document = Grid(borders: CellBorders.All(Hairline), rowSpan: 2);
        BRenderList list = Scene(document).Session.RenderFrame();

        // The left edge of the spanning cell is as tall as both rows together.
        BRenderCommand.FillRect left = Fills(list)
            .Where(f => f.Color == BColor.Black && f.Rect.Width <= 1)
            .OrderByDescending(f => f.Rect.Height)
            .First();

        double rowHeight = Text(list, "b2").Origin.Y - Text(list, "b1").Origin.Y;
        Assert.True(left.Rect.Height > rowHeight, "the merged cell was only as tall as its own row");
    }

    [Fact(Timeout = 600000)]
    public void A_Nested_Table_Is_Laid_Out_Inside_Its_Cell()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Plain("x"),
            RichTextParagraph.Plain("y"),
            RichTextParagraph.Plain("outer"),
        ]).WithTables([
            new DocumentTable(
                0,
                3,
                [
                    new TableRow([
                        new TableCell(
                            0,
                            2,
                            0,
                            tables: [
                                new DocumentTable(
                                    0,
                                    2,
                                    [new TableRow([new TableCell(0, 1, 0), new TableCell(1, 1, 1)])],
                                    [50, 50],
                                    cellPadding: 0),
                            ]),
                        new TableCell(2, 1, 1),
                    ]),
                ],
                [100, 100],
                cellPadding: 0),
        ]);

        BRenderList list = Scene(document).Session.RenderFrame();

        // The nested table's two cells sit beside each other inside the first
        // column, and the outer table's second column is still beyond them.
        Assert.Equal(Text(list, "x").Origin.Y, Text(list, "y").Origin.Y, 3);
        Assert.Equal(Text(list, "x").Origin.X + 50, Text(list, "y").Origin.X, 3);
        Assert.Equal(Text(list, "x").Origin.X + 100, Text(list, "outer").Origin.X, 3);
    }

    [Fact(Timeout = 600000)]
    public void Cell_Padding_Moves_The_Text_In_From_The_Edge()
    {
        RichTextDocument plain = Grid();
        RichTextDocument padded = plain.WithTables([
            new DocumentTable(
                0,
                4,
                plain.Tables[0].Rows,
                plain.Tables[0].ColumnWidths,
                cellPadding: 6),
        ]);

        double bare = Text(Scene(plain).Session.RenderFrame(), "a1").Origin.X;
        double inset = Text(Scene(padded).Session.RenderFrame(), "a1").Origin.X;

        Assert.Equal(bare + 6, inset, 3);
    }

    [Fact(Timeout = 600000)]
    public void A_Grid_Wider_Than_The_Surface_Is_Scaled_To_Fit()
    {
        RichTextDocument document = Grid().WithTables([
            new DocumentTable(
                0,
                4,
                Grid().Tables[0].Rows,
                [4000, 4000],
                cellPadding: 0),
        ]);

        RichEditScene scene = Scene(document, new BSize(300, 300));
        BRenderList list = scene.Session.RenderFrame();

        // Scaled to the width there is, rather than drawn off the right edge.
        Assert.True(Text(list, "b1").Origin.X < scene.Edit.Bounds.Right, "the grid ran off the surface");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Document_Without_Tables_Draws_No_Cells()
    {
        RichEditScene scene = Scene(RichTextDocument.FromPlainText("body"));

        Assert.DoesNotContain(
            Fills(scene.Session.RenderFrame()),
            f => f.Color == BColor.Black);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Typing_In_A_Cell_Keeps_The_Grid_Around_It()
    {
        RichEditScene scene = Scene(Grid());
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 2), new RichTextPosition(0, 2));

        // A split inside a cell adds a paragraph. The cell has to grow with it,
        // or every cell after this one would name the wrong paragraphs.
        scene.Edit.Document = scene.Edit.Document.SplitParagraph(new RichTextPosition(0, 2)).Document;

        DocumentTable table = Assert.Single(scene.Edit.Document.Tables);
        Assert.Equal(2, table.Rows[0].Cells[0].ParagraphCount);
        Assert.Equal(2, table.Rows[0].Cells[1].ParagraphIndex);
        Assert.Equal(5, table.ParagraphCount);
        scene.Session.Dispose();
    }

    /// <summary>Where a command sits in the frame, which is what says what covers what.</summary>
    private static int IndexOf(BRenderList list, Func<BRenderCommand, bool> match)
    {
        for (int i = 0; i < list.Commands.Count; i++)
        {
            if (match(list.Commands[i]))
                return i;
        }

        return -1;
    }
}
