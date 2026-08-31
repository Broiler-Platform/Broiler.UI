using Broiler.Graphics;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Drawing justified paragraphs: a wrapped line fills its column by widening its
/// own spaces rather than by moving, its closing line is left ragged, and the
/// caret, the selection and a click all land on the widened text rather than
/// where the unstretched text would have been.
/// </summary>
public sealed class StandardRichEditJustificationTests
{
    private static readonly ParagraphStyle Justified =
        ParagraphStyle.Default with { Alignment = TextAlignment.Justify };

    private const string Wrapping = "the quick brown fox jumps over the lazy dog again and again";

    private static RichEditScene Scene(BSize size, string text, ParagraphStyle style)
    {
        RichEditScene scene = Create(size);
        scene.Edit.Document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create(text, InlineStyle.Default, style)]);
        return scene;
    }

    private static BRenderCommand.DrawText[] Texts(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().ToArray();

    private static double ContentLeft(StandardRichEdit edit) => edit.Bounds.Left + edit.PaddingX;

    private static double ContentWidth(StandardRichEdit edit) =>
        Math.Max(0, edit.Bounds.Width - (edit.PaddingX * 2));

    /// <summary>The drawn commands grouped by their y, i.e. one entry per visual line.</summary>
    private static List<List<BRenderCommand.DrawText>> Rows(BRenderList list) =>
        Texts(list)
            .GroupBy(command => Math.Round(command.Origin.Y, 3))
            .OrderBy(group => group.Key)
            .Select(group => group.OrderBy(c => c.Origin.X).ToList())
            .ToList();

    /// <summary>Where a row's last glyph ends.</summary>
    private static double RowRight(List<BRenderCommand.DrawText> row)
    {
        BRenderCommand.DrawText last = row[^1];
        return last.Origin.X + BTextMeasurer.MeasureAdvance(last.Text.Text.TrimEnd(), last.Text.Font);
    }

    [Fact(Timeout = 600000)]
    public void A_Wrapped_Justified_Line_Reaches_The_Right_Margin()
    {
        RichEditScene scene = Scene(new BSize(200, 300), Wrapping, Justified);

        List<List<BRenderCommand.DrawText>> rows = Rows(scene.Session.RenderFrame());

        Assert.True(rows.Count > 1, $"expected the paragraph to wrap, got {rows.Count} row(s)");
        double right = ContentLeft(scene.Edit) + ContentWidth(scene.Edit);
        foreach (List<BRenderCommand.DrawText> row in rows.Take(rows.Count - 1))
        {
            Assert.Equal(ContentLeft(scene.Edit), row[0].Origin.X, 3);
            Assert.Equal(right, RowRight(row), 1);
        }

        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Last_Line_Of_A_Justified_Paragraph_Is_Left_Ragged()
    {
        RichEditScene scene = Scene(new BSize(200, 300), Wrapping, Justified);

        List<List<BRenderCommand.DrawText>> rows = Rows(scene.Session.RenderFrame());
        List<BRenderCommand.DrawText> last = rows[^1];

        Assert.Equal(ContentLeft(scene.Edit), last[0].Origin.X, 3);
        Assert.True(
            RowRight(last) < ContentLeft(scene.Edit) + ContentWidth(scene.Edit) - 1,
            "the closing line was stretched to the margin instead of being left ragged");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Justified_Paragraph_That_Fits_On_One_Line_Is_Not_Stretched()
    {
        RichEditScene scene = Scene(new BSize(400, 200), "short line", Justified);

        BRenderCommand.DrawText[] drawn = Texts(scene.Session.RenderFrame());

        // Its only line is also its last, so there is nothing to fill.
        Assert.Equal(ContentLeft(scene.Edit), drawn[0].Origin.X, 3);
        Assert.Single(drawn);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Line_With_No_Spaces_Is_Not_Prised_Apart()
    {
        // Too long to fit, so it is hard-broken across lines that hold no space
        // between them. There is no gap to widen on such a line, and the slack
        // must not be taken out of the glyphs instead.
        RichEditScene scene = Scene(new BSize(90, 200), "supercalifragilistic word", Justified);

        List<List<BRenderCommand.DrawText>> rows = Rows(scene.Session.RenderFrame());

        foreach (List<BRenderCommand.DrawText> row in rows)
        {
            BRenderCommand.DrawText whole = Assert.Single(row);
            Assert.Equal(ContentLeft(scene.Edit), whole.Origin.X, 3);
        }

        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Caret_Follows_The_Widened_Text()
    {
        RichEditScene scene = Scene(new BSize(200, 300), Wrapping, Justified);
        scene.Session.SetFocus(scene.Edit);

        List<List<BRenderCommand.DrawText>> rows = Rows(scene.Session.RenderFrame());
        List<BRenderCommand.DrawText> first = rows[0];

        // Past the last glyph but before the space the line wrapped on: that is
        // where the drawn text ends, and on a stretched line it is the right
        // margin rather than where the line would have ended unstretched.
        string line = string.Concat(first.Select(c => c.Text.Text));
        int end = line.TrimEnd().Length;
        scene.Edit.Selection = new RichTextRange(
            new RichTextPosition(0, end),
            new RichTextPosition(0, end));

        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.FillRect caret = list.Commands.OfType<BRenderCommand.FillRect>()
            .Single(c => c.Color == scene.Edit.CaretColor && c.Rect.Width <= 2);

        Assert.Equal(RowRight(first), caret.Rect.Left, 1);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Clicking_A_Stretched_Line_Lands_On_The_Character_Under_The_Pointer()
    {
        RichEditScene scene = Scene(new BSize(200, 300), Wrapping, Justified);
        scene.Session.SetFocus(scene.Edit);

        List<List<BRenderCommand.DrawText>> rows = Rows(scene.Session.RenderFrame());
        List<BRenderCommand.DrawText> first = rows[0];
        BRenderCommand.DrawText target = first[^1];
        int expected = first.Sum(d => d.Text.Text.Length) - target.Text.Text.Length;

        // The start of the last chunk on a stretched line sits further right than
        // the unstretched arithmetic would put that offset, so a hit test that
        // ignored the stretch would answer with an earlier character.
        double y = scene.Edit.Bounds.Top + scene.Edit.PaddingY + 2;
        scene.Route.Dispatch(MouseDown(target.Origin.X + 0.5, y));
        scene.Route.Dispatch(MouseUp(target.Origin.X + 0.5, y));

        Assert.Equal(new RichTextPosition(0, expected), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }
}
