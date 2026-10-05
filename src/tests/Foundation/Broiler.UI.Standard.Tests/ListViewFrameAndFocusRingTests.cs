using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The list's frame stays whole over its scrollbar (ADR 0034).
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class ListViewFrameAndFocusRingTests
{
    private static readonly BRect ListBounds = new(0, 0, 220, 120);

    public static TheoryData<string> Palettes => new() { "Light", "Dark", "HighContrastLight", "HighContrastDark", "Aquatic", "Desert", "Dusk", "Night sky" };

    [Theory]
    [MemberData(nameof(Palettes))]
    public void The_Frame_Is_Drawn_Over_The_Bar_So_Its_Right_Side_Is_Not_Covered(string palette)
    {
        StandardThemeTokens theme = Palette(palette);
        StandardListView list = ScrollingList(theme);

        BRenderCommand[] commands = Render(list, focused: false);

        BRenderCommand.StrokeRoundedRect frame = Assert.Single(commands.OfType<BRenderCommand.StrokeRoundedRect>(), stroke => stroke.Rect == ListBounds);
        Assert.Equal(theme.Border, frame.Color);
        (BRenderCommand.FillRoundedRect track, BRenderCommand.FillRoundedRect thumb) = Bar(commands, theme);

        // The bar runs the whole height of the list along its right edge: the frame's right side.
        Assert.Equal(new BRect(ListBounds.Right - 12, ListBounds.Top, 12, ListBounds.Height), track.Rect);
        Assert.Equal(ListBounds.Right, thumb.Rect.Right);

        // It is drawn first, and nothing is filled after the frame, so the frame is whole on every side.
        int frameIndex = Array.IndexOf(commands, frame);
        Assert.True(Array.IndexOf(commands, track) < frameIndex && Array.IndexOf(commands, thumb) < frameIndex, "The bar is drawn after the frame.");
        Assert.DoesNotContain(commands.Skip(frameIndex + 1), static command => command is BRenderCommand.FillRect or BRenderCommand.FillRoundedRect);
    }

    private static StandardThemeTokens Palette(string name) => name switch
    {
        "Light" => StandardThemeTokens.Light,
        "Dark" => StandardThemeTokens.Dark,
        "HighContrastLight" => StandardThemeTokens.HighContrastLight,
        "HighContrastDark" => StandardThemeTokens.HighContrastDark,
        _ => HostingShapedPalettes.Named(name),
    };

    private static StandardListView ScrollingList(StandardThemeTokens theme)
    {
        var list = new StandardListView();
        list.ApplyTheme(theme);
        list.SetItems(Enumerable.Range(0, 60).Select(index => new UiListItem($"item{index}", $"Item {index}")).ToArray());
        return list;
    }

    /// <summary>The track and the thumb of the list's bar: fills a bar wide in the theme's scrollbar colors.</summary>
    private static (BRenderCommand.FillRoundedRect Track, BRenderCommand.FillRoundedRect Thumb) Bar(BRenderCommand[] commands, StandardThemeTokens theme)
    {
        BRenderCommand.FillRoundedRect track = Assert.Single(commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect.Width == 12 && fill.Color == theme.ScrollbarTrack);
        BRenderCommand.FillRoundedRect thumb = Assert.Single(commands.OfType<BRenderCommand.FillRoundedRect>(), fill => fill.Rect.Width == 12 && fill.Color == theme.ScrollbarThumb);
        Assert.True(thumb.Rect.Height < track.Rect.Height, "The thumb is shorter than its track.");
        return (track, thumb);
    }

    private static BRenderCommand[] Render(StandardListView list, bool focused)
    {
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new ScrollbarThemeRoleTests.TestHost(new BSize(400, 300)));
        var root = new ScrollbarThemeRoleTests.FixedRoot((list, ListBounds));
        session.AddRoot(root);
        session.RenderFrame();
        if (focused)
            session.SetFocus(list);
        BRenderCommand[] commands = session.RenderFrame().Commands.ToArray();
        session.RemoveRoot(root);
        root.Release();
        return commands;
    }
}
