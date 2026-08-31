using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Laying the editing surface out on the page a document states. Before this it
/// filled whatever width it was given, so a letter written for a 4.5cm left
/// margin was edited against an 8 point one and looked nothing like its print.
/// </summary>
public sealed class StandardRichEditPageTests
{
    /// <summary>A4 with the left margin a letterhead stripe stands in.</summary>
    private static readonly PageGeometry A4Letterhead =
        new(595.276, 841.89, 127.55, 56.7, 56.7, 56.7);

    private static RichEditScene Scene(BSize size, PageGeometry? page, params DocumentShape[] shapes)
    {
        RichEditScene scene = Create(size);
        RichTextDocument document = RichTextDocument
            .FromParagraphs([RichTextParagraph.Plain("body text")])
            .WithShapes(shapes);
        scene.Edit.Document = page is null ? document : document.WithPageGeometry(page);
        return scene;
    }

    private static BRenderCommand.DrawText[] Texts(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().ToArray();

    private static BRenderCommand.FillRect[] Fills(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.FillRect>().ToArray();

    [Fact(Timeout = 600000)]
    public void The_Text_Starts_At_The_Documents_Left_Margin()
    {
        RichEditScene scene = Scene(new BSize(900, 400), A4Letterhead);

        double sheetLeft = scene.Edit.Bounds.Left + ((900 - A4Letterhead.Width) / 2);
        Assert.Equal(
            sheetLeft + A4Letterhead.MarginLeft,
            Texts(scene.Session.RenderFrame())[0].Origin.X,
            2);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Sheet_Is_Centred_In_The_Control()
    {
        RichEditScene scene = Scene(new BSize(900, 400), A4Letterhead);

        BRenderCommand.FillRect sheet = Assert.Single(
            Fills(scene.Session.RenderFrame()).Where(f => Math.Abs(f.Rect.Width - A4Letterhead.Width) < 0.01));

        double expected = scene.Edit.Bounds.Left + ((900 - A4Letterhead.Width) / 2);
        Assert.Equal(expected, sheet.Rect.Left, 2);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Window_Narrower_Than_The_Paper_Shows_Its_Left_Edge()
    {
        // Centring half the sheet out of view would hide the margin the letterhead
        // stands in, which is the part worth seeing.
        RichEditScene scene = Scene(new BSize(300, 400), A4Letterhead);

        BRenderCommand.FillRect sheet = Assert.Single(
            Fills(scene.Session.RenderFrame()).Where(f => Math.Abs(f.Rect.Width - A4Letterhead.Width) < 0.01));

        Assert.True(sheet.Rect.Left >= scene.Edit.Bounds.Left, "the sheet started left of the control");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Document_Without_A_Page_Is_Laid_Out_As_Before()
    {
        RichEditScene withPage = Scene(new BSize(900, 400), A4Letterhead);
        double paged = Texts(withPage.Session.RenderFrame())[0].Origin.X;
        withPage.Session.Dispose();

        RichEditScene plain = Scene(new BSize(900, 400), page: null);
        double flowed = Texts(plain.Session.RenderFrame())[0].Origin.X;

        Assert.Equal(plain.Edit.Bounds.Left + plain.Edit.PaddingX, flowed, 3);
        Assert.NotEqual(paged, flowed, 3);
        plain.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Document_Without_A_Page_Paints_No_Sheet()
    {
        RichEditScene scene = Scene(new BSize(900, 400), page: null);

        Assert.DoesNotContain(
            Fills(scene.Session.RenderFrame()),
            f => f.Color == scene.Edit.PageSurround);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Shape_In_The_Margin_Lands_On_The_Sheet()
    {
        // The stripe is anchored 111.8pt left of a column that starts 127.55pt in,
        // so on this page it belongs on the paper rather than off its edge.
        RichEditScene scene = Scene(
            new BSize(900, 400),
            A4Letterhead,
            new DocumentShape(0, -111.8, 0, 100.3, 300, ShapeFill.Solid(BColor.Black)));

        BRenderCommand.FillRect stripe = Assert.Single(
            Fills(scene.Session.RenderFrame()).Where(f => f.Color == BColor.Black));
        double sheetLeft = scene.Edit.Bounds.Left + ((900 - A4Letterhead.Width) / 2);

        Assert.True(stripe.Rect.Left >= sheetLeft - 0.01, "the stripe fell off the left of the sheet");
        Assert.True(stripe.Rect.Left < sheetLeft + A4Letterhead.MarginLeft, "the stripe should sit in the margin");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Text_Column_Is_The_Documents_Own_Width()
    {
        RichEditScene wide = Scene(new BSize(900, 400), A4Letterhead);
        RichEditScene wider = Scene(new BSize(1400, 400), A4Letterhead);

        // A wider window shows more desk, not a wider column.
        double a = Texts(wide.Session.RenderFrame())[0].Text.Text.Length;
        double b = Texts(wider.Session.RenderFrame())[0].Text.Text.Length;

        Assert.Equal(a, b);
        wide.Session.Dispose();
        wider.Session.Dispose();
    }
}
