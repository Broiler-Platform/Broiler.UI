using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.UI.CodeEditor;
using Broiler.UI.CodeEditor.Standard;
using Broiler.UI.ComboBox;
using Broiler.UI.ComboBox.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Xunit;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The drop-down, menu, and code editor side of ADR 0029: highlighted items and selected code in the theme's
/// selection text color, and the explicit high-contrast flag.
/// </summary>
public sealed class SelectionTextControlTests
{
    private static readonly StandardThemeTokens SystemPalette = SelectionTextRoleTests.SystemHighContrast();

    [Fact]
    public void The_Highlighted_Drop_Down_Item_Is_Drawn_In_The_Selection_Text_Color()
    {
        var combo = new StandardComboBox();
        combo.ApplyTheme(SystemPalette);
        combo.SetItems([new UiComboBoxItem("one", "One"), new UiComboBoxItem("two", "Two")]);

        BRenderList list = RenderOpen(combo);

        Assert.Equal(0, combo.HighlightedIndex);
        Assert.Equal(SystemPalette.SelectionText, TextColor(list, "One"));
        Assert.Equal(SystemPalette.Text, TextColor(list, "Two"));
        Assert.Equal(SystemPalette.SelectionText, combo.SelectedForeground);
    }

    [Fact]
    public void A_Preset_Draws_The_Highlighted_Drop_Down_Item_In_The_Foreground()
    {
        var combo = new StandardComboBox();
        combo.ApplyTheme(StandardThemeTokens.Dark);
        BColor custom = BColor.FromArgb(0xFF, 0xE0, 0xC0, 0x40);
        combo.Foreground = custom;
        combo.SetItems([new UiComboBoxItem("one", "One"), new UiComboBoxItem("two", "Two")]);

        BRenderList list = RenderOpen(combo);

        Assert.Equal(custom, TextColor(list, "One"));
        Assert.Equal(custom, TextColor(list, "Two"));
    }

    [Fact]
    public void The_Open_Menu_Item_And_The_Highlighted_Row_Are_Drawn_In_The_Selection_Text_Color()
    {
        var file = new UiMenuItem("file", "File");
        file.Children.Add(new UiMenuItem("open", "Open"));
        file.Children.Add(new UiMenuItem("save", "Save"));
        file.Children.Add(new UiMenuItem("print", "Print") { IsEnabled = false });
        var menu = new StandardMenu { PresentationMode = UiMenuPresentationMode.MenuBar };
        menu.ApplyTheme(SystemPalette);
        menu.SetItems([file, new UiMenuItem("edit", "Edit")]);
        Assert.True(menu.Open());
        Assert.True(menu.SetSelectedPath([0, 1]));

        BRenderList list = Render(menu, new BSize(400, 300));

        Assert.Equal(SystemPalette.SelectionText, TextColor(list, "File"));
        Assert.Equal(SystemPalette.Text, TextColor(list, "Edit"));
        Assert.Equal(SystemPalette.Text, TextColor(list, "Open"));
        Assert.Equal(SystemPalette.SelectionText, TextColor(list, "Save"));
        Assert.Equal(SystemPalette.TextDisabled, TextColor(list, "Print"));

        // A preset leaves the open item in the foreground.
        menu.ApplyTheme(StandardThemeTokens.Light);
        Assert.Equal(StandardThemeTokens.Light.Text, TextColor(Render(menu, new BSize(400, 300)), "Save"));
    }

    [Fact]
    public void A_Code_Palette_Distinguishes_Without_Color_When_The_Theme_Says_It_Is_High_Contrast()
    {
        Assert.False(StandardCodeEditorPalette.FromTokens(StandardThemeTokens.Light).DistinguishWithoutColor);
        Assert.True(StandardCodeEditorPalette.FromTokens(StandardThemeTokens.Light with { IsHighContrast = true }).DistinguishWithoutColor);
        Assert.True(StandardCodeEditorPalette.FromTokens(StandardThemeTokens.HighContrastLight).DistinguishWithoutColor);
    }

    [Fact]
    public void Selected_Code_Is_Drawn_In_The_Selection_Text_Color_While_The_Editor_Has_Focus()
    {
        var editor = new StandardCodeEditor { Document = new SingleLineDocument("let value = 1;"), HasFocus = true };
        editor.ApplyTheme(SystemPalette);
        editor.Selection = new CodeSelection(4, 9);

        BRenderList list = Render(editor, new BSize(400, 28));

        Assert.Equal(SystemPalette.SelectionText, editor.Palette.SelectionForeground);
        Assert.Equal(SystemPalette.Text, TextColor(list, "let "));
        Assert.Equal(SystemPalette.SelectionText, TextColor(list, "value"));
        Assert.Equal(SystemPalette.Text, TextColor(list, " = 1;"));

        // The selected piece starts where the selection fill does.
        BRect fill = Assert.Single(list.Commands.OfType<BRenderCommand.FillRect>(), command => command.Color == SystemPalette.AccentSoft).Rect;
        Assert.Equal(fill.Left, Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "value").Origin.X, 6);

