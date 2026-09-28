using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Where the content sits in the control: the column, the sheet, and the
/// mapping from the layout's space into the control's.
/// </summary>
public sealed class RichEditViewportTests
{
    private static readonly BRect Box = new(100, 50, 900, 400);

    /// <summary>A4 with the left margin a letterhead stripe stands in.</summary>
    private static readonly PageGeometry A4 = new(595.276, 841.89, 127.55, 56.7, 56.7, 56.7);

    private static RichTextDocument Text() => RichTextDocument.FromPlainText("body");

    [Fact]
    public void Without_A_Page_The_Column_Is_The_Control_Less_Its_Padding()
    {
        RichEditViewport view = RichEditViewport.Create(Box, 8, 6, Text(), zoom: 1, scrollY: 0);

        Assert.Null(view.Page);
        Assert.Equal(108, view.ContentLeft);
        Assert.Equal(56, view.ContentTop);
        Assert.Equal(884, view.ContentWidth);
        Assert.Equal(new BRect(108, 56, 884, 388), view.Inner);
    }

    [Fact]
    public void A_Shape_In_The_Margin_Moves_The_Column_Over_By_How_Far_It_Reaches()
    {
        RichTextDocument document = Text().WithShapes([
            new DocumentShape(0, -40, 0, 30, 100, ShapeFill.Solid(BColor.Black)),
            new DocumentShape(0, -10, 0, 30, 100, ShapeFill.Solid(BColor.Black)),
            new DocumentShape(0, 25, 0, 30, 100, ShapeFill.Solid(BColor.Black)),
        ]);

        RichEditViewport view = RichEditViewport.Create(Box, 8, 6, document, zoom: 1.5, scrollY: 0);

        Assert.Equal(60, view.ShapeGutter, 6);
        Assert.Equal(108 + 60, view.ContentLeft, 6);
        Assert.Equal(884 - 60, view.ContentWidth, 6);
    }

    [Fact]
    public void The_Sheet_Is_Centred_And_Never_Starts_Left_Of_The_Padding()
    {
        RichTextDocument document = Text().WithPageGeometry(A4);

        RichEditViewport wide = RichEditViewport.Create(Box, 8, 6, document, zoom: 1, scrollY: 0);
        RichEditViewport narrow = RichEditViewport.Create(new BRect(100, 50, 300, 400), 8, 6, document, zoom: 1, scrollY: 0);

        Assert.Equal(100 + ((900 - A4.Width) / 2), wide.PageLeft, 6);
        Assert.Equal(wide.PageLeft + A4.MarginLeft, wide.ContentLeft, 6);
        Assert.Equal(56 + A4.MarginTop, wide.ContentTop, 6);
        Assert.Equal(A4.ContentWidth, wide.ContentWidth, 6);
        Assert.Equal(108, narrow.PageLeft, 6);
    }

    [Fact]
    public void The_Paper_Is_Zoomed_With_The_Text_On_It()
    {
        PageGeometry zoomed = Assert.IsType<PageGeometry>(RichEditViewport.PageFor(Text().WithPageGeometry(A4), 2));

        Assert.Equal(A4.Width * 2, zoomed.Width, 6);
        Assert.Equal(A4.MarginLeft * 2, zoomed.MarginLeft, 6);
        Assert.Equal(A4.Width, Assert.IsType<PageGeometry>(RichEditViewport.PageFor(Text().WithPageGeometry(A4), 1)).Width);
    }

    [Fact]
    public void The_Sheet_Grows_With_Text_That_Runs_Past_The_Page()
    {
        RichEditViewport view = RichEditViewport.Create(Box, 8, 6, Text().WithPageGeometry(A4), zoom: 1, scrollY: 30);

        BRect shortText = view.Sheet(A4, contentHeight: 100);
        BRect longText = view.Sheet(A4, contentHeight: 2000);

        Assert.Equal(A4.Height, shortText.Height, 6);
        Assert.True(longText.Height > A4.Height);
        Assert.Equal(50 + 6 - 30, shortText.Top, 6);
    }

    [Fact]
    public void Scrolling_Moves_The_Content_Up_The_Control()
    {
        RichEditViewport view = RichEditViewport.Create(Box, 8, 6, Text(), zoom: 1, scrollY: 40);

        Assert.Equal(56 + 100 - 40, view.ToControlY(100), 6);
    }
}
