using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Drawing aligned paragraphs: a centered or right-aligned paragraph is laid out
/// where its alignment puts it rather than at the left margin, each of its
/// wrapped lines is aligned on its own, and everything measured from a line — the
/// caret, the selection, the list marker, a click — moves with the text.
/// </summary>
public sealed class StandardRichEditAlignmentRenderTests
{
    private const string Bullet = "•";

    private static ParagraphStyle Aligned(TextAlignment alignment) =>
        ParagraphStyle.Default with { Alignment = alignment };

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

    private static double ContentLeft(StandardRichEdit edit) => edit.Bounds.Left + edit.PaddingX;

    private static double ContentWidth(StandardRichEdit edit) =>
        Math.Max(0, edit.Bounds.Width - (edit.PaddingX * 2));

    private static double Advance(BRenderCommand.DrawText drawn) =>
        BTextMeasurer.MeasureAdvance(drawn.Text.Text, drawn.Text.Font);

    [Fact(Timeout = 600000)]
    public void A_Centered_Paragraph_Sits_In_The_Middle_Of_Its_Column()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            ("flush", ParagraphStyle.Default),
            ("middle", Aligned(TextAlignment.Center)));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText centered = Drawn(list, "middle");
        double slack = ContentWidth(scene.Edit) - Advance(centered);
        Assert.True(slack > 0, "the sample must be narrower than the column for centering to show");
        Assert.Equal(ContentLeft(scene.Edit), Drawn(list, "flush").Origin.X, 3);
        Assert.Equal(ContentLeft(scene.Edit) + (slack / 2), centered.Origin.X, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Right_Aligned_Paragraph_Ends_At_The_Right_Margin()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            ("flush", ParagraphStyle.Default),
            ("ragged", Aligned(TextAlignment.Right)));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText right = Drawn(list, "ragged");
        Assert.Equal(
            ContentLeft(scene.Edit) + ContentWidth(scene.Edit),
            right.Origin.X + Advance(right),
            3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Indented_Paragraph_Is_Aligned_Inside_What_Its_Indent_Leaves()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            ("middle", ParagraphStyle.Default with { Alignment = TextAlignment.Center, IndentLevel = 1 }));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText centered = Drawn(list, "middle");
        double indent = scene.Edit.IndentWidth;
        double slack = ContentWidth(scene.Edit) - indent - Advance(centered);
        Assert.Equal(ContentLeft(scene.Edit) + indent + (slack / 2), centered.Origin.X, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Line_Too_Wide_For_Its_Column_Still_Starts_At_The_Margin()
    {
        // One unbreakable word wider than the column: the alignment slack is
        // negative, and a negative offset would push the text off to the left.
        RichEditScene scene = Scene(
            new BSize(80, 200),
            ("supercalifragilistic", Aligned(TextAlignment.Center)));

        BRenderList list = scene.Session.RenderFrame();

        Assert.All(Texts(list), drawn => Assert.True(
            drawn.Origin.X >= ContentLeft(scene.Edit) - 0.001,
            $"a line starts at {drawn.Origin.X}, left of the margin at {ContentLeft(scene.Edit)}"));
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Each_Wrapped_Line_Of_A_Centered_Paragraph_Is_Centered_On_Its_Own()
    {
        RichEditScene scene = Scene(
            new BSize(160, 200),
            ("the quick brown fox jumps over the lazy dog", Aligned(TextAlignment.Center)));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText[] lines = Texts(list);
        Assert.True(lines.Length > 1, $"expected the paragraph to wrap, got {lines.Length} line(s)");

        double center = ContentLeft(scene.Edit) + (ContentWidth(scene.Edit) / 2);
        foreach (BRenderCommand.DrawText line in lines)
        {
            // Trailing whitespace is not counted, so the space a line wraps on
            // does not pull it off center.
            double width = BTextMeasurer.MeasureAdvance(line.Text.Text.TrimEnd(), line.Text.Font);
            Assert.Equal(center, line.Origin.X + (width / 2), 3);
        }

        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Caret_On_An_Empty_Centered_Line_Sits_Where_That_Line_Would_Be_Typed()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            (string.Empty, Aligned(TextAlignment.Center)));
        scene.Session.SetFocus(scene.Edit);

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.FillRect caret = Assert.Single(
            list.Commands.OfType<BRenderCommand.FillRect>()
                .Where(c => c.Color == scene.Edit.CaretColor && c.Rect.Width <= 2));
        Assert.Equal(ContentLeft(scene.Edit) + (ContentWidth(scene.Edit) / 2), caret.Rect.Left, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Selection_Of_A_Centered_Paragraph_Covers_Where_Its_Text_Is_Drawn()
    {
        RichEditScene scene = Scene(new BSize(400, 200), ("middle", Aligned(TextAlignment.Center)));
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 6));
        scene.Session.SetFocus(scene.Edit);

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.FillRect selection = Assert.Single(
            list.Commands.OfType<BRenderCommand.FillRect>().Where(c => c.Color == scene.Edit.SelectionBackground));
        BRenderCommand.DrawText centered = Drawn(list, "middle");
        Assert.Equal(centered.Origin.X, selection.Rect.Left, 3);
        Assert.Equal(Advance(centered), selection.Rect.Width, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Centered_List_Item_Keeps_Its_Marker_Against_Its_Text()
    {
        RichEditScene scene = Scene(
            new BSize(400, 200),
            ("item", ParagraphStyle.Default with { Alignment = TextAlignment.Center, ListKind = ListKind.Bullet }));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText bullet = Drawn(list, Bullet);
        BRenderCommand.DrawText item = Drawn(list, "item");
        Assert.True(bullet.Origin.X > ContentLeft(scene.Edit), "a centered item's marker moves in from the margin");
        Assert.True(
            item.Origin.X >= bullet.Origin.X + BTextMeasurer.MeasureAdvance(Bullet, bullet.Text.Font),
            $"item text at {item.Origin.X} overlaps its bullet at {bullet.Origin.X}");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Clicking_A_Centered_Line_Lands_On_The_Character_Under_The_Pointer()
    {
        RichEditScene scene = Scene(new BSize(400, 200), ("middle", Aligned(TextAlignment.Center)));
        scene.Session.SetFocus(scene.Edit);
        BRenderCommand.DrawText centered = Drawn(scene.Session.RenderFrame(), "middle");

        // Just past the end of the centered text, which is well left of where a
        // left-aligned hit test would put the same click.
        double y = scene.Edit.Bounds.Top + scene.Edit.PaddingY + 2;
        scene.Route.Dispatch(MouseDown(centered.Origin.X + Advance(centered) + 4, y));
        scene.Route.Dispatch(MouseUp(centered.Origin.X + Advance(centered) + 4, y));

        Assert.Equal(new RichTextPosition(0, 6), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Centering_The_Caret_Paragraph_Redraws_It_Centered_Without_A_Selection()
    {
        // The reported symptom: aligning with nothing selected changed the model
        // but left the line drawn against the left margin.
        RichEditScene scene = Scene(new BSize(400, 200), ("middle", ParagraphStyle.Default));
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 3));
        scene.Session.SetFocus(scene.Edit);
        double flush = Drawn(scene.Session.RenderFrame(), "middle").Origin.X;

        Assert.True(scene.Edit.ExecuteCommand(RichEditCommand.AlignCenter));

        BRenderCommand.DrawText centered = Drawn(scene.Session.RenderFrame(), "middle");
        Assert.Equal(TextAlignment.Center, scene.Edit.Document.Paragraphs[0].Style.Alignment);
        Assert.True(
            centered.Origin.X > flush,
            $"the centered line is still drawn at {centered.Origin.X}, where the left-aligned line was");
        Assert.Equal(
            ContentLeft(scene.Edit) + ((ContentWidth(scene.Edit) - Advance(centered)) / 2),
            centered.Origin.X,
            3);
        scene.Session.Dispose();
    }
}
