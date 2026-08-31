using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Drawing a document's floating shapes. This surface has no page and fills
/// whatever width it is given, so a shape anchored out in a margin would be
/// drawn off the left edge; the text column moves over far enough to show it,
/// and only when there is something to show.
/// </summary>
public sealed class StandardRichEditShapeTests
{
    private static readonly ShapeFill Green =
        new(BColor.FromArgb(0xFF, 0xAE, 0xCF, 0x00), BColor.White, 90);

    private static RichEditScene Scene(BSize size, params DocumentShape[] shapes)
    {
        RichEditScene scene = Create(size);
        scene.Edit.Document = RichTextDocument
            .FromParagraphs([RichTextParagraph.Plain("body text")])
            .WithShapes(shapes);
        return scene;
    }

    private static BRenderCommand.FillRect[] Fills(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.FillRect>().ToArray();

    private static BRenderCommand.DrawText[] Texts(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().ToArray();

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

    [Fact(Timeout = 600000)]
    public void A_Margin_Shape_Moves_The_Text_Column_Over()
    {
        RichEditScene plain = Scene(new BSize(400, 200));
        double before = Texts(plain.Session.RenderFrame())[0].Origin.X;
        plain.Session.Dispose();

        RichEditScene withStripe = Scene(
            new BSize(400, 200),
            new DocumentShape(0, -40, 0, 30, 100, ShapeFill.Solid(BColor.Black)));
        double after = Texts(withStripe.Session.RenderFrame())[0].Origin.X;

        Assert.Equal(before + 40, after, 3);
        withStripe.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Document_Without_Margin_Content_Keeps_The_Full_Width()
    {
        RichEditScene plain = Scene(new BSize(400, 200));
        double before = Texts(plain.Session.RenderFrame())[0].Origin.X;
        plain.Session.Dispose();

        // A shape inside the column asks for no room of its own.
        RichEditScene inside = Scene(
            new BSize(400, 200),
            new DocumentShape(0, 10, 0, 30, 30, ShapeFill.Solid(BColor.Black)));

        Assert.Equal(before, Texts(inside.Session.RenderFrame())[0].Origin.X, 3);
        inside.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Margin_Shape_Is_Drawn_On_The_Surface()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            new DocumentShape(0, -40, 0, 30, 100, ShapeFill.Solid(BColor.Black)));

        BRenderCommand.FillRect drawn = Assert.Single(
            Fills(scene.Session.RenderFrame()).Where(f => f.Color == BColor.Black));

        Assert.True(drawn.Rect.Left >= scene.Edit.Bounds.Left, "the shape was drawn off the left edge");
        Assert.Equal(30, drawn.Rect.Width, 3);
        Assert.Equal(100, drawn.Rect.Height, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Gradient_Is_Banded_Rather_Than_Flat()
    {
        RichEditScene scene = Scene(
            new BSize(400, 300),
            new DocumentShape(0, -40, 0, 30, 200, Green));

        // One band per point of height: a flat fill would be a single rectangle.
        Assert.True(
            Fills(scene.Session.RenderFrame()).Length > 100,
            "the gradient should be drawn as bands");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Shape_Is_Drawn_Under_The_Text()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            new DocumentShape(0, -40, 0, 30, 100, ShapeFill.Solid(BColor.Black)));

        BRenderList list = scene.Session.RenderFrame();
        int fill = IndexOf(list, c => c is BRenderCommand.FillRect rect && rect.Color == BColor.Black);
        int text = IndexOf(list, c => c is BRenderCommand.DrawText);

        Assert.True(fill >= 0 && text >= 0, "expected both a shape and text");
        Assert.True(fill < text, "the shape was drawn over the text");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Shapes_Own_Text_Is_Drawn_Inside_It()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            new DocumentShape(
                0, -80, 0, 70, 60,
                ShapeFill.Solid(BColor.White),
                BColor.Black,
                [RichTextParagraph.Plain("LOGO")]));

        BRenderCommand.DrawText logo = Assert.Single(
            Texts(scene.Session.RenderFrame()).Where(t => t.Text.Text.Contains("LOGO", StringComparison.Ordinal)));

        double shapeLeft = scene.Edit.Bounds.Left + scene.Edit.PaddingX;
        Assert.True(logo.Origin.X >= shapeLeft - 0.001, "the shape's text was drawn left of its box");
        Assert.True(logo.Origin.X < shapeLeft + 70, "the shape's text was drawn outside its box");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Shape_Anchored_To_A_Later_Paragraph_Hangs_From_It()
    {
        RichEditScene scene = Create(new BSize(400, 300));
        scene.Edit.Document = RichTextDocument
            .FromParagraphs([RichTextParagraph.Plain("first"), RichTextParagraph.Plain("second")])
            .WithShapes([new DocumentShape(1, -40, 0, 30, 20, ShapeFill.Solid(BColor.Black))]);

        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.FillRect drawn = Assert.Single(
            Fills(list).Where(f => f.Color == BColor.Black));
        BRenderCommand.DrawText second = Assert.Single(
            Texts(list).Where(t => t.Text.Text.Contains("second", StringComparison.Ordinal)));

        // Anchored to the second paragraph, so it starts where that paragraph does.
        Assert.True(drawn.Rect.Top <= second.Origin.Y + 1, "the shape hung from the wrong paragraph");
        Assert.True(drawn.Rect.Top > 0);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Document_With_No_Shapes_Draws_None()
    {
        RichEditScene scene = Scene(new BSize(400, 200));

        Assert.DoesNotContain(
            Fills(scene.Session.RenderFrame()),
            f => f.Color == BColor.Black);
        scene.Session.Dispose();
    }
}
