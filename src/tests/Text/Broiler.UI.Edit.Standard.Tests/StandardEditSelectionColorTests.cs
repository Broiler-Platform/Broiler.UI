using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;

using static Broiler.UI.Edit.Standard.Tests.EditStandardHarness;

namespace Broiler.UI.Edit.Standard.Tests;

/// <summary>
/// Selected text in the theme's selection text color (ADR 0029), and the presets' single run kept.
/// </summary>
public sealed class StandardEditSelectionColorTests
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
    public void Selected_Text_Is_Drawn_In_The_Selection_Color_And_Each_Glyph_Once()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.ApplyTheme(SystemPalette);
        scene.Edit.SetSelection(2, 5);

        BRenderList list = scene.Render();

        BRect selection = Assert.Single(list.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Color == Highlight).Rect;
        List<(BRect Clip, BColor Color)> strips = ClippedRuns(list, "Hello world");
        Assert.Equal([SystemPalette.Text, HighlightText, SystemPalette.Text], strips.Select(strip => strip.Color));

        // The strips meet at the selection's edges and do not overlap.
        Assert.Equal(selection.Left, strips[0].Clip.Right, 6);
        Assert.Equal(selection.Left, strips[1].Clip.Left, 6);
        Assert.Equal(selection.Width, strips[1].Clip.Width, 6);
        Assert.Equal(selection.Right, strips[2].Clip.Left, 6);

        // Without a selection the text is one run again.
        scene.Edit.SetSelection(0, 0);
        Assert.Equal([SystemPalette.Text], Runs(scene.Render(), "Hello world").Select(run => run.Color));
    }

    [Fact]
    public void A_Preset_Draws_The_Selected_Text_As_One_Run_That_Follows_Foreground()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.ApplyTheme(StandardThemeTokens.Light);
        scene.Edit.SetSelection(2, 5);

        Assert.Equal([StandardThemeTokens.Light.Text], Runs(scene.Render(), "Hello world").Select(run => run.Color));

        BColor custom = BColor.FromArgb(0xFF, 0x80, 0x10, 0x10);
        scene.Edit.Foreground = custom;
        Assert.Equal(custom, scene.Edit.SelectionForeground);
        Assert.Equal([custom], Runs(scene.Render(), "Hello world").Select(run => run.Color));
    }

    [Fact]
    public void The_Highlighted_Menu_Row_And_Its_Shortcut_Take_The_Selection_Color()
    {
        EditScene scene = Create("Hello world");
        scene.Edit.ApplyTheme(SystemPalette);
        scene.Edit.SetSelection(0, 5);
        scene.Edit.OpenContextMenu(new BPoint(scene.TextX(2), scene.CenterY()));
        Assert.True(scene.Route.Dispatch(Key("Down", 0x28)));
        StandardEditContextMenuItem highlighted = scene.Edit.ContextMenuItems[scene.Edit.ContextMenuHighlightedIndex];
        StandardEditContextMenuItem other = scene.Edit.ContextMenuItems.First(item => !item.IsSeparator && item.IsEnabled && item != highlighted && item.Shortcut.Length > 0);

        BRenderList list = scene.Render();

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
