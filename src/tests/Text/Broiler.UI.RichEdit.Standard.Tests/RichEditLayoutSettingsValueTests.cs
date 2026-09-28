using Broiler.Graphics.Text;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// The layout settings snapshot: what zoom multiplies, how a stated size becomes
/// pixels, where a tab lands, and that every setting that moves a line is part
/// of the value a layout compares to decide whether it is current.
/// </summary>
public sealed class RichEditLayoutSettingsValueTests
{
    private static readonly BFontStyle Font = new("Segoe UI", 16);

    private static RichEditLayoutSettings Settings(double zoom = 1, double tab = 48) => new(300, zoom, Font, 24, tab);

    [Fact]
    public void A_Stated_Size_Is_Converted_From_Points_Before_It_Is_Zoomed()
    {
        BFontStyle font = Settings(zoom: 1.5).RunFont(InlineStyle.Default with { FontSize = 12, Bold = true, FontFamily = "Consolas" });

        Assert.Equal(BFontStyle.PointsToPixels(12) * 1.5, font.Size, 6);
        Assert.Equal("Consolas", font.FamilyName);
        Assert.Equal(BFontWeight.Bold, font.Weight);
    }

    [Fact]
    public void A_Run_That_States_Nothing_Takes_The_Controls_Own_Font()
    {
        BFontStyle font = Settings(zoom: 2).RunFont(InlineStyle.Default);

        Assert.Equal(Font.FamilyName, font.FamilyName);
        Assert.Equal(Font.Size * 2, font.Size, 6);
        Assert.Equal(Font.Weight, font.Weight);
    }

    [Fact]
    public void Zoomed_Out_Text_Is_Never_Smaller_Than_A_Pixel()
    {
        Assert.Equal(1, RichEditLayoutSettings.ZoomedFontFor(new BFontStyle("Segoe UI", 4), 0.1).Size);
    }

    [Fact]
    public void A_Tab_Always_Moves_On_To_The_Next_Stop()
    {
        Assert.Equal(48, Settings().NextTabStop(0), 6);
        Assert.Equal(48, Settings().NextTabStop(47.9), 6);
        Assert.Equal(96, Settings().NextTabStop(48), 6);
        Assert.Equal(48, Settings().NextTabStop(-10), 6);

        // An unusable width falls back to the default grid, which zoom scales.
        Assert.Equal(RichEditLayoutSettings.DefaultTabStopWidth, Settings(tab: 0).NextTabStop(0), 6);
        Assert.Equal(60, Settings(zoom: 2, tab: 30).NextTabStop(0), 6);
    }

    [Fact]
    public void Every_Setting_That_Moves_A_Line_Is_Part_Of_The_Value()
    {
        RichEditLayoutSettings settings = new(300, 1, Font, 24, 48);

        Assert.Equal(settings, new RichEditLayoutSettings(300, 1, Font, 24, 48));
        Assert.NotEqual(settings, settings with { ContentWidth = 301 });
        Assert.NotEqual(settings, settings with { Zoom = 1.1 });
        Assert.NotEqual(settings, settings with { Font = Font with { Size = 17 } });
        Assert.NotEqual(settings, settings with { IndentWidth = 25 });
        Assert.NotEqual(settings, settings with { TabStopWidth = 49 });
    }
}
