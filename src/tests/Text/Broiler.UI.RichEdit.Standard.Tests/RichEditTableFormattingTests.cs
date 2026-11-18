using Broiler.Graphics;
using Broiler.Graphics.Text;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class RichEditTableFormattingTests
{
    private static RichEditLayout Layout(RichTextDocument document, double width = 400, double zoom = 1)
    {
        var layout = new RichEditLayout(new RichEditImageCache(() => null));
        layout.Update(document, new RichEditLayoutSettings(width, zoom, BFontStyle.Default, 24, 48));
        return layout;
    }

    [Theory]
    [InlineData(CellVerticalAlignment.Top, 0)]
    [InlineData(CellVerticalAlignment.Center, .5)]
    [InlineData(CellVerticalAlignment.Bottom, 1)]
    public void Padding_And_Vertical_Alignment_Move_Text_And_Hit_Test_Together(CellVerticalAlignment alignment, double factor)
    {
        var table = new DocumentTable(0, 2, [new TableRow([
            new TableCell(0, 1, 0, padding: new TableCellPadding(5, 7, 9, 11), verticalAlignment: alignment),
            new TableCell(1, 1, 1)], minHeight: 100)], [100, 100]);
        var layout = Layout(RichTextDocument.FromPlainText("Left\nRight").WithTables([table]));
        var line = Assert.Single(layout.Lines, item => item.ParagraphIndex == 0);
        Assert.Equal(5, layout.Frame(0).Left);
        Assert.Equal(86, layout.Frame(0).Width);
        Assert.Equal(7 + factor * (100 - 18 - line.Height), line.Top, 6);
        Assert.Equal(0, layout.LineAt(new BPoint(1, 50)).ParagraphIndex);
        Assert.Equal(1, layout.LineAt(new BPoint(150, 50)).ParagraphIndex);
    }

    [Fact]
    public void Exact_Height_Clips_Overflow_While_Minimum_Height_Grows()
    {
        var document = RichTextDocument.FromPlainText("one two three four five six seven eight nine ten\nAfter");
        DocumentTable Table(bool exact) => new(0, 1,
            [new TableRow([new TableCell(0, 1, 0)], minHeight: 12, exactHeight: exact)], [50]);
        var exact = Layout(document.WithTables([Table(true)]));
        var minimum = Layout(document.WithTables([Table(false)]));
        Assert.Equal(12, Assert.Single(exact.Cells).Bounds.Height);
        Assert.True(Assert.Single(minimum.Cells).Bounds.Height > 12);
        Assert.Equal(12, Assert.Single(exact.Lines, line => line.ParagraphIndex == 1).Top);
        Assert.All(exact.Lines.Where(line => line.ParagraphIndex == 0), line => Assert.Equal(12, line.CellClip!.Value.Bottom));
        Assert.Equal(document.Paragraphs[0].Length, exact.Lines.Where(line => line.ParagraphIndex == 0).Max(line => line.End));
        Assert.Contains(exact.Lines, line => line.ParagraphIndex == 0 && line.Top > 12);
        Assert.Equal(1, exact.LineAt(new BPoint(10, 14)).ParagraphIndex);
    }

    [Fact]
    public void Nested_Percentage_Width_Is_Relative_To_The_Parent_Content_Box()
    {
        var nested = new DocumentTable(1, 1, [new TableRow([new TableCell(1, 1, 0)])], [100], widthPercent: 50, alignment: TableAlignment.End);
        var outer = new DocumentTable(0, 3, [new TableRow([new TableCell(0, 3, 0, tables: [nested],
            padding: new TableCellPadding(10, 5, 10, 5))])], [200], preferredWidth: 200, alignment: TableAlignment.Center);
        var layout = Layout(RichTextDocument.FromPlainText("Before\nNested\nAfter").WithTables([outer]));
        var boxes = layout.Cells.OrderByDescending(cell => cell.Bounds.Width).ToArray();
        Assert.Equal(100, boxes[0].Bounds.Left);
        Assert.Equal(200, boxes[0].Bounds.Width);
        Assert.Equal(200, boxes[1].Bounds.Left);
        Assert.Equal(90, boxes[1].Bounds.Width);
    }

    [Fact]
    public void Autofit_Allocates_More_Space_To_Wider_Contents_And_Scales_With_Zoom()
    {
        var table = new DocumentTable(0, 2, [new TableRow([new TableCell(0, 1, 0), new TableCell(1, 1, 1)])], [50, 50], autoFit: true);
        var document = RichTextDocument.FromPlainText("i\nLonger content").WithTables([table]);
        var layout = Layout(document);
        var zoomed = Layout(document, 800, 2);
        Assert.True(layout.Cells[1].Bounds.Width > layout.Cells[0].Bounds.Width * 2);
        Assert.Equal(layout.Cells[1].Bounds.Width * 2, zoomed.Cells[1].Bounds.Width, 6);
    }

    [Fact]
    public void Covered_Cells_Do_Not_Duplicate_Text_And_Rowspan_Uses_All_Row_Heights()
    {
        var table = new DocumentTable(0, 3, [
            new TableRow([new TableCell(0, 1, 0, rowSpan: 2, verticalAlignment: CellVerticalAlignment.Bottom), new TableCell(1, 1, 1)], minHeight: 40),
            new TableRow([new TableCell(0, 1, 0, isRowSpanContinuation: true), new TableCell(2, 1, 1)], minHeight: 60)], [100, 100]);
        var layout = Layout(RichTextDocument.FromPlainText("Merged\nTop\nBottom").WithTables([table]));
        Assert.Equal(100, layout.Cells[0].Bounds.Height);
        Assert.Single(layout.Lines, line => line.ParagraphIndex == 0);
        Assert.True(layout.Lines.Single(line => line.ParagraphIndex == 0).Top > 60);
    }

    [Fact]
    public void Shared_Border_Conflicts_Are_Resolved_Per_Segment_At_A_Rowspan_Seam()
    {
        var thin = new TableBorder(BColor.Black, 1);
        var thick = new TableBorder(BColor.White, 3, TableBorderStyle.Double);
        var edges = RichEditTableBorders.Resolve([
            new(true, 100, 0, 100, thin), new(true, 100, 0, 40, thick), new(true, 100, 40, 100, thin)]).ToArray();
        Assert.Equal(2, edges.Length);
        Assert.Equal(thick, edges[0].Border);
        Assert.Equal(thin, edges[1].Border);
        Assert.Equal(40, edges[0].End);
        Assert.Equal(40, edges[1].Start);
    }

    [Theory]
    [InlineData(TableBorderStyle.Dashed)]
    [InlineData(TableBorderStyle.Dotted)]
    [InlineData(TableBorderStyle.Double)]
    public void Patterned_Borders_Keep_Gaps_And_Use_Zoomed_Thickness(TableBorderStyle style)
    {
        var list = new BRenderList(0);
        RichEditTableBorders.Draw(list, new(false, 10, 0, 100, new TableBorder(BColor.Black, 2, style)), 2);
        var strips = list.Commands.OfType<BRenderCommand.FillRect>().ToArray();
        Assert.True(strips.Length >= 2);
        if (style == TableBorderStyle.Double)
        {
            Assert.Equal(4d / 3, strips[0].Rect.Height, 6);
            Assert.True(strips[0].Rect.Bottom < strips[1].Rect.Top);
        }
        else
        {
            Assert.Equal(4, strips[0].Rect.Height);
            Assert.True(strips[0].Rect.Right < strips[1].Rect.Left);
        }
    }
}
