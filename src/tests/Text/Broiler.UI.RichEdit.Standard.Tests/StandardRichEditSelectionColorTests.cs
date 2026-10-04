using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;

using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Selected text in the theme's selection text color (ADR 0029). Each line is drawn once per strip - before,
/// on, and after the selection - each clipped to its strip, a strip with no text left in it is not drawn, and
/// the presets keep their single runs.
/// </summary>
public sealed class StandardRichEditSelectionColorTests
{
    private static readonly BColor Highlight = BColor.FromArgb(0xFF, 0x1A, 0xEB, 0xFF);
    private static readonly BColor HighlightText = BColor.FromArgb(0xFF, 0x00, 0x00, 0x00);

    private static readonly StandardThemeTokens SystemPalette = StandardThemeTokens.HighContrastDark with
    {
        Accent = Highlight,
        AccentSoft = Highlight,
        SelectionText = HighlightText,
    };

    [Fact]
    public void Selected_Text_Is_Drawn_In_The_Selection_Color_Between_The_Selection_Edges()
    {
        RichEditScene scene = Create(new BSize(320, 160), "hello world");
        using UiSession session = scene.Session;
        scene.Edit.ApplyTheme(SystemPalette);
        session.SetFocus(scene.Edit);
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 2), new RichTextPosition(0, 5));

        BRenderList list = session.RenderFrame();

        BRect selection = Assert.Single(list.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Color == Highlight).Rect;
        List<(BRect Clip, BColor Color)> strips = ClippedRuns(list, "hello world");
        Assert.Equal([SystemPalette.Text, HighlightText, SystemPalette.Text], strips.Select(strip => strip.Color));
        Assert.Equal(selection.Left, strips[0].Clip.Right, 6);
        Assert.Equal(selection.Left, strips[1].Clip.Left, 6);
        Assert.Equal(selection.Width, strips[1].Clip.Width, 6);
        Assert.Equal(selection.Right, strips[2].Clip.Left, 6);

        // A disabled editor still fills its selection, so the selected text takes the selection color there
        // too, while the rest keeps its dimmed color.
        scene.Edit.IsEnabled = false;
        list = session.RenderFrame();
        Assert.Contains(list.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Color == Highlight);
        Assert.Equal(
            [scene.Edit.PlaceholderForeground, HighlightText, scene.Edit.PlaceholderForeground],
            ClippedRuns(list, "hello world").Select(strip => strip.Color));
    }

    [Fact]
    public void A_Line_Selected_From_End_To_End_Is_Drawn_Once_And_A_Selection_From_Its_Start_Twice()
    {
        RichEditScene scene = Create(new BSize(320, 160), "hello world");
        using UiSession session = scene.Session;
        scene.Edit.ApplyTheme(SystemPalette);
        session.SetFocus(scene.Edit);
        BRect unselectedClip = Assert.Single(ClippedRuns(session.RenderFrame(), "hello world")).Clip;
        scene.Edit.ExecuteCommand(RichEditCommand.SelectAll);

        BRenderList list = session.RenderFrame();

        // No strip beside the selection holds any of the line, so it is drawn once, under the clip it has
        // without a selection.
        Assert.Equal([(unselectedClip, HighlightText)], ClippedRuns(list, "hello world"));

        // From the start of the line to its middle: the selected strip runs to the left edge, then the rest.
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 0), new RichTextPosition(0, 5));
        list = session.RenderFrame();
        BRect selection = Assert.Single(list.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Color == Highlight).Rect;
        List<(BRect Clip, BColor Color)> strips = ClippedRuns(list, "hello world");
        Assert.Equal([HighlightText, SystemPalette.Text], strips.Select(strip => strip.Color));
        Assert.True(strips[0].Clip.Left <= selection.Left);
        Assert.Equal(selection.Right, strips[0].Clip.Right, 6);
        Assert.Equal(selection.Right, strips[1].Clip.Left, 6);
    }

    [Fact]
    public void A_Run_Keeps_Its_Own_Color_Off_The_Selection_And_Takes_The_Selection_Color_On_It()
    {
        RichEditScene scene = Create(new BSize(320, 160), "hello world");
        using UiSession session = scene.Session;
        scene.Edit.ApplyTheme(SystemPalette);
        session.SetFocus(scene.Edit);
        scene.Edit.ExecuteCommand(RichEditCommand.SelectAll);
        scene.Edit.ExecuteCommand(RichEditCommand.SetForeground, BColor.Red);
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 2), new RichTextPosition(0, 8));

        List<(BRect Clip, BColor Color)> strips = ClippedRuns(session.RenderFrame(), "hello world");

        Assert.Equal([BColor.Red, HighlightText, BColor.Red], strips.Select(strip => strip.Color));

        // To the end of the line, no text is left after the selection to draw in the run's color.
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 6), new RichTextPosition(0, 11));
        strips = ClippedRuns(session.RenderFrame(), "hello world");
        Assert.Equal([BColor.Red, HighlightText], strips.Select(strip => strip.Color));
    }

    [Fact]
    public void A_Preset_Draws_Selected_Text_As_One_Run_In_Its_Own_Color()
    {
        RichEditScene scene = Create(new BSize(320, 160), "hello world");
        using UiSession session = scene.Session;
        scene.Edit.ApplyTheme(StandardThemeTokens.Light);
        session.SetFocus(scene.Edit);
        scene.Edit.Selection = new RichTextRange(new RichTextPosition(0, 2), new RichTextPosition(0, 5));

        Assert.Null(scene.Edit.SelectionForeground);
        Assert.Equal([StandardThemeTokens.Light.Text], ClippedRuns(session.RenderFrame(), "hello world").Select(strip => strip.Color));
    }

    [Fact]
    public void The_Highlighted_Menu_Row_And_Its_Shortcut_Take_The_Selection_Color()
    {
        RichEditScene scene = Create(new BSize(400, 300), "hello");
        using UiSession session = scene.Session;
        scene.Edit.ApplyTheme(SystemPalette);
        scene.Edit.ExecuteCommand(RichEditCommand.SelectAll);
        scene.Edit.OpenContextMenu(new BPoint(20, 40));
        scene.Route.Dispatch(Key("Down", 0x28));
        StandardRichEditContextMenuItem highlighted = scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex];
        StandardRichEditContextMenuItem other = scene.Edit.ContextMenuItems.First(item => !item.IsSeparator && item.IsEnabled && item != highlighted && item.Shortcut.Length > 0);
        Assert.NotEqual(string.Empty, highlighted.Shortcut);

        BRenderList list = session.RenderFrame();

        Assert.Equal(HighlightText, Assert.Single(Runs(list, highlighted.Text)).Color);
        Assert.Equal(HighlightText, Assert.Single(Runs(list, highlighted.Shortcut)).Color);
        Assert.Equal(SystemPalette.Text, Assert.Single(Runs(list, other.Text)).Color);
        Assert.Equal(SystemPalette.TextDisabled, Assert.Single(Runs(list, other.Shortcut)).Color);
    }

    private static IEnumerable<BTextRun> Runs(BRenderList list, string text) =>
        list.Commands.OfType<BRenderCommand.DrawText>().Select(command => command.Text).Where(run => run.Text == text);

    /// <summary>Each draw of <paramref name="text"/> with the clip it was drawn under.</summary>
    private static List<(BRect Clip, BColor Color)> ClippedRuns(BRenderList list, string text)
    {
        var clips = new Stack<BRect>();
        var runs = new List<(BRect, BColor)>();
        foreach (BRenderCommand command in list.Commands)
        {
            if (command is BRenderCommand.PushClip push)
                clips.Push(push.Rect);
            else if (command is BRenderCommand.PopClip)
                clips.Pop();
            else if (command is BRenderCommand.DrawText draw && draw.Text.Text == text)
                runs.Add((clips.Peek(), draw.Text.Color));
        }

        return runs;
    }
}
