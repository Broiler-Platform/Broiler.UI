using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The list's frame stays whole over its scrollbar, and its focus ring stays whole where it crosses an opaque thumb
/// of too nearly its color (ADR 0034).
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class ListViewFrameAndFocusRingTests
{
    private static readonly BRect ListBounds = new(0, 0, 220, 120);

    public static TheoryData<string> Palettes => new() { "Light", "Dark", "HighContrastLight", "HighContrastDark", "Aquatic", "Desert", "Dusk", "Night sky" };

    public static TheoryData<string> HostingPaletteNames => HostingShapedPalettes.Names;

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

    [Theory]
    [MemberData(nameof(HostingPaletteNames))]
    public void In_A_Windows_Contrast_Theme_The_Focus_Ring_Stays_Whole_Where_It_Crosses_The_Thumb(string name)
    {
        StandardThemeTokens theme = HostingShapedPalettes.Named(name);
        StandardListView list = ScrollingList(theme);

        BRenderCommand[] commands = Render(list, focused: true);

        BRect ringRect = StandardControlPaint.Inset(ListBounds, 2);
        BRenderCommand.StrokeRoundedRect ring = Assert.Single(commands.OfType<BRenderCommand.StrokeRoundedRect>(), stroke => stroke.Rect == ringRect && stroke.Color == theme.FocusRing);
        (_, BRenderCommand.FillRoundedRect thumb) = Bar(commands, theme);

        // The ring's right side runs through the bar, and the highlight is lost on the window text thumb.
        Assert.InRange(ring.Rect.Right, thumb.Rect.Left, thumb.Rect.Right);
        Assert.InRange(StandardContrast.Ratio(ring.Color, thumb.Color), 1.4, 1.95);

        // So the stretch over the thumb is drawn again after the ring, clipped to the thumb, in the window color.
        BRenderCommand[] after = commands.Skip(Array.IndexOf(commands, ring) + 1).ToArray();
        BRenderCommand.PushClip clip = Assert.IsType<BRenderCommand.PushClip>(after[0]);
        BRenderCommand.StrokeRoundedRect across = Assert.IsType<BRenderCommand.StrokeRoundedRect>(after[1]);
        Assert.IsType<BRenderCommand.PopClip>(after[2]);
        Assert.Equal(thumb.Rect, clip.Rect);
        Assert.Equal((ring.Rect, ring.RadiusX, ring.RadiusY, ring.Thickness), (across.Rect, across.RadiusX, across.RadiusY, across.Thickness));
        Assert.Equal(theme.Surface, across.Color);
        Assert.True(StandardContrast.Ratio(across.Color, thumb.Color) >= StandardContrast.AaLargeOrUi);
    }

    [Theory]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void The_High_Contrast_Presets_Draw_The_Ring_Across_Their_Thumb_In_The_Surface_Color(string palette)
    {
        StandardThemeTokens theme = Palette(palette);

        BRenderCommand[] commands = Render(ScrollingList(theme), focused: true);

        // Black on black and yellow on white: the ring would be lost on the thumb.
        Assert.True(StandardContrast.Ratio(theme.FocusRing, theme.ScrollbarThumb) < StandardContrast.AaLargeOrUi);
        BRenderCommand.StrokeRoundedRect across = Assert.Single(
            commands.OfType<BRenderCommand.StrokeRoundedRect>(),
            stroke => stroke.Rect == StandardControlPaint.Inset(ListBounds, 2) && stroke.Color != theme.FocusRing);
        Assert.Equal(theme.Surface, across.Color);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Light_And_Dark_Draw_The_Ring_Once(string palette)
    {
        StandardThemeTokens theme = Palette(palette);

        BRenderCommand[] commands = Render(ScrollingList(theme), focused: true);

        // Their thumbs are mid tones: the surface would show no better on them than the ring does, so the ring is
        // drawn as it always was.
        Assert.True(StandardContrast.Ratio(theme.Surface, theme.ScrollbarThumb) < StandardContrast.AaLargeOrUi);
        BRenderCommand.StrokeRoundedRect ring = Assert.Single(commands.OfType<BRenderCommand.StrokeRoundedRect>(), stroke => stroke.Rect == StandardControlPaint.Inset(ListBounds, 2));
        Assert.Equal(theme.FocusRing, ring.Color);
        Assert.Same(ring, commands[^1]);
    }

    [Theory]
    [MemberData(nameof(HostingPaletteNames))]
    public void A_Selected_Rows_Fill_Ends_Short_Of_The_Bar(string name)
    {
        StandardThemeTokens theme = HostingShapedPalettes.Named(name);
        StandardListView list = ScrollingList(theme);
        list.SelectedItemId = "item0";

        BRenderCommand[] commands = Render(list, focused: true);

        // The highlight fill stops 2 DIP short of the bar, so the window color, not the highlight, borders the
        // thumb's side.
        BRenderCommand.FillRect selection = Assert.Single(commands.OfType<BRenderCommand.FillRect>(), fill => fill.Color == theme.AccentSoft);
        (BRenderCommand.FillRoundedRect track, _) = Bar(commands, theme);
        Assert.True(selection.Rect.Right <= track.Rect.Left - 2, $"The selection ends at {selection.Rect.Right}, the bar starts at {track.Rect.Left}.");
        Assert.Equal(theme.Surface, list.Background);
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
