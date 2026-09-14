using Broiler.Documents.Model;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class StandardRichEditLayoutSettingsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Changing_Spacing_Reflows_Like_A_Fresh_Editor_And_Requests_A_Frame(bool indent)
    {
        var warm = RichEditStandardHarness.Create(new BSize(160, 220));
        var fresh = RichEditStandardHarness.Create(new BSize(160, 220));
        using var warmSession = warm.Session;
        using var freshSession = fresh.Session;
        var document = RichTextDocument.FromParagraphs([
            RichTextParagraph.Create("a\tb\tc d e f g h i j", InlineStyle.Default,
                ParagraphStyle.Default with { IndentLevel = 1 })]);
        warm.Edit.Document = document;
        fresh.Edit.Document = document;
        SetSpacing(warm.Edit, indent, 8);
        SetSpacing(fresh.Edit, indent, 80);
        var before = Text(warmSession.RenderFrame());
        Assert.Empty(warmSession.Invalidations);

        SetSpacing(warm.Edit, indent, 80);

        Assert.Contains(warmSession.Invalidations, item => item.Element == warm.Edit && item.Kind.HasFlag(UiInvalidationKind.Render));
        var after = Text(warmSession.RenderFrame());
        Assert.NotEqual(before, after);
        Assert.Equal(Text(freshSession.RenderFrame()), after);
        Assert.Same(document, warm.Edit.Document);

        SetSpacing(warm.Edit, indent, 80);
        Assert.Empty(warmSession.Invalidations);
    }

    private static void SetSpacing(StandardRichEdit edit, bool indent, double value)
    {
        if (indent) edit.IndentWidth = value; else edit.TabStopWidth = value;
    }

    private static (string Text, BPoint Origin)[] Text(BRenderList frame) =>
        frame.Commands.OfType<BRenderCommand.DrawText>().Select(command => (command.Text.Text, command.Origin)).ToArray();
}
