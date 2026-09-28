using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// The painter driven directly, with a layout and a frame and no control: what
/// it draws follows from what it is given and nothing else.
/// </summary>
public sealed class RichEditPainterTests
{
    private static readonly BColor Selection = BColor.FromArgb(0xFF, 0x00, 0x00, 0xEE);
    private static readonly BColor Caret = BColor.FromArgb(0xFF, 0xEE, 0x00, 0x00);

    private static readonly RichEditPalette Palette = new(
        Background: BColor.White,
        Foreground: BColor.Black,
        PlaceholderForeground: BColor.FromArgb(0xFF, 0x80, 0x80, 0x80),
        BorderColor: BColor.Black,
        SelectionBackground: Selection,
        SecondarySelectionBackground: BColor.FromArgb(0xFF, 0xEE, 0xEE, 0x00),
        CaretColor: Caret,
        PageSurround: BColor.White);

    private static readonly RichEditViewport View = new(new BRect(0, 0, 300, 200), 8, 6, page: null, shapeGutter: 0, scrollY: 0);

    private static RichEditLayout Layout(RichTextDocument document)
    {
        var layout = new RichEditLayout(new RichEditImageCache(() => null));
        layout.Update(document, new RichEditLayoutSettings(View.ContentWidth, 1, BFontStyle.Default, 24, 48));
        return layout;
    }

    private static BRenderList Paint(
        RichEditLayout layout,
        RichTextDocument document,
        RichTextRange selection,
        bool focused = true,
        bool enabled = true,
        string placeholder = "",
        string composition = "")
    {
        var list = new BRenderList(0);
        new RichEditPainter(layout, new RichEditImageCache(() => null)).Paint(list, new RichEditPaintFrame(
            document, View, Palette, enabled, focused, selection, null, placeholder, composition, InlineStyle.Default));
        return list;
    }

    private static BRenderCommand.FillRect[] Fills(BRenderList list, BColor color) =>
        list.Commands.OfType<BRenderCommand.FillRect>().Where(fill => fill.Color.Equals(color)).ToArray();

    [Fact]
    public void Paints_The_Selection_Exactly_Where_A_Click_Would_Find_It()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("first line\n\nthird");
        RichEditLayout layout = Layout(document);
        var range = new RichTextRange(new RichTextPosition(0, 6), new RichTextPosition(2, 3));

        BRenderCommand.FillRect[] painted = Fills(Paint(layout, document, range), Selection);

        Assert.Equal(3, painted.Length);
        for (int i = 0; i < painted.Length; i++)
        {
            Assert.True(layout.TrySelectionSpan(layout.Lines[i], range, View.ContentLeft, out double left, out double width));
            Assert.Equal(new BRect(left, View.ToControlY(layout.Lines[i].Top), width, layout.Lines[i].Height), painted[i].Rect);
        }
    }

    [Theory]
    [InlineData(true, true, 1)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    public void Draws_A_Caret_Only_For_A_Focused_Enabled_Editor(bool focused, bool enabled, int carets)
    {
        RichTextDocument document = RichTextDocument.FromPlainText("text");

        BRenderList list = Paint(Layout(document), document, RichTextRange.Caret(new RichTextPosition(0, 2)), focused, enabled);

        Assert.Equal(carets, Fills(list, Caret).Length);
    }

    [Fact]
    public void Shows_The_Placeholder_Only_While_There_Is_Nothing_Else_To_Show()
    {
        RichTextDocument empty = RichTextDocument.FromPlainText(string.Empty);
        RichEditLayout layout = Layout(empty);
        RichTextRange caret = RichTextRange.Caret(RichTextDocument.Start);

        Assert.Contains(
            Paint(layout, empty, caret, placeholder: "Type here").Commands.OfType<BRenderCommand.DrawText>(),
            text => text.Text.Text == "Type here");
        Assert.DoesNotContain(
            Paint(layout, empty, caret, placeholder: "Type here", composition: "ab").Commands.OfType<BRenderCommand.DrawText>(),
            text => text.Text.Text == "Type here");
    }
}
