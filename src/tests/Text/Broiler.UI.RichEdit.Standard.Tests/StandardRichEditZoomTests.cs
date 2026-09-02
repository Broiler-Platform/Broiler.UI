using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Reading a document at a size other than the one it states. Zoom scales what
/// the document says — its fonts, indents, tab stops, pictures and page — and
/// leaves what the control says alone, so the text grows inside chrome that
/// stays put and wraps to the column the window really has. Everything measured
/// from a line follows it, and none of it reaches the document.
/// </summary>
public sealed class StandardRichEditZoomTests
{
    private static readonly byte[] ImageBytes = [1, 2, 3, 4];

    private static RichEditScene At(double zoom, string text = "", BSize? size = null)
    {
        RichEditScene scene = Create(size ?? new BSize(400, 200), text);
        scene.Edit.Zoom = zoom;
        return scene;
    }

    private static BRenderCommand.DrawText[] Texts(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().ToArray();

    private static BRenderCommand.DrawText Drawn(BRenderList list, string text) =>
        Assert.Single(Texts(list).Where(command => command.Text.Text == text));

    private static double ContentLeft(StandardRichEdit edit) => edit.Bounds.Left + edit.PaddingX;

    // --- The property ------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void A_Fresh_Surface_Reads_At_The_Size_The_Document_States()
    {
        RichEditScene scene = Create(new BSize(400, 200));

        Assert.Equal(1, scene.Edit.Zoom);
        scene.Session.Dispose();
    }

    [Theory(Timeout = 600000)]
    [InlineData(0, StandardRichEdit.MinimumZoom)]
    [InlineData(-4, StandardRichEdit.MinimumZoom)]
    [InlineData(0.01, StandardRichEdit.MinimumZoom)]
    [InlineData(1000, StandardRichEdit.MaximumZoom)]
    public void An_Impossible_Zoom_Is_Clamped_Into_Range(double requested, double expected)
    {
        RichEditScene scene = Create(new BSize(400, 200));

        scene.Edit.Zoom = requested;

        Assert.Equal(expected, scene.Edit.Zoom);
        scene.Session.Dispose();
    }

    [Theory(Timeout = 600000)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_Zoom_That_Is_Not_A_Number_Falls_Back_To_Full_Size(double requested)
    {
        RichEditScene scene = At(2);

        scene.Edit.Zoom = requested;

        Assert.Equal(1, scene.Edit.Zoom);
        scene.Session.Dispose();
    }

    // --- Text --------------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void A_Documents_Points_Become_The_Controls_Pixels()
    {
        // The unit boundary, pinned. A document states type in points and this
        // control measures in device-independent pixels, so twelve points is
        // sixteen pixels. Passing the number across unconverted rendered a
        // twelve-point document a quarter smaller than it asks for, and matched
        // no other renderer of the same file.
        RichEditScene scene = At(1);
        scene.Edit.Document = RichTextDocument.FromParagraphs(
        [
            RichTextParagraph.Create("twelve", InlineStyle.Default with { FontSize = 12 }),
        ]);

        Assert.Equal(16, Drawn(scene.Session.RenderFrame(), "twelve").Text.Font.Size, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Font_Chosen_Here_Round_Trips_Through_The_Document()
    {
        // The other direction, and the one that writes to a file: a size picked
        // in this control's units is stored as points and comes back as the same
        // pixels. A conversion applied on only one side would drift a document's
        // type every time it was opened and saved.
        RichEditScene scene = At(1);
        scene.Edit.Document = RichTextDocument.FromParagraphs(
        [
            RichTextParagraph.Create("sample", InlineStyle.Default),
        ]);
        scene.Edit.ExecuteCommand(RichEditCommand.SelectAll);
        scene.Edit.ExecuteCommand(
            RichEditCommand.SetFont,
            new BFontStyle("sans-serif", 24));

        Assert.Equal(24, Drawn(scene.Session.RenderFrame(), "sample").Text.Font.Size, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Text_Is_Drawn_At_The_Zoomed_Size()
    {
        RichEditScene plain = Create(new BSize(400, 200), "sample");
        double stated = Drawn(plain.Session.RenderFrame(), "sample").Text.Font.Size;
        plain.Session.Dispose();

        RichEditScene scene = At(1.5, "sample");

        Assert.Equal(stated * 1.5, Drawn(scene.Session.RenderFrame(), "sample").Text.Font.Size, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Runs_Own_Size_Is_Scaled_Too_Rather_Than_Replaced()
    {
        RichEditScene scene = At(2);
        scene.Edit.Document = RichTextDocument.FromParagraphs(
        [
            RichTextParagraph.Create("big", InlineStyle.Default with { FontSize = 30 }),
        ]);

        // 30 points is 40 device-independent pixels, and the zoom doubles that.
        // The run's own size is scaled rather than replaced by the control's,
        // which is what this test is about; the conversion is why 80 and not 60.
        Assert.Equal(80, Drawn(scene.Session.RenderFrame(), "big").Text.Font.Size, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Zoomed_Out_Far_Enough_Text_Is_Still_Drawn_At_A_Whole_Pixel()
    {
        RichEditScene scene = At(StandardRichEdit.MinimumZoom, "sample");
        scene.Edit.Font = new BFontStyle("sans-serif", 4);

        Assert.True(Drawn(scene.Session.RenderFrame(), "sample").Text.Font.Size >= 1);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Column_Does_Not_Grow_With_The_Text_So_Zoomed_In_Text_Wraps_Sooner()
    {
        const string Text = "one two three four five six seven eight nine ten";

        RichEditScene plain = Create(new BSize(400, 400), Text);
        int statedLines = Texts(plain.Session.RenderFrame()).Length;
        plain.Session.Dispose();

        RichEditScene scene = At(2, Text, new BSize(400, 400));

        int zoomedLines = Texts(scene.Session.RenderFrame()).Length;
        Assert.True(
            zoomedLines > statedLines,
            $"zoomed to twice the size the text took {zoomedLines} line(s), no more than the {statedLines} it took at full size");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Padding_Is_The_Controls_Own_And_Stays_Where_It_Is()
    {
        RichEditScene scene = At(3, "sample");

        Assert.Equal(ContentLeft(scene.Edit), Drawn(scene.Session.RenderFrame(), "sample").Origin.X, 3);
        scene.Session.Dispose();
    }

    // --- Indents, tabs, markers -------------------------------------------

    [Fact(Timeout = 600000)]
    public void An_Indent_Is_Scaled_With_The_Text_It_Indents()
    {
        RichEditScene scene = At(2);
        scene.Edit.Document = RichTextDocument.FromParagraphs(
        [
            RichTextParagraph.Create(
                "indented",
                InlineStyle.Default,
                ParagraphStyle.Default with { IndentLevel = 1 }),
        ]);

        Assert.Equal(
            ContentLeft(scene.Edit) + (scene.Edit.IndentWidth * 2),
            Drawn(scene.Session.RenderFrame(), "indented").Origin.X,
            3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Tab_Reaches_A_Stop_That_Moved_With_The_Zoom()
    {
        RichEditScene scene = At(2, "a\tb", new BSize(900, 200));

        Assert.Equal(
            ContentLeft(scene.Edit) + (scene.Edit.TabStopWidth * 2),
            Drawn(scene.Session.RenderFrame(), "b").Origin.X,
            3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_List_Marker_Keeps_Its_Item_At_The_Zoomed_Size()
    {
        RichEditScene scene = At(2, size: new BSize(600, 200));
        scene.Edit.Document = RichTextDocument.FromParagraphs(
        [
            RichTextParagraph.Create(
                "item",
                InlineStyle.Default,
                ParagraphStyle.Default with { ListKind = ListKind.Bullet }),
        ]);

        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.DrawText marker = Drawn(list, "•");
        BRenderCommand.DrawText item = Drawn(list, "item");

        Assert.Equal(ContentLeft(scene.Edit), marker.Origin.X, 3);
        double gap = item.Origin.X - marker.Origin.X;
        Assert.True(
            gap >= scene.Edit.IndentWidth * 2,
            $"the marker gutter was {gap}, narrower than the zoomed indent of {scene.Edit.IndentWidth * 2}");
        scene.Session.Dispose();
    }

    // --- Pictures ----------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void A_Picture_Is_Drawn_At_The_Zoomed_Size()
    {
        RichEditScene scene = At(1.5, size: new BSize(600, 300));
        var image = new InlineImage(ImageBytes, "image/png", 60, 40, "a logo");
        scene.Edit.Document = RichTextDocument.FromParagraphs(
        [
            RichTextParagraph.Create(InlineImage.PlaceholderText, InlineStyle.Default with { Image = image }),
        ]);

        BRenderCommand.DrawImage drawn = Assert.Single(
            scene.Session.RenderFrame().Commands.OfType<BRenderCommand.DrawImage>());

        Assert.Equal(90, drawn.Destination.Width, 3);
        Assert.Equal(60, drawn.Destination.Height, 3);
        scene.Session.Dispose();
    }

    // --- The page ----------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void The_Sheet_And_Its_Margins_Are_Scaled_With_What_Is_Written_On_Them()
    {
        var a4 = new PageGeometry(595.276, 841.89, 127.55, 56.7, 56.7, 56.7);
        RichEditScene scene = At(1.5, size: new BSize(1400, 600));
        scene.Edit.Document = RichTextDocument
            .FromParagraphs([RichTextParagraph.Plain("body text")])
            .WithPageGeometry(a4);

        BRenderList list = scene.Session.RenderFrame();
        double sheetWidth = a4.Width * 1.5;
        BRenderCommand.FillRect sheet = Assert.Single(
            list.Commands.OfType<BRenderCommand.FillRect>()
                .Where(fill => Math.Abs(fill.Rect.Width - sheetWidth) < 0.01));

        Assert.Equal(
            sheet.Rect.Left + (a4.MarginLeft * 1.5),
            Drawn(list, "body text").Origin.X,
            2);
        scene.Session.Dispose();
    }

    // --- What follows the text --------------------------------------------

    [Fact(Timeout = 600000)]
    public void Clicking_Lands_On_The_Character_Under_The_Pointer_At_Any_Zoom()
    {
        RichEditScene plain = Create(new BSize(900, 200), "sample text");
        plain.Session.RenderFrame();
        plain.Session.SetFocus(plain.Edit);
        double statedLeft = ContentLeft(plain.Edit);
        double statedAdvance = Drawn(plain.Session.RenderFrame(), "sample text").Origin.X;
        Assert.Equal(statedLeft, statedAdvance, 3);
        plain.Route.Dispatch(MouseDown(statedLeft + 30, plain.Edit.Bounds.Top + plain.Edit.PaddingY + 4));
        int statedOffset = plain.Edit.Selection.Focus.Offset;
        plain.Session.Dispose();

        RichEditScene scene = At(2, "sample text", new BSize(900, 200));
        scene.Session.RenderFrame();
        scene.Session.SetFocus(scene.Edit);

        // Twice the size, twice as far along the line for the same character.
        scene.Route.Dispatch(MouseDown(ContentLeft(scene.Edit) + 60, scene.Edit.Bounds.Top + scene.Edit.PaddingY + 4));

        Assert.Equal(statedOffset, scene.Edit.Selection.Focus.Offset);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Caret_Is_Published_At_The_Zoomed_Line_Height()
    {
        RichEditScene plain = Create(new BSize(400, 200), "sample");
        plain.Session.SetFocus(plain.Edit);
        plain.Session.RenderFrame();
        BFontStyle font = plain.Edit.Font;

        // The caret is always drawn a little shorter than its line; what matters
        // here is that the line it is measured against is the zoomed one.
        double inset = BTextMeasurer.GetLineHeight(font) - plain.Host.LastCaret!.Bounds.Height;
        plain.Session.Dispose();

        RichEditScene scene = At(2, "sample");
        scene.Session.SetFocus(scene.Edit);
        scene.Session.RenderFrame();

        double zoomedLine = BTextMeasurer.GetLineHeight(font with { Size = font.Size * 2 });
        Assert.Equal(zoomedLine - inset, scene.Host.LastCaret!.Bounds.Height, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Zooming_Keeps_The_Reader_Where_They_Were_Reading()
    {
        RichEditScene scene = Create(
            new BSize(400, 120),
            string.Join('\n', Enumerable.Range(0, 60).Select(i => "line " + i)));
        scene.Session.RenderFrame();
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(40, 0));
        scene.Session.SetFocus(scene.Edit);
        scene.Route.Dispatch(Wheel(10, 10, -6));
        double before = scene.Edit.VerticalScrollOffset;
        Assert.True(before > 0, "the sample must be scrolled for the offset to be worth keeping");

        scene.Edit.Zoom = 2;
        scene.Session.RenderFrame();

        Assert.Equal(before * 2, scene.Edit.VerticalScrollOffset, 3);
        scene.Session.Dispose();
    }

    // --- The document is not touched --------------------------------------

    [Fact(Timeout = 600000)]
    public void Zoom_Is_A_Property_Of_The_View_And_Never_Reaches_The_Document()
    {
        RichEditScene scene = Create(new BSize(400, 200));
        RichTextDocument document = RichTextDocument.FromParagraphs(
        [
            RichTextParagraph.Create("sized", InlineStyle.Default with { FontSize = 30 }),
        ]);
        scene.Edit.Document = document;
        scene.Session.RenderFrame();

        scene.Edit.Zoom = 4;
        scene.Session.RenderFrame();

        Assert.Same(document, scene.Edit.Document);
        Assert.Equal(30, scene.Edit.Document.Paragraphs[0].Runs[0].Style.FontSize);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Returning_To_Full_Size_Draws_Exactly_What_Was_Drawn_Before()
    {
        RichEditScene scene = Create(new BSize(400, 200), "sample");
        double stated = Drawn(scene.Session.RenderFrame(), "sample").Origin.X;

        scene.Edit.Zoom = 2.5;
        scene.Session.RenderFrame();
        scene.Edit.Zoom = 1;

        Assert.Equal(stated, Drawn(scene.Session.RenderFrame(), "sample").Origin.X, 3);
        scene.Session.Dispose();
    }
}
