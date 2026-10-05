using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Windowing;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.Input.Text;

namespace Broiler.UI.FormatCodeView.Standard.Tests;

public sealed class StandardFormatCodeViewInputTests
{
    [Fact]
    public void Committed_Ime_Text_Raises_One_Structured_Edit()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("hello"));
        scene.View.IsEditable = true;
        scene.Session.SetFocus(scene.View);
        scene.View.SetSelection(1, 4);
        var edits = new List<FormatCodeEditRequestedEventArgs>();
        scene.View.EditRequested += (_, args) => edits.Add(args);

        Assert.True(scene.Route.Dispatch(new TextCompositionEvent(
            FormatCodeViewStandardHarness.Header("ime"),
            "ni",
            TextCompositionState.Updated)));
        Assert.Equal("ni", scene.View.CompositionText);
        Assert.Empty(edits);

        Assert.True(scene.Route.Dispatch(new TextCompositionEvent(
            FormatCodeViewStandardHarness.Header("ime"),
            "你",
            TextCompositionState.Committed)));
        ReplaceFormatCodeTextIntent intent = Assert.IsType<ReplaceFormatCodeTextIntent>(
            Assert.Single(edits).Intent);
        Assert.Equal("你", intent.Text);
        Assert.Empty(scene.View.CompositionText);
    }

    [Fact]
    public void Backspace_On_Code_Requests_Semantic_Removal_Not_Bracket_Deletion()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("x", new InlineStyle { Bold = true })]);
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100), FormatCodeProjector.Project(document));
        scene.View.IsEditable = true;
        scene.Session.SetFocus(scene.View);
        FormatCodeEditRequestedEventArgs? edit = null;
        scene.View.EditRequested += (_, args) => edit = args;
        int afterOpenCode = scene.View.Projection!.Tokens[0].ProjectedLength;
        scene.View.SetSelection(afterOpenCode, afterOpenCode);

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Key(
            "Backspace", BVirtualKey.Back)));
        Assert.Equal(FormatCodeProperty.Bold, edit?.Token?.EditDescriptor?.Property);
        Assert.IsType<ApplyFormatCodeInlineIntent>(edit?.Intent);
    }

    [Fact]
    public void Backspace_Windows_Text_Event_Does_Not_Insert_U0008()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("ab"));
        scene.View.IsEditable = true;
        scene.Session.SetFocus(scene.View);
        scene.View.SetSelection(2, 2);
        var edits = new List<FormatCodeEditRequestedEventArgs>();
        scene.View.EditRequested += (_, args) => edits.Add(args);

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Key(
            "Backspace", BVirtualKey.Back)));
        Assert.False(scene.Route.Dispatch(new TextInputEvent(
            FormatCodeViewStandardHarness.Header("wm-char"),
            "\b",
            InputEventSource.Synthetic)));

        ReplaceFormatCodeTextIntent edit = Assert.IsType<ReplaceFormatCodeTextIntent>(
            Assert.Single(edits).Intent);
        Assert.Empty(edit.Text);
    }

    [Fact]
    public void Committed_Text_Filters_Control_Characters()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("ab"));
        scene.View.IsEditable = true;
        scene.Session.SetFocus(scene.View);
        scene.View.SetSelection(1, 1);
        FormatCodeEditRequestedEventArgs? edit = null;
        scene.View.EditRequested += (_, args) => edit = args;

        Assert.True(scene.Route.Dispatch(new TextInputEvent(
            FormatCodeViewStandardHarness.Header("text"),
            "x\b\ty\r\n",
            InputEventSource.Synthetic)));

        ReplaceFormatCodeTextIntent intent = Assert.IsType<ReplaceFormatCodeTextIntent>(
            edit?.Intent);
        Assert.Equal("xy", intent.Text);
    }

    [Fact]
    public void Committed_Ime_Text_Filters_Control_Characters()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("ab"));
        scene.View.IsEditable = true;
        scene.Session.SetFocus(scene.View);
        scene.View.SetSelection(1, 1);
        FormatCodeEditRequestedEventArgs? edit = null;
        scene.View.EditRequested += (_, args) => edit = args;

        Assert.True(scene.Route.Dispatch(new TextCompositionEvent(
            FormatCodeViewStandardHarness.Header("ime"),
            "x\by",
            TextCompositionState.Committed)));

        ReplaceFormatCodeTextIntent intent = Assert.IsType<ReplaceFormatCodeTextIntent>(
            edit?.Intent);
        Assert.Equal("xy", intent.Text);
    }

    [Fact]
    public void Pointer_Click_Requests_Typed_Navigation_And_Drag_Selects()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("hello", new InlineStyle { Bold = true })]);
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeProjector.Project(document));
        scene.Session.RenderFrame();
        FormatCodeNavigationRequestedEventArgs? navigation = null;
        scene.View.NavigationRequested += (_, args) => navigation = args;

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.MouseDown(12, 12)));
        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.MouseMove(100, 12)));
        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.MouseUp(100, 12)));

        Assert.Same(scene.View, scene.Session.FocusedElement);
        Assert.NotNull(navigation);
        Assert.True(scene.View.HasSelection);
        Assert.True(document.IsValid(navigation.Mapping.DocumentPosition));
    }

    [Fact]
    public void Keyboard_Navigation_Preserves_Directional_Selection_And_Copies()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("abcdef"));
        scene.Session.SetFocus(scene.View);
        scene.View.SetSelection(5, 5);

        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("Left", BVirtualKey.Left, KeyboardModifierState.Shift));
        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("Left", BVirtualKey.Left, KeyboardModifierState.Shift));

        Assert.Equal(5, scene.View.SelectionAnchor);
        Assert.Equal(3, scene.View.SelectionFocus);
        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("C", BVirtualKey.C, KeyboardModifierState.Control)));
        Assert.Equal("de", scene.Host.ClipboardText);

        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("A", BVirtualKey.A, KeyboardModifierState.Control));
        Assert.Equal(scene.View.Text.Length, scene.View.SelectionLength);
    }

    [Fact]
    public void Search_Exit_And_Activation_Are_Exposed_To_The_Host()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("one two one"));
        scene.Session.SetFocus(scene.View);
        int searchRequests = 0;
        int exitRequests = 0;
        int navigationRequests = 0;
        scene.View.SearchRequested += (_, _) => searchRequests++;
        scene.View.ExitRequested += (_, _) => exitRequests++;
        scene.View.NavigationRequested += (_, _) => navigationRequests++;

        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("F", 0x46, KeyboardModifierState.Control));
        scene.View.Find("one");
        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("F3", 0x72));
        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("Enter", BVirtualKey.Enter));
        scene.Route.Dispatch(FormatCodeViewStandardHarness.Key("Escape", BVirtualKey.Escape));

        Assert.Equal(1, searchRequests);
        Assert.Equal(1, exitRequests);
        Assert.Equal(1, navigationRequests);
        Assert.Equal(8, scene.View.SelectionStart);
    }

    [Fact]
    public void Wheel_Scrolls_Overflowing_Content()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(160, 70),
            FormatCodeViewStandardHarness.Project(string.Join('\n', Enumerable.Repeat("line", 30))));
        scene.Session.RenderFrame();

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Wheel(20, 20, -2)));
        Assert.True(scene.View.VerticalScrollOffset > 0);
    }

    [Fact]
    public void A_Wheel_Tilted_Right_Scrolls_Right_And_Tilted_Left_Scrolls_Back()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(160, 70),
            FormatCodeViewStandardHarness.Project(string.Join('\n', Enumerable.Repeat("a long line that is much wider than the view", 3))));
        scene.View.Wrapping = FormatCodeViewWrapping.NoWrap;
        scene.Session.RenderFrame();

        // Win32 reports a wheel tilted right (WM_MOUSEHWHEEL) as a positive notch.
        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Wheel(20, 20, 1, MouseWheelAxis.Horizontal)));
        double right = scene.View.HorizontalScrollOffset;
        Assert.True(right > 0);
        Assert.Equal(0, scene.View.VerticalScrollOffset);

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Wheel(20, 20, -1, MouseWheelAxis.Horizontal)));
        Assert.True(scene.View.HorizontalScrollOffset < right);
    }

    [Theory]
    [InlineData(MouseWheelAxis.Vertical)]
    [InlineData(MouseWheelAxis.Horizontal)]
    public void Shift_With_The_Wheel_Turned_Towards_The_User_Scrolls_Right_However_The_Host_Reports_It(MouseWheelAxis axis)
    {
        // Broiler.Input and Broiler.Graphics report Shift with the wheel as a vertical notch with Shift;
        // Broiler.Hosting.Windows turns it into a horizontal one that keeps Shift and the vertical sign.
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(160, 70),
            FormatCodeViewStandardHarness.Project(string.Join('\n', Enumerable.Repeat("a long line that is much wider than the view", 30))));
        scene.View.Wrapping = FormatCodeViewWrapping.NoWrap;
        scene.Session.RenderFrame();

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Wheel(20, 20, -1, axis, InputModifiers.Shift)));
        double right = scene.View.HorizontalScrollOffset;
        Assert.True(right > 0);
        Assert.Equal(0, scene.View.VerticalScrollOffset);

        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Wheel(20, 20, 1, axis, InputModifiers.Shift)));
        Assert.True(scene.View.HorizontalScrollOffset < right);
        Assert.Equal(0, scene.View.VerticalScrollOffset);
    }

    [Fact]
    public void A_Tilt_Leaves_A_Wrapped_View_Where_It_Is()
    {
        // The default: lines wrap, so there is nothing to scroll sideways.
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(160, 70),
            FormatCodeViewStandardHarness.Project(string.Join('\n', Enumerable.Repeat("line", 30))));
        Assert.Equal(FormatCodeViewWrapping.Wrap, scene.View.Wrapping);
        scene.Session.RenderFrame();
        Assert.True(scene.Route.Dispatch(FormatCodeViewStandardHarness.Wheel(20, 20, -2)));
        double top = scene.View.VerticalScrollOffset;
        Assert.True(top > 0);

        // Neither tilt moves the lines, and both are left for a scroller outside the view.
        foreach (double notches in new[] { -1.0, 1.0 })
        {
            Assert.False(scene.Route.Dispatch(FormatCodeViewStandardHarness.Wheel(20, 20, notches, MouseWheelAxis.Horizontal)));
            Assert.Equal(top, scene.View.VerticalScrollOffset);
            Assert.Equal(0, scene.View.HorizontalScrollOffset);
        }
    }
}
