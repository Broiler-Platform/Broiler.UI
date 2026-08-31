using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Drawing a floating picture: a shape that carries an image rather than paint,
/// which is what a letterhead's logo is read as. A shape could only be painted
/// and lettered before, so a document that had one drew an empty box where its
/// logo should be.
/// </summary>
public sealed class StandardRichEditFloatingPictureTests
{
    private static readonly byte[] Bytes = [1, 2, 3, 4];

    private static InlineImage Logo() => new(Bytes, "image/png", 60, 40, "a logo");

    private static RichEditScene Scene(BSize size, params DocumentShape[] shapes)
    {
        RichEditScene scene = Create(size);
        scene.Edit.Document = RichTextDocument
            .FromParagraphs([RichTextParagraph.Plain("body text")])
            .WithShapes(shapes);
        return scene;
    }

    private static DocumentShape Picture(double x = -40, double y = 0, double width = 60, double height = 40) =>
        new(0, x, y, width, height, image: Logo());

    private static BRenderCommand.DrawImage[] DrawnImages(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawImage>().ToArray();

    [Fact(Timeout = 600000)]
    public void Draws_A_Floating_Picture_In_Its_Box()
    {
        RichEditScene scene = Scene(new BSize(400, 200), Picture());

        BRenderCommand.DrawImage drawn = Assert.Single(DrawnImages(scene.Session.RenderFrame()));

        // The box is the size, not the image's own: the frame is what the
        // document stated for it.
        Assert.Equal(60, drawn.Destination.Width, 3);
        Assert.Equal(40, drawn.Destination.Height, 3);
        Assert.Equal(1, scene.Host.CreatedImages);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Draws_A_Floating_Picture_At_The_Box_The_Shape_States()
    {
        // A logo two thirds the width of its frame would be stretched to the
        // frame, which is what every word processor does with a resized picture.
        RichEditScene scene = Scene(new BSize(400, 200), Picture(width: 90, height: 30));

        BRenderCommand.DrawImage drawn = Assert.Single(DrawnImages(scene.Session.RenderFrame()));

        Assert.Equal(90, drawn.Destination.Width, 3);
        Assert.Equal(30, drawn.Destination.Height, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Floating_Picture_In_The_Margin_Is_Drawn_On_The_Surface()
    {
        RichEditScene scene = Scene(new BSize(400, 200), Picture(x: -40));

        BRenderCommand.DrawImage drawn = Assert.Single(DrawnImages(scene.Session.RenderFrame()));

        Assert.True(
            drawn.Destination.Left >= scene.Edit.Bounds.Left,
            "the picture was drawn off the left edge");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Floating_Picture_Is_Drawn_Under_The_Text()
    {
        RichEditScene scene = Scene(new BSize(400, 200), Picture());

        BRenderList list = scene.Session.RenderFrame();
        int picture = IndexOf(list, c => c is BRenderCommand.DrawImage);
        int text = IndexOf(list, c => c is BRenderCommand.DrawText);

        Assert.True(picture >= 0 && text >= 0, "expected both a picture and text");
        Assert.True(picture < text, "the picture was drawn over the text");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Floating_Picture_Hangs_From_The_Paragraph_It_Is_Anchored_To()
    {
        RichEditScene scene = Create(new BSize(400, 300));
        scene.Edit.Document = RichTextDocument
            .FromParagraphs([RichTextParagraph.Plain("first"), RichTextParagraph.Plain("second")])
            .WithShapes([new DocumentShape(1, -40, 0, 60, 20, image: Logo())]);

        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.DrawImage drawn = Assert.Single(DrawnImages(list));
        BRenderCommand.DrawText second = Assert.Single(
            list.Commands.OfType<BRenderCommand.DrawText>()
                .Where(t => t.Text.Text.Contains("second", StringComparison.Ordinal)));

        Assert.True(drawn.Destination.Top <= second.Origin.Y + 1, "the picture hung from the wrong paragraph");
        Assert.True(drawn.Destination.Top > 0);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Floating_Picture_Keeps_Its_Handle_Across_Frames()
    {
        // A shape's picture is in the document without being in a paragraph. The
        // pass that releases pictures the document no longer holds looked only at
        // paragraphs, so this one was released the moment layout ran again.
        RichEditScene scene = Scene(new BSize(400, 200), Picture());

        scene.Session.RenderFrame();
        scene.Edit.Document = scene.Edit.Document.WithShapes([.. scene.Edit.Document.Shapes]);
        scene.Session.RenderFrame();

        Assert.Equal(0, scene.Host.ReleasedImages);
        Assert.Single(DrawnImages(scene.Session.RenderFrame()));
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Falls_Back_To_An_Outline_When_The_Picture_Cannot_Be_Decoded()
    {
        RichEditScene scene = Scene(new BSize(400, 200), Picture());
        scene.Host.ImagePixelSize = null;

        BRenderList list = scene.Session.RenderFrame();

        Assert.Empty(DrawnImages(list));
        Assert.Equal(1, scene.Host.CreatedImages);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Picture_Shape_Keeps_Its_Frame()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            new DocumentShape(0, -40, 0, 60, 40, ShapeFill.Solid(BColor.White), BColor.Black, image: Logo()));

        BRenderList list = scene.Session.RenderFrame();

        // Fill under the picture, outline over it, so a bordered logo keeps its
        // border rather than having the picture painted across it.
        int fill = IndexOf(list, c => c is BRenderCommand.FillRect rect && rect.Color == BColor.White);
        int picture = IndexOf(list, c => c is BRenderCommand.DrawImage);
        int outline = IndexOf(list, c => c is BRenderCommand.StrokeRect stroke && stroke.Color == BColor.Black);

        Assert.True(fill >= 0 && picture >= 0 && outline >= 0, "expected a fill, a picture, and an outline");
        Assert.True(fill < picture, "the fill was drawn over the picture");
        Assert.True(picture < outline, "the picture was drawn over the outline");
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