        // Unfocused, the selection is the inactive surface, which the ordinary colors read on.
        editor.HasFocus = false;
        Assert.Equal(SystemPalette.Text, TextColor(Render(editor, new BSize(400, 28)), "let value = 1;"));
    }

    [Fact]
    public void A_Preset_Draws_Selected_Code_In_Its_Classification_Colors()
    {
        var editor = new StandardCodeEditor { Document = new SingleLineDocument("let value = 1;"), HasFocus = true };
        editor.ApplyTheme(StandardThemeTokens.Dark);
        editor.Selection = new CodeSelection(4, 9);

        BRenderList list = Render(editor, new BSize(400, 28));

        Assert.Null(editor.Palette.SelectionForeground);
        Assert.Equal(StandardThemeTokens.Dark.Text, TextColor(list, "let value = 1;"));
    }

    private static BRenderList RenderOpen(StandardComboBox combo)
    {
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new TestHost(new BSize(300, 300)));
        session.AddRoot(new FixedRoot(combo, new BRect(10, 10, 160, 32)));
        session.RenderFrame();
        session.SetFocus(combo);
        Assert.True(session.DispatchInput(KeyDown("Down")));
        Assert.True(combo.IsDropDownOpen);
        return session.RenderFrame();
    }

    private static BRenderList Render(UiElement element, BSize size)
    {
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new TestHost(size));
        var root = new FixedRoot(element, new BRect(0, 0, size.Width, 28));
        session.AddRoot(root);
        BRenderList list = session.RenderFrame();
        session.RemoveRoot(root);
        root.Release();
        return list;
    }

    private static BColor TextColor(BRenderList list, string text) =>
        Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == text).Text.Color;

    private static UiInputEvent KeyDown(string name) =>
        UiInputEvent.FromKeyboardKey(
            new KeyboardKeyEvent(
                new InputEventHeader(InputDeviceId.FromOpaqueValue("keyboard"), new InputTimestamp(1, System.TimeSpan.TicksPerSecond, "selection-test"), 1),
                KeyboardKey.FromName(name),
                KeyboardKeyTransition.Down,
                KeyboardModifierState.None,
                0,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

    /// <summary>A read-only document of one line, enough to render.</summary>
    private sealed class SingleLineDocument(string text) : ICodeDocument
    {
        public ICodeTextSnapshot Snapshot { get; } = new SingleLineSnapshot(text);
        public bool IsReadOnly => true;
        public string LineEnding => "\n";
        public bool CanUndo => false;
        public bool CanRedo => false;

        public event System.Action<ICodeTextSnapshot>? SnapshotChanged
        {
            add { }
            remove { }
        }

        public CodeEditOutcome Submit(CodeEditIntent intent) => new(false, Snapshot, CodeEditRejection.ReadOnly);
        public bool Undo() => false;
        public bool Redo() => false;
        public void BreakUndoGroup() { }
    }

    private sealed class SingleLineSnapshot(string text) : ICodeTextSnapshot
    {
        public int Version => 1;
        public int Length => text.Length;
        public int LineCount => 1;
        public int GetLineStart(int line) => 0;
        public int GetLineLength(int line) => text.Length;
        public int GetLineFromPosition(int position) => 0;
        public string GetText(int start, int length) => text.Substring(start, length);
        public void CopyTo(int start, int length, System.Span<char> destination) => text.AsSpan(start, length).CopyTo(destination);
        public int GetNextCaretPosition(int position) => System.Math.Min(text.Length, position + 1);
        public int GetPreviousCaretPosition(int position) => System.Math.Max(0, position - 1);
    }

    /// <summary>Arranges its one child into a fixed rectangle.</summary>
    private sealed class FixedRoot : UiElement
    {
        private readonly UiElement _child;
        private readonly BRect _rect;

        public FixedRoot(UiElement child, BRect rect)
        {
            _child = child;
            _rect = rect;
            AddChild(child);
        }

        /// <summary>Lets the child go, so a later frame can place it in another root.</summary>
        public void Release() => RemoveChild(_child);

        protected override BSize MeasureCore(BSize availableSize)
        {
            _child.Measure(new BSize(_rect.Width, _rect.Height));
            return availableSize;
        }

        protected override void ArrangeCore(BRect finalRect) => _child.Arrange(_rect);
    }

    private sealed class TestHost(BSize viewportSize) : IUiHost
    {
        public BSize ViewportSize => viewportSize;
        public double Scale => 1.0;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
