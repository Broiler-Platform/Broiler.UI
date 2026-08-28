using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Drawing list paragraphs: a bulleted or numbered item is marked once, in a
/// gutter its text is indented past, numbering counts the way the PDF writer
/// numbers the same document, and everything measured from a line — its wrapped
/// continuations, the caret, the selection — moves with the text.
/// </summary>
public sealed class StandardRichEditListRenderTests
{
    private const string Bullet = "•";

    private static ParagraphStyle Bulleted(int indentLevel = 0) =>
        ParagraphStyle.Default with { ListKind = ListKind.Bullet, IndentLevel = indentLevel };

    private static ParagraphStyle Numbered(int indentLevel = 0) =>
        ParagraphStyle.Default with { ListKind = ListKind.Numbered, IndentLevel = indentLevel };

    private static RichEditScene Scene(BSize size, params (string Text, ParagraphStyle Style)[] paragraphs)
    {
        RichEditScene scene = Create(size);
        scene.Edit.Document = RichTextDocument.FromParagraphs(
            paragraphs.Select(p => RichTextParagraph.Create(p.Text, InlineStyle.Default, p.Style)));
        return scene;
    }

    private static BRenderCommand.DrawText[] Texts(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().ToArray();

    private static BRenderCommand.DrawText Drawn(BRenderList list, string text) =>
        Assert.Single(Texts(list).Where(command => command.Text.Text == text));

    [Fact(Timeout = 600000)]
    public void A_Bulleted_Paragraph_Draws_A_Bullet_In_Front_Of_Its_Text()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            ("plain", ParagraphStyle.Default),
            ("item", Bulleted()));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText plain = Drawn(list, "plain");
        BRenderCommand.DrawText bullet = Drawn(list, Bullet);
        BRenderCommand.DrawText item = Drawn(list, "item");

        // The marker starts where an undecorated paragraph starts, the item's own
        // text starts clear of the marker, and both sit on the item's line.
        Assert.Equal(plain.Origin.X, bullet.Origin.X, 3);
        Assert.True(
            item.Origin.X >= bullet.Origin.X + BTextMeasurer.MeasureAdvance(Bullet, bullet.Text.Font),
            $"item text at {item.Origin.X} overlaps its bullet at {bullet.Origin.X}");
        Assert.Equal(bullet.Origin.Y, item.Origin.Y, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Numbers_Count_Up_And_Restart_After_A_Paragraph_That_Is_Not_A_List_Item()
    {
        RichEditScene scene = Scene(
            new BSize(400, 300),
            ("one", Numbered()),
            ("two", Numbered()),
            ("aside", ParagraphStyle.Default),
            ("again", Numbered()));

        BRenderList list = scene.Session.RenderFrame();

        string[] markers = Texts(list)
            .Select(command => command.Text.Text)
            .Where(text => text.EndsWith('.'))
            .ToArray();
        Assert.Equal(["1.", "2.", "1."], markers);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Wrapped_Item_Is_Marked_Once_And_Its_Later_Lines_Hang_Under_The_First()
    {
        RichEditScene scene = Scene(
            new BSize(160, 200),
            ("the quick brown fox jumps over the lazy dog", Bulleted()));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText bullet = Drawn(list, Bullet);
        BRenderCommand.DrawText[] lines = Texts(list).Where(command => command.Text.Text != Bullet).ToArray();

        Assert.True(lines.Length > 1, $"expected the item to wrap, got {lines.Length} line(s)");
        Assert.All(lines, line => Assert.Equal(lines[0].Origin.X, line.Origin.X, 3));
        Assert.True(lines[0].Origin.X > bullet.Origin.X, "the item text must hang to the right of its bullet");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Indent_Level_Moves_A_Paragraph_By_One_Indent_Width_Each()
    {
        RichEditScene scene = Scene(
            new BSize(600, 200),
            ("flush", ParagraphStyle.Default),
            ("indented", ParagraphStyle.Default with { IndentLevel = 2 }));

        BRenderList list = scene.Session.RenderFrame();

        double flush = Drawn(list, "flush").Origin.X;
        Assert.Equal(flush + (2 * scene.Edit.IndentWidth), Drawn(list, "indented").Origin.X, 3);
        Assert.Empty(Texts(list).Where(command => command.Text.Text == Bullet));
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Items_Of_One_List_Line_Up_Where_It_Runs_From_Nine_Into_Ten()
    {
        RichEditScene scene = Scene(
            new BSize(400, 400),
            Enumerable.Range(1, 10).Select(n => ($"item{n}", Numbered())).ToArray());

        BRenderList list = scene.Session.RenderFrame();

        Drawn(list, "10.");
        Assert.Equal(Drawn(list, "item9").Origin.X, Drawn(list, "item10").Origin.X, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Selection_Of_An_Item_Starts_At_Its_Text()
    {
        RichEditScene scene = Scene(new BSize(400, 200), ("item", Bulleted()));
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 4));
        scene.Session.SetFocus(scene.Edit);

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.FillRect selection = Assert.Single(
            list.Commands.OfType<BRenderCommand.FillRect>().Where(c => c.Color == scene.Edit.SelectionBackground));
        Assert.Equal(Drawn(list, "item").Origin.X, selection.Rect.Left, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Caret_In_An_Empty_Item_Sits_Where_That_Item_Would_Be_Typed()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            (string.Empty, Bulleted()),
            ("item", Bulleted()));
        scene.Session.SetFocus(scene.Edit);

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.FillRect caret = Assert.Single(
            list.Commands.OfType<BRenderCommand.FillRect>()
                .Where(c => c.Color == scene.Edit.CaretColor && c.Rect.Width <= 2));
        Assert.Equal(Drawn(list, "item").Origin.X, caret.Rect.Left, 3);
        Assert.Equal(2, Texts(list).Count(command => command.Text.Text == Bullet));
        scene.Session.Dispose();
    }
}
