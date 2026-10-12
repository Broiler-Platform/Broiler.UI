using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Text;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// What Ctrl+Z takes back after keyboard editing: a typed word or a run of
/// deletions, not a single character, while paste and clicks stay separate steps.
/// </summary>
public sealed class StandardRichEditUndoTests
{
    private static RichEditScene Focused(string text = "")
    {
        RichEditScene scene = Create(new BSize(300, 160), text);
        scene.Session.RenderFrame();
        scene.Session.SetFocus(scene.Edit);
        return scene;
    }

    private static void Type(RichEditScene scene, string keys)
    {
        foreach (char key in keys)
            scene.Route.Dispatch(Text(key.ToString()));
    }

    private static void Undo(RichEditScene scene) =>
        scene.Route.Dispatch(Key("Z", 0x5A, KeyboardKeyTransition.Down, KeyboardModifierState.Control));

    [Fact]
    public void Ctrl_Z_After_Typing_A_Word_Removes_The_Whole_Word()
    {
        RichEditScene scene = Focused();
        Type(scene, "hello");

        Undo(scene);

        Assert.Equal(string.Empty, scene.Edit.GetPlainText());
        Assert.False(scene.Edit.GetCommandState(RichEditCommand.Undo).IsEnabled);
        scene.Session.Dispose();
    }

    [Fact]
    public void Ctrl_Z_Takes_Typed_Words_Back_One_At_A_Time()
    {
        RichEditScene scene = Focused();
        Type(scene, "hi there");

        Undo(scene);
        Assert.Equal("hi ", scene.Edit.GetPlainText());

        Undo(scene);
        Assert.Equal(string.Empty, scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Committed_Compositions_In_A_Row_Join_One_Step()
    {
        RichEditScene scene = Focused();

        scene.Route.Dispatch(Composition("ka", TextCompositionState.Committed));
        scene.Route.Dispatch(Composition("na", TextCompositionState.Committed));
        Undo(scene);

        Assert.Equal(string.Empty, scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Backspaces_In_A_Row_Come_Back_With_One_Ctrl_Z()
    {
        RichEditScene scene = Focused("abcdef");
        scene.Edit.Selection = RichTextRange.Caret(scene.Edit.Document.End);

        scene.Route.Dispatch(Key("Backspace", BVirtualKey.Back));
        scene.Route.Dispatch(Key("Backspace", BVirtualKey.Back));
        scene.Route.Dispatch(Key("Backspace", BVirtualKey.Back));
        Assert.Equal("abc", scene.Edit.GetPlainText());

        Undo(scene);

        Assert.Equal("abcdef", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Clicking_Back_Into_The_Text_Starts_A_New_Step_Even_At_The_Same_Caret()
    {
        RichEditScene scene = Focused();
        Type(scene, "a");
        RichTextRange before = scene.Edit.Selection;

        // Past the end of the only line, so the caret lands where typing left it.
        scene.Route.Dispatch(MouseDown(290, 8));
        scene.Route.Dispatch(MouseUp(290, 8));
        Assert.Equal(before, scene.Edit.Selection);
        Type(scene, "b");

        Undo(scene);

        Assert.Equal("a", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Paste_Is_A_Step_Of_Its_Own()
    {
        RichEditScene scene = Focused();
        scene.Host.ClipboardText = "XY";
        Type(scene, "ab");

        Assert.True(scene.Edit.ExecuteCommand(RichEditCommand.Paste));
        Type(scene, "c");

        Undo(scene);
        Assert.Equal("abXY", scene.Edit.GetPlainText());
        Undo(scene);
        Assert.Equal("ab", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void A_Host_Can_End_The_Typing_Step()
    {
        RichEditScene scene = Focused();
        Type(scene, "ab");

        scene.Edit.BreakUndoGroup();
        Type(scene, "cd");
        Undo(scene);

        Assert.Equal("ab", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Typing_Still_Reports_The_InsertText_Command()
    {
        RichEditScene scene = Focused();
        var executed = new List<RichEditCommandExecutedEventArgs>();
        scene.Edit.CommandExecuted += (_, e) => executed.Add(e);

        Type(scene, "ab");

        Assert.Equal(2, executed.Count);
        Assert.All(executed, e =>
        {
            Assert.Equal(RichEditCommand.InsertText, e.Command);
            Assert.True(e.Changed);
        });
        scene.Session.Dispose();
    }
}
