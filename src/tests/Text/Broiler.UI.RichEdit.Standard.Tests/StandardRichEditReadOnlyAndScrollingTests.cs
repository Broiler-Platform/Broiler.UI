using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

public sealed class StandardRichEditReadOnlyAndScrollingTests
{
    private static RichEditScene Focused(string text = "", BSize? size = null)
    {
        RichEditScene scene = Create(size ?? new BSize(300, 160), text);
        scene.Session.RenderFrame();
        scene.Session.SetFocus(scene.Edit);
        return scene;
    }

    private static void Caret(RichEditScene scene, int paragraph, int offset) =>
        scene.Edit.Selection = RichTextRange.Caret(new RichTextPosition(paragraph, offset));

    [Fact]
    public void Read_Only_Ignores_Typing_And_Composition()
    {
        RichEditScene scene = Focused("hello");
        scene.Edit.IsReadOnly = true;
        Caret(scene, 0, 5);

        scene.Route.Dispatch(Text(" world"));
        scene.Route.Dispatch(Composition("test", TextCompositionState.Updated));

        Assert.Equal("hello", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Read_Only_Ignores_Backspace_And_Delete()
    {
        RichEditScene scene = Focused("hello");
        scene.Edit.IsReadOnly = true;
        Caret(scene, 0, 2);

        scene.Route.Dispatch(Key("Backspace", BVirtualKey.Back));
        scene.Route.Dispatch(Key("Delete", 0x2E));

        Assert.Equal("hello", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Read_Only_Ignores_Enter()
    {
        RichEditScene scene = Focused("hello");
        scene.Edit.IsReadOnly = true;
        Caret(scene, 0, 2);

        scene.Route.Dispatch(Key("Enter", BVirtualKey.Enter));

        Assert.Equal("hello", scene.Edit.GetPlainText());
        Assert.Equal(1, scene.Edit.Document.ParagraphCount);
        scene.Session.Dispose();
    }

    [Fact]
    public void Read_Only_Tab_Returns_False_For_Focus_Navigation()
    {
        RichEditScene scene = Focused("hello");
        scene.Edit.IsReadOnly = true;
        Caret(scene, 0, 2);

        bool handled = scene.Route.Dispatch(Key("Tab", BVirtualKey.Tab));

        Assert.False(handled);
        Assert.Equal("hello", scene.Edit.GetPlainText());
        scene.Session.Dispose();
    }

    [Fact]
    public void Read_Only_Blocks_Cut_Paste_And_Formatting_Chords()
    {
        RichEditScene scene = Focused("hello world");
        scene.Host.SetText("clipboard");
        scene.Edit.IsReadOnly = true;
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 5));

        // Cut (Ctrl+X)
        bool cutHandled = scene.Route.Dispatch(Key("X", 0x58, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.False(cutHandled);
        Assert.Equal("clipboard", scene.Host.ClipboardText);
        Assert.Equal("hello world", scene.Edit.GetPlainText());

        // Paste (Ctrl+V)
        bool pasteHandled = scene.Route.Dispatch(Key("V", 0x56, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.False(pasteHandled);
        Assert.Equal("hello world", scene.Edit.GetPlainText());

        // Formatting (Ctrl+B)
        bool boldHandled = scene.Route.Dispatch(Key("B", 0x42, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.False(boldHandled);
        scene.Session.Dispose();
    }

    [Fact]
    public void Read_Only_Permits_Copy_And_Select_All()
    {
        RichEditScene scene = Focused("hello world");
        scene.Edit.IsReadOnly = true;
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 5));

        // Copy (Ctrl+C)
        bool copyHandled = scene.Route.Dispatch(Key("C", BVirtualKey.C, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.True(copyHandled);
        Assert.Equal("hello", scene.Host.ClipboardText);

        // Select All (Ctrl+A)
        bool selectAllHandled = scene.Route.Dispatch(Key("A", BVirtualKey.A, KeyboardKeyTransition.Down, KeyboardModifierState.Control));
        Assert.True(selectAllHandled);
        Assert.Equal(RichTextDocument.Start, scene.Edit.Selection.Anchor);
        Assert.Equal(scene.Edit.Document.End, scene.Edit.Selection.Focus);
        scene.Session.Dispose();
    }

    [Fact]
    public void Read_Only_Mouse_Selection_And_Keyboard_Navigation_Work()
    {
        RichEditScene scene = Focused("hello world");
        scene.Edit.IsReadOnly = true;

        // Mouse click and drag
        scene.Route.Dispatch(MouseDown(8, 8));
        scene.Route.Dispatch(MouseMove(150, 8));
        Assert.False(scene.Edit.Selection.IsEmpty);

        // Shift+Right extends selection
        RichTextPosition focusBefore = scene.Edit.Selection.Focus;
        scene.Route.Dispatch(Key("Right", BVirtualKey.Right, KeyboardKeyTransition.Down, KeyboardModifierState.Shift));
        Assert.True(scene.Edit.Selection.Focus.Offset >= focusBefore.Offset);

        scene.Session.Dispose();
    }

    [Fact]
    public void NoWrap_Preserves_Long_Lines_Without_Breaking()
    {
        RichTextDocument document = RichTextDocument.FromPlainText("a quick brown fox jumps over the lazy dog repeatedly without wrapping");
        var layout = new RichEditLayout(new RichEditImageCache(() => null));
        var settings = new RichEditLayoutSettings(
            ContentWidth: 100,
            Zoom: 1,
            Font: BFontStyle.Default,
            IndentWidth: 24,
            TabStopWidth: 48,
            Wrapping: RichEditWrapping.NoWrap);

        layout.Update(document, settings);

        // Under NoWrap, the entire paragraph should remain on a single visual line
        Assert.Single(layout.Lines);
        Assert.True(layout.ContentExtentWidth > 100);
    }

    [Fact]
    public void GetVisibleLineRange_Uses_Binary_Search_Correctly()
    {
        string text = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"line {i}"));
        RichTextDocument document = RichTextDocument.FromPlainText(text);
        var layout = new RichEditLayout(new RichEditImageCache(() => null));
        var settings = new RichEditLayoutSettings(
            ContentWidth: 200,
            Zoom: 1,
            Font: BFontStyle.Default,
            IndentWidth: 24,
            TabStopWidth: 48,
            Wrapping: RichEditWrapping.Wrap);

        layout.Update(document, settings);
        Assert.Equal(50, layout.Lines.Count);

        // Query visible lines between y = 50 and y = 100
        (int start, int count) = layout.GetVisibleLineRange(50, 100);
        Assert.True(start >= 0 && count > 0);
        Assert.True(layout.Lines[start].Top + layout.Lines[start].Height >= 50);
        Assert.True(layout.Lines[start + count - 1].Top <= 100);

        // Query range above all content
        (int startAbove, int countAbove) = layout.GetVisibleLineRange(-100, -10);
        Assert.Equal(0, startAbove);
        Assert.Equal(0, countAbove);

        // Query range below all content
        (int startBelow, int countBelow) = layout.GetVisibleLineRange(10000, 10100);
        Assert.Equal(0, countBelow);
    }

    [Fact]
    public void VerticalScrollPolicy_Never_Sizes_To_Content_Height()
    {
        string text = "line 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7\nline 8\nline 9\nline 10";
        var edit = new StandardRichEdit
        {
            PreferredSize = new BSize(200, 30),
            VerticalScrollPolicy = RichEditScrollPolicy.Never,
        };
        edit.SetPlainText(text);

        edit.Measure(new BSize(200, double.PositiveInfinity));

        // When VerticalScrollPolicy is Never, it grows to fit content height rather than capping at PreferredSize.Height
        Assert.True(edit.DesiredSize.Height > 100);
    }

    [Fact]
    public void ScrollToStart_And_ScrollToEnd_Adjust_Offsets()
    {
        string text = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"long line {i} with lots and lots of text to induce horizontal scrolling"));
        RichEditScene scene = Focused(text, new BSize(150, 100));
        scene.Edit.Wrapping = RichEditWrapping.NoWrap;
        scene.Edit.HorizontalScrollPolicy = RichEditScrollPolicy.Auto;
        scene.Session.RenderFrame();

        scene.Edit.ScrollToEnd();
        Assert.True(scene.Edit.VerticalScrollOffset > 0);

        scene.Edit.ScrollToStart();
        Assert.Equal(0, scene.Edit.VerticalScrollOffset);
        Assert.Equal(0, scene.Edit.HorizontalScrollOffset);
        scene.Session.Dispose();
    }

    [Fact]
    public void A_Wheel_Tilted_Right_Scrolls_Right_Like_Shift_With_The_Wheel_Turned_Down()
    {
        string text = string.Join("\n", Enumerable.Range(0, 5).Select(i => $"long line {i} with lots and lots of text to induce horizontal scrolling"));
        RichEditScene scene = Focused(text, new BSize(150, 100));
        scene.Edit.Wrapping = RichEditWrapping.NoWrap;
        scene.Edit.HorizontalScrollPolicy = RichEditScrollPolicy.Auto;
        scene.Session.RenderFrame();

        // Win32 reports a wheel tilted right (WM_MOUSEHWHEEL) as a positive notch.
        scene.Route.Dispatch(new MouseWheelEvent(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(50, 50),
            MouseButtons.None, MouseWheelAxis.Horizontal, 1, InputEventSource.Synthetic));
        double tilted = scene.Edit.HorizontalScrollOffset;
        Assert.True(tilted > 0);
        Assert.Equal(0, scene.Edit.VerticalScrollOffset);

        // Shift with the wheel turned towards the user (a negative notch) scrolls right as well.
        scene.Route.Dispatch(new MouseWheelEvent(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(50, 50),
            MouseButtons.None, MouseWheelAxis.Vertical, -1, InputEventSource.Synthetic, InputModifiers.Shift));
        double shifted = scene.Edit.HorizontalScrollOffset;
        Assert.True(shifted > tilted);
        Assert.Equal(0, scene.Edit.VerticalScrollOffset);

        scene.Route.Dispatch(new MouseWheelEvent(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(50, 50),
            MouseButtons.None, MouseWheelAxis.Horizontal, -1, InputEventSource.Synthetic));
        Assert.True(scene.Edit.HorizontalScrollOffset < shifted);
        scene.Session.Dispose();
    }

    [Fact]
    public void Shift_With_The_Wheel_Scrolls_The_Same_Way_When_The_Host_Has_Already_Turned_It_Sideways()
    {
        string text = string.Join("\n", Enumerable.Range(0, 5).Select(i => $"long line {i} with lots and lots of text to induce horizontal scrolling"));
        RichEditScene scene = Focused(text, new BSize(150, 100));
        scene.Edit.Wrapping = RichEditWrapping.NoWrap;
        scene.Edit.HorizontalScrollPolicy = RichEditScrollPolicy.Auto;
        scene.Session.RenderFrame();

        // Broiler.Hosting.Windows reports Shift with the wheel as a horizontal notch that keeps Shift
        // and the vertical sign: turned towards the user, it arrives as Horizontal -1 with Shift.
        Assert.True(scene.Route.Dispatch(new MouseWheelEvent(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(50, 50),
            MouseButtons.None, MouseWheelAxis.Horizontal, -1, InputEventSource.Synthetic, InputModifiers.Shift)));
        double right = scene.Edit.HorizontalScrollOffset;
        Assert.True(right > 0);
        Assert.Equal(0, scene.Edit.VerticalScrollOffset);

        Assert.True(scene.Route.Dispatch(new MouseWheelEvent(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(50, 50),
            MouseButtons.None, MouseWheelAxis.Horizontal, 1, InputEventSource.Synthetic, InputModifiers.Shift)));
        Assert.True(scene.Edit.HorizontalScrollOffset < right);
        Assert.Equal(0, scene.Edit.VerticalScrollOffset);
        scene.Session.Dispose();
    }

    [Fact]
    public void A_Tilt_Leaves_An_Editor_That_Does_Not_Scroll_Sideways_Where_It_Is()
    {
        // The default: no horizontal scrolling.
        string text = string.Join("\n", Enumerable.Range(0, 60).Select(i => $"line {i}"));
        RichEditScene scene = Focused(text, new BSize(150, 100));
        Assert.Equal(RichEditScrollPolicy.Never, scene.Edit.HorizontalScrollPolicy);
        scene.Session.RenderFrame();
        Assert.True(scene.Route.Dispatch(new MouseWheelEvent(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(50, 50),
            MouseButtons.None, MouseWheelAxis.Vertical, -3, InputEventSource.Synthetic)));
        double top = scene.Edit.VerticalScrollOffset;
        Assert.True(top > 0);

        // Neither tilt moves the lines, and both are left for a scroller outside the editor.
        foreach (double notches in new[] { -1.0, 1.0 })
        {
            Assert.False(scene.Route.Dispatch(new MouseWheelEvent(Header("mouse"), InputPoint.ClientDeviceIndependentPixels(50, 50),
                MouseButtons.None, MouseWheelAxis.Horizontal, notches, InputEventSource.Synthetic)));
            Assert.Equal(top, scene.Edit.VerticalScrollOffset);
            Assert.Equal(0, scene.Edit.HorizontalScrollOffset);
        }

        scene.Session.Dispose();
    }

    [Fact]
    public void IUiScrollable_MakeVisible_Scrolls_Both_Axes()
    {
        string text = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"row {i:D2} with sufficient text width to enable horizontal visibility testing"));
        RichEditScene scene = Focused(text, new BSize(150, 80));
        scene.Edit.Wrapping = RichEditWrapping.NoWrap;
        scene.Edit.HorizontalScrollPolicy = RichEditScrollPolicy.Auto;
        scene.Session.RenderFrame();

        IUiScrollable scrollable = scene.Edit;
        scrollable.MakeVisible(new BRect(200, 300, 50, 20));

        Assert.True(scene.Edit.VerticalScrollOffset > 0);
        Assert.True(scene.Edit.HorizontalScrollOffset > 0);
        scene.Session.Dispose();
    }
}
