using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;

using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class StandardRichEditContextMenuTests
{
    [Fact]
    public void Right_Click_Preserves_A_Multiline_Selection_And_Copy_Closes_The_Menu()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello\nworld");
        using UiSession session = scene.Session;
        scene.Edit.ExecuteCommand(RichEditCommand.SelectAll);
        RichTextRange selection = scene.Edit.Selection;
        BRenderCommand.DrawText secondLine = session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>().Single(c => c.Text.Text == "world");
        RightClick(scene, new BPoint(secondLine.Origin.X + 2, secondLine.Origin.Y + 2));

        Assert.True(scene.Edit.IsContextMenuOpen);
        Assert.Equal(selection, scene.Edit.Selection);
        Assert.Same(scene.Edit, session.FocusedElement);
        Assert.Same(scene.Edit, session.CapturedElement);
        ClickItem(scene, RichEditCommand.Copy);

        Assert.Equal("hello\nworld", scene.Host.ClipboardText);
        Assert.Equal("hello\nworld", scene.Edit.GetPlainText());
        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void Right_Click_Outside_Selection_Moves_Caret()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello\nworld");
        using UiSession session = scene.Session;
        scene.Edit.Selection = new RichTextRange(new(0, 0), new(0, 5));
        session.RenderFrame();
        RightClick(scene, new BPoint(350, 250));

        Assert.True(scene.Edit.Selection.IsEmpty);
        Assert.Equal(new RichTextPosition(1, 5), scene.Edit.Selection.Focus);
        Assert.False(Item(scene, RichEditCommand.Copy).IsEnabled);
    }

    [Fact]
    public void Cut_And_Paste_Use_Existing_Commands_And_Undo_History()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello\nworld");
        using UiSession session = scene.Session;
        var executed = new List<RichEditCommand>();
        scene.Edit.CommandExecuted += (_, e) => executed.Add(e.Command);
        scene.Edit.Selection = new RichTextRange(new(0, 0), new(1, 0));
        Open(scene);
        ClickItem(scene, RichEditCommand.Cut);
        Assert.Equal("world", scene.Edit.GetPlainText());
        Assert.Equal("hello\n", scene.Host.ClipboardText);

        scene.Edit.Selection = RichTextRange.Caret(new(0, 5));
        Open(scene);
        ClickItem(scene, RichEditCommand.Paste);
        Assert.Equal("worldhello\n", scene.Edit.GetPlainText());
        Assert.Equal([RichEditCommand.Cut, RichEditCommand.Paste], executed);
        Assert.True(scene.Edit.ExecuteCommand(RichEditCommand.Undo));
        Assert.Equal("world", scene.Edit.GetPlainText());
        Assert.True(scene.Edit.ExecuteCommand(RichEditCommand.Undo));
        Assert.Equal("hello\nworld", scene.Edit.GetPlainText());
    }

    [Fact]
    public void Select_All_Selects_Every_Paragraph()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello\nworld");
        using UiSession session = scene.Session;
        Open(scene);
        ClickItem(scene, RichEditCommand.SelectAll);
        Assert.Equal(new RichTextPosition(0, 0), scene.Edit.Selection.Start);
        Assert.Equal(new RichTextPosition(1, 5), scene.Edit.Selection.End);
    }

    [Fact]
    public void Read_Only_Menu_Disables_Cut_And_Paste_And_Skips_Them_With_Keyboard()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello");
        using UiSession session = scene.Session;
        scene.Edit.ExecuteCommand(RichEditCommand.SelectAll);
        scene.Edit.IsReadOnly = true;
        scene.Host.ClipboardText = "paste";
        Open(scene);
        Assert.False(Item(scene, RichEditCommand.Cut).IsEnabled);
        Assert.False(Item(scene, RichEditCommand.Paste).IsEnabled);
        Assert.True(Item(scene, RichEditCommand.Copy).IsEnabled);
        Assert.True(Item(scene, RichEditCommand.SelectAll).IsEnabled);
        ClickItem(scene, RichEditCommand.Cut);
        Assert.True(scene.Edit.IsContextMenuOpen);
        scene.Route.Dispatch(Key("Down", 0x28));
        Assert.Equal(RichEditCommand.Copy, scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex].Command);
        scene.Route.Dispatch(Key("Enter", 0x0D));
        Assert.Equal("hello", scene.Host.ClipboardText);
        Assert.False(scene.Edit.IsContextMenuOpen);
    }

    [Fact]
    public void Empty_Clipboard_And_Empty_Selection_Disable_Clipboard_Commands()
    {
        RichEditScene scene = Create(new BSize(400, 300));
        using UiSession session = scene.Session;
        Open(scene);
        Assert.All(scene.Edit.ContextMenuItems, item => Assert.False(item.IsEnabled));
        scene.Edit.CloseContextMenu();
        scene.Host.ClipboardText = "\n\t";
        Open(scene);
        Assert.True(Item(scene, RichEditCommand.Paste).IsEnabled);
    }

    [Theory]
    [InlineData("F10", 0x79, KeyboardModifierState.Shift)]
    [InlineData("ContextMenu", 0x5D, KeyboardModifierState.None)]
    public void Keyboard_Opens_Menu_And_Escape_Dismisses_Without_Editing(string key, int code, KeyboardModifierState modifiers)
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello");
        using UiSession session = scene.Session;
        session.SetFocus(scene.Edit);
        Assert.True(scene.Route.Dispatch(Key(key, code, modifiers: modifiers)));
        Assert.True(scene.Edit.IsContextMenuOpen);
        scene.Route.Dispatch(Text("x"));
        scene.Route.Dispatch(Key("Backspace", 0x08));
        Assert.Equal("hello", scene.Edit.GetPlainText());
        Assert.True(scene.Route.Dispatch(Key("Escape", 0x1B)));
        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void Outside_Click_Dismisses_Without_Changing_Selection()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello");
        using UiSession session = scene.Session;
        scene.Edit.ExecuteCommand(RichEditCommand.SelectAll);
        RichTextRange selection = scene.Edit.Selection;
        Open(scene);
        scene.Route.Dispatch(MouseDown(390, 290));
        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Equal(selection, scene.Edit.Selection);
        Assert.Null(session.CapturedElement);
    }

    [Fact]
    public void Menu_Is_Clamped_Rendered_Above_Siblings_And_Exposed_In_Semantics()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello");
        using UiSession session = scene.Session;
        session.AddRoot(new StandardRichEdit { PreferredSize = new BSize(400, 300) });
        scene.Edit.OpenContextMenu(new BPoint(399, 299));
        BRect bounds = scene.Edit.ContextMenuBounds;
        Assert.InRange(bounds.Left, 0, 400);
        Assert.InRange(bounds.Top, 0, 300);
        Assert.True(bounds.Right <= 400 && bounds.Bottom <= 300);
        BRenderList rendered = session.RenderFrame();
        rendered.Validate();
        string[] labels = rendered.Commands.OfType<BRenderCommand.DrawText>().Select(c => c.Text.Text).ToArray();
        Assert.Equal(["Cut", "Ctrl+X", "Copy", "Ctrl+C", "Paste", "Ctrl+V", "Select All", "Ctrl+A"], labels.TakeLast(8));
        UiSemanticNode node = scene.Edit.GetSemanticNode();
        Assert.Equal(UiSemanticRole.RichEdit, node.Role);
        Assert.Equal(UiSemanticRole.Menu, Assert.Single(node.Children).Role);
        scene.Edit.CloseContextMenu();
        Assert.Empty(scene.Edit.GetSemanticNode().Children);
    }

    [Fact]
    public void Disabling_Or_Disposing_Dismisses_The_Menu_And_Releases_Capture()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello");
        using UiSession session = scene.Session;
        Open(scene);
        scene.Edit.IsEnabled = false;
        scene.Route.Dispatch(Key("Enter", 0x0D));
        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Null(session.CapturedElement);
        Assert.False(scene.Edit.OpenContextMenu(new BPoint(20, 20)));
        scene.Edit.IsEnabled = true;
        Open(scene);
        scene.Edit.Dispose();
        Assert.False(scene.Edit.IsContextMenuOpen);
        Assert.Null(session.CapturedElement);
    }

    private static void Open(RichEditScene scene) => scene.Edit.OpenContextMenu(new BPoint(20, 40));

    private static StandardRichEditContextMenuItem Item(RichEditScene scene, RichEditCommand command) =>
        scene.Edit.ContextMenuItems.Single(item => item.Command == command);

    private static void RightClick(RichEditScene scene, BPoint point)
    {
        MouseButtonEvent down = MouseDown(point.X, point.Y) with { Button = MouseButton.Right, Buttons = MouseButtons.Right };
        Assert.True(scene.Route.Dispatch(down));
        Assert.True(scene.Route.Dispatch(down with { Transition = MouseButtonTransition.Up, Buttons = MouseButtons.None }));
    }

    private static void ClickItem(RichEditScene scene, RichEditCommand command)
    {
        BRect bounds = scene.Edit.ContextMenuBounds;
        double top = bounds.Top + 4;
        foreach (StandardRichEditContextMenuItem item in scene.Edit.ContextMenuItems)
        {
            double height = item.IsSeparator ? 7 : scene.Edit.ContextMenuItemHeight;
            if (item.Command == command)
            {
                scene.Route.Dispatch(MouseDown(bounds.Left + 10, top + height / 2));
                return;
            }
            top += height;
        }
        Assert.Fail($"Missing menu command: {command}");
    }
}
