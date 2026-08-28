using Broiler.Graphics;
using Broiler.Input.Keyboard;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// The Tab key and the tab character it types: running text takes a tab and lays
/// the words after it out at the next tab stop, a list item takes a level instead,
/// and everything measured from a line — the caret, the selection, hit testing,
/// wrapping — agrees with where the text was drawn.
/// </summary>
public sealed class StandardRichEditTabTests
{
    private static RichEditScene Focused(string text = "", BSize? size = null)
    {
        RichEditScene scene = Create(size ?? new BSize(400, 200), text);
        scene.Session.RenderFrame();
        scene.Session.SetFocus(scene.Edit);
        return scene;
    }

    private static KeyboardKeyEvent Tab(bool shift = false) =>
        Key("Tab", BVirtualKey.Tab, KeyboardKeyTransition.Down,
            shift ? KeyboardModifierState.Shift : KeyboardModifierState.None);

    private static BRenderCommand.DrawText Drawn(BRenderList list, string text) =>
        Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>().Where(command => command.Text.Text == text));

    private static ParagraphStyle Bulleted(int indentLevel = 0) =>
        ParagraphStyle.Default with { ListKind = ListKind.Bullet, IndentLevel = indentLevel };

    // --- The key -----------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void Tab_Types_A_Tab_Into_Running_Text()
    {
        RichEditScene scene = Focused("ab");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 1));

        scene.Route.Dispatch(Tab());

        Assert.Equal("a\tb", scene.Edit.Document.PlainText);
        Assert.Equal(new RichTextPosition(0, 2), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void One_Press_Types_One_Tab_Even_Where_The_Platform_Also_Commits_It_As_Text()
    {
        // Windows raises WM_KEYDOWN and WM_CHAR for the same press; only the key
        // may type, or a single Tab would land in the document twice.
        RichEditScene scene = Focused("ab");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 1));

        scene.Route.Dispatch(Tab());
        scene.Route.Dispatch(Text("\t"));

        Assert.Equal("a\tb", scene.Edit.Document.PlainText);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Tab_Replaces_The_Selection_It_Is_Typed_Over()
    {
        RichEditScene scene = Focused("abcd");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 1), new RichTextPosition(0, 3));

        scene.Route.Dispatch(Tab());

        Assert.Equal("a\td", scene.Edit.Document.PlainText);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Shift_Tab_Takes_Back_The_Tab_In_Front_Of_The_Caret()
    {
        RichEditScene scene = Focused("ab");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 1));
        scene.Route.Dispatch(Tab());

        scene.Route.Dispatch(Tab(shift: true));

        Assert.Equal("ab", scene.Edit.Document.PlainText);
        Assert.Equal(new RichTextPosition(0, 1), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Shift_Tab_With_No_Tab_To_Take_Back_Outdents_The_Paragraph()
    {
        RichEditScene scene = Focused();
        scene.Edit.Document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("plain", InlineStyle.Default, ParagraphStyle.Default with { IndentLevel = 2 })]);
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 3));

        scene.Route.Dispatch(Tab(shift: true));

        Assert.Equal("plain", scene.Edit.Document.PlainText);
        Assert.Equal(1, scene.Edit.Document.Paragraphs[0].Style.IndentLevel);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Shift_Tab_On_An_Unindented_Paragraph_Leaves_The_Text_Alone()
    {
        RichEditScene scene = Focused("plain");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 3));

        scene.Route.Dispatch(Tab(shift: true));

        Assert.Equal("plain", scene.Edit.Document.PlainText);
        Assert.Equal(0, scene.Edit.Document.Paragraphs[0].Style.IndentLevel);
        scene.Session.Dispose();
    }

    [Theory(Timeout = 600000)]
    [InlineData(KeyboardModifierState.Control)]
    [InlineData(KeyboardModifierState.Alt)]
    public void A_Modified_Tab_Is_Left_For_The_Application(KeyboardModifierState modifiers)
    {
        // Ctrl+Tab and Alt+Tab move between documents and windows; the editor must
        // not swallow them by typing a tab.
        RichEditScene scene = Focused("ab");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 1));

        scene.Route.Dispatch(Key("Tab", BVirtualKey.Tab, KeyboardKeyTransition.Down, modifiers));

        Assert.Equal("ab", scene.Edit.Document.PlainText);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Read_Only_Edit_Types_Nothing()
    {
        RichEditScene scene = Focused("ab");
        scene.Edit.IsReadOnly = true;
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 1));

        scene.Route.Dispatch(Tab());

        Assert.Equal("ab", scene.Edit.Document.PlainText);
        scene.Session.Dispose();
    }

    // --- Lists and paragraph levels ----------------------------------------

    [Fact(Timeout = 600000)]
    public void Tab_In_Front_Of_A_List_Item_Demotes_It_And_Shift_Tab_Promotes_It()
    {
        RichEditScene scene = Focused();
        scene.Edit.Document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("item", InlineStyle.Default, Bulleted())]);
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 0));

        scene.Route.Dispatch(Tab());
        Assert.Equal("item", scene.Edit.Document.PlainText);
        Assert.Equal(1, scene.Edit.Document.Paragraphs[0].Style.IndentLevel);

        scene.Route.Dispatch(Tab(shift: true));
        Assert.Equal(0, scene.Edit.Document.Paragraphs[0].Style.IndentLevel);
        Assert.Equal(ListKind.Bullet, scene.Edit.Document.Paragraphs[0].Style.ListKind);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Tab_Inside_A_List_Item_Types_A_Tab_Rather_Than_Demoting_It()
    {
        RichEditScene scene = Focused();
        scene.Edit.Document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("item", InlineStyle.Default, Bulleted())]);
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 2));

        scene.Route.Dispatch(Tab());

        Assert.Equal("it\tem", scene.Edit.Document.PlainText);
        Assert.Equal(0, scene.Edit.Document.Paragraphs[0].Style.IndentLevel);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Tab_Over_A_Selection_Of_Several_Paragraphs_Indents_Them_All()
    {
        RichEditScene scene = Focused("one\ntwo");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 1), new RichTextPosition(1, 1));

        scene.Route.Dispatch(Tab());

        Assert.Equal("one\ntwo", scene.Edit.Document.PlainText);
        Assert.Equal(1, scene.Edit.Document.Paragraphs[0].Style.IndentLevel);
        Assert.Equal(1, scene.Edit.Document.Paragraphs[1].Style.IndentLevel);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Shift_Tab_Over_A_Selection_Of_Several_Paragraphs_Outdents_Them_All()
    {
        RichEditScene scene = Focused("one\ntwo");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 1), new RichTextPosition(1, 1));
        scene.Route.Dispatch(Tab());

        scene.Route.Dispatch(Tab(shift: true));

        Assert.Equal(0, scene.Edit.Document.Paragraphs[0].Style.IndentLevel);
        Assert.Equal(0, scene.Edit.Document.Paragraphs[1].Style.IndentLevel);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Tab_Is_One_Undo_Step()
    {
        RichEditScene scene = Focused("ab");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 1));
        scene.Route.Dispatch(Tab());

        scene.Edit.ExecuteCommand(RichEditCommand.Undo);

        Assert.Equal("ab", scene.Edit.Document.PlainText);
        scene.Session.Dispose();
    }

    // --- Layout ------------------------------------------------------------

    [Fact(Timeout = 600000)]
    public void Text_After_A_Tab_Starts_At_The_Next_Tab_Stop()
    {
        RichEditScene scene = Focused("a\tb");
        double stop = scene.Edit.TabStopWidth;

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText a = Drawn(list, "a");
        BRenderCommand.DrawText b = Drawn(list, "b");
        Assert.Equal(a.Origin.X + stop, b.Origin.X, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Tab_Always_Advances_Even_From_A_Column_That_Is_Already_A_Tab_Stop()
    {
        RichEditScene scene = Focused("\t\tb");
        double stop = scene.Edit.TabStopWidth;

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText b = Drawn(list, "b");
        Assert.Equal(stop * 2, b.Origin.X - scene.Edit.PaddingX, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Words_Of_Different_Lengths_Line_Up_On_The_Same_Tab_Stop()
    {
        RichEditScene scene = Focused("a\tone\nbcd\ttwo");

        BRenderList list = scene.Session.RenderFrame();

        Assert.Equal(Drawn(list, "one").Origin.X, Drawn(list, "two").Origin.X, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Tab_Is_Measured_From_Where_The_Paragraph_Text_Starts_Not_From_The_Control()
    {
        // An indented paragraph keeps its own tab grid, so tabbed text stays put
        // relative to the paragraph when the paragraph is indented.
        RichEditScene scene = Focused();
        scene.Edit.Document = RichTextDocument.FromParagraphs(
        [
            RichTextParagraph.Create("a\tone", InlineStyle.Default, ParagraphStyle.Default),
            RichTextParagraph.Create("a\ttwo", InlineStyle.Default, ParagraphStyle.Default with { IndentLevel = 1 }),
        ]);

        BRenderList list = scene.Session.RenderFrame();

        double plain = Drawn(list, "one").Origin.X;
        double indented = Drawn(list, "two").Origin.X;
        Assert.Equal(plain + scene.Edit.IndentWidth, indented, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Caret_Sits_At_The_Tab_Stop_The_Tab_Reached()
    {
        RichEditScene scene = Focused("\tb");
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(0, 1));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText b = Drawn(list, "b");
        BRenderCommand.FillRect caret = Assert.Single(list.Commands
            .OfType<BRenderCommand.FillRect>()
            .Where(command => command.Color == scene.Edit.CaretColor && command.Rect.Width <= 2));
        Assert.Equal(b.Origin.X, caret.Rect.Left, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void Clicking_Past_A_Tab_Lands_On_The_Character_Drawn_There()
    {
        RichEditScene scene = Focused("\tbc");
        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.DrawText drawn = Drawn(list, "bc");

        scene.Route.Dispatch(MouseDown(drawn.Origin.X + 1, drawn.Origin.Y + 4));
        scene.Route.Dispatch(MouseUp(drawn.Origin.X + 1, drawn.Origin.Y + 4));

        Assert.Equal(new RichTextPosition(0, 1), scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void The_Selection_Behind_A_Tab_Is_As_Wide_As_The_Gap_It_Opened()
    {
        RichEditScene scene = Focused("\t");
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 1));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.FillRect highlight = Assert.Single(list.Commands
            .OfType<BRenderCommand.FillRect>()
            .Where(command => command.Color == scene.Edit.SelectionBackground));
        Assert.Equal(scene.Edit.TabStopWidth, highlight.Rect.Width, 3);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Tab_Wide_Enough_To_Overflow_The_Line_Wraps_The_Text_After_It()
    {
        // The tab reaches past the right edge on its own, so nothing can follow it
        // on that line however short the word after it is.
        RichEditScene scene = Focused("aaa\tbbb", new BSize(60, 200));

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText first = Drawn(list, "aaa");
        BRenderCommand.DrawText second = Drawn(list, "bbb");
        Assert.True(
            second.Origin.Y > first.Origin.Y,
            $"expected the text after the tab to wrap; both were drawn at y {first.Origin.Y}");
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void A_Tab_Draws_No_Glyphs_Of_Its_Own()
    {
        RichEditScene scene = Focused("a\tb");

        BRenderList list = scene.Session.RenderFrame();

        Assert.DoesNotContain(
            list.Commands.OfType<BRenderCommand.DrawText>(),
            command => command.Text.Text.Contains('\t') || command.Text.Text.Length == 0);
        scene.Session.Dispose();
    }

    [Fact(Timeout = 600000)]
    public void An_Underline_Runs_Across_The_Gap_A_Tab_Opens()
    {
        RichEditScene scene = Focused();
        scene.Edit.Document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("a\tb", InlineStyle.Default with { Underline = true }, ParagraphStyle.Default)]);

        BRenderList list = scene.Session.RenderFrame();
        BRenderCommand.DrawText a = Drawn(list, "a");

        Assert.Contains(
            list.Commands.OfType<BRenderCommand.FillRect>(),
            command => command.Rect.Left >= a.Origin.X &&
                       command.Rect.Left < a.Origin.X + scene.Edit.TabStopWidth &&
                       command.Rect.Width >= scene.Edit.TabStopWidth / 2 &&
                       command.Rect.Height <= 3);
        scene.Session.Dispose();
    }
}
