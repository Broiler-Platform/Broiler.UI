using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The two-line presenter's unread dot is a mark, not text, so it needs 3:1 against the fill it is drawn on: the
/// list's background on an unselected row and the selection fill on a selected one (ADR 0034).
/// </summary>
[Collection(GlobalThemeCollection.Name)]
public sealed class UnreadDotContrastTests
{
    public static TheoryData<StandardThemeTokens> Presets => new()
    {
        StandardThemeTokens.Light,
        StandardThemeTokens.Dark,
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
        StandardThemeTokens.Light.WithTextScale(2),
        StandardThemeTokens.Dark.WithTextScale(1.5),
    };

    [Theory]
    [MemberData(nameof(Presets))]
    public void Every_Preset_Draws_The_Dot_At_Three_To_One_On_The_Row_It_Marks(StandardThemeTokens theme)
    {
        (BColor unselected, BColor selected) = RenderDots(theme);

        AssertStandsOut(unselected, theme.Surface, theme, "unselected row");
        AssertStandsOut(selected, theme.AccentSoft, theme, "selected row");
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Presets_Keep_The_Accent_Except_Where_It_Does_Not_Show_On_The_Selection(StandardThemeTokens theme)
    {
        (BColor unselected, BColor selected) = RenderDots(theme);

        // Unselected rows keep the accent in every preset; so do selected rows, except in Dark, whose accent is
        // 2.76:1 on its selection fill. Its accent text shade is 6.27:1 there.
        Assert.Equal(theme.Accent, unselected);
        bool dark = theme.Name == "Dark";
        Assert.Equal(dark ? theme.AccentText : theme.Accent, selected);
        if (dark)
            Assert.True(StandardContrast.Ratio(theme.Accent, theme.AccentSoft) < StandardContrast.AaLargeOrUi);
    }

    [Theory]
    [MemberData(nameof(HostingPaletteNames))]
    public void A_Palette_Built_From_A_Windows_Contrast_Theme_Draws_The_Dot_As_Before(string name)
    {
        StandardThemeTokens theme = HostingShapedPalettes.Named(name);

        (BColor unselected, BColor selected) = RenderDots(theme);

        // The highlight is the accent and the selection fill, so a selected row's dot takes the highlight text.
        Assert.Equal(theme.Accent, unselected);
        Assert.Equal(theme.SelectionText, selected);
        AssertStandsOut(unselected, theme.Surface, theme, "unselected row");
        AssertStandsOut(selected, theme.AccentSoft, theme, "selected row");
    }

    [Fact]
    public void A_Highlight_Whose_Text_Is_The_Window_Text_Gets_A_Dot_That_Shows()
    {
        // A dark highlight on a black window with white text on both: the highlight is lost on the window, and the
        // selected text is the ordinary text, so the selection gives the dot no color of its own.
        StandardThemeTokens theme = HostingShapedPalettes.Create("Dark highlight", 0x000000, 0xFFFFFF, 0x1A1A8C, 0xFFFFFF);
        Assert.Equal(theme.Text, theme.SelectionText);
        Assert.True(StandardContrast.Ratio(theme.Accent, theme.Surface) < StandardContrast.AaLargeOrUi);

        (BColor unselected, BColor selected) = RenderDots(theme);

        Assert.Equal(theme.AccentText, unselected);
        Assert.Equal(theme.AccentText, selected);
        AssertStandsOut(unselected, theme.Surface, theme, "unselected row");
        AssertStandsOut(selected, theme.AccentSoft, theme, "selected row");
    }

    [Fact]
    public void Without_A_Readable_Accent_Text_The_Dot_Takes_The_Rows_Text_Color()
    {
        StandardThemeTokens dark = StandardThemeTokens.Dark;

        // A caller written before the accent text shade leaves it unset, so it follows the accent, which is lost on
        // Dark's selection fill: the dot takes the selected text color, which the theme chose to read there.
        Assert.Equal(dark.Text, DotColor(Context(dark.Accent, dark.AccentSoft, dark.Text)));

        // A selection fill that is not opaque keeps the accent: what shows through it is not known.
        BColor translucent = BColor.FromArgb(0x80, dark.AccentSoft.R, dark.AccentSoft.G, dark.AccentSoft.B);
        Assert.Equal(dark.Accent, DotColor(Context(dark.Accent, translucent, dark.Text)));
    }

    [Fact]
    public void The_Single_Line_Presenter_Draws_No_Dot()
    {
        var list = new StandardListView();
        list.ApplyTheme(StandardThemeTokens.Dark);
        list.SetItems([new UiListItem("a", "Ann", "Agenda", "09:00", isRead: false)]);
        list.SelectedItemId = "a";

        Assert.DoesNotContain(Render(list).Commands.OfType<BRenderCommand.FillRect>(), static fill => fill.Rect.Width == 6 && fill.Rect.Height == 6);
    }

    [Fact]
    public void An_Accent_Set_After_The_Theme_Does_Not_Take_The_Themes_Accent_Text()
    {
        // A brand purple, lost on Dark's navy selection fill as Dark's own accent is. Dark's accent text is a shade
        // of Dark's blue, chosen for that accent, not for this one.
        StandardThemeTokens dark = StandardThemeTokens.Dark;
        BColor brand = BColor.FromArgb(0xFF, 0x6A, 0x1B, 0x9A);
        Assert.True(StandardContrast.Ratio(brand, dark.AccentSoft) < StandardContrast.AaLargeOrUi);

        (BColor unselected, BColor selected) = RenderDots(dark, list =>
        {
            list.Accent = brand;
            Assert.Equal(brand, list.AccentText);
        });

        // So the selected row's dot is the row's text color, as for a context that gives no accent text.
        Assert.NotEqual(dark.AccentText, selected);
        Assert.Equal(dark.Text, selected);
        AssertStandsOut(selected, dark.AccentSoft, dark, "selected row");
        Assert.NotEqual(dark.AccentText, unselected);
    }

    [Fact]
    public void The_Lists_Accent_Text_Belongs_To_The_Accent_It_Was_Chosen_For()
    {
        BColor brand = BColor.FromArgb(0xFF, 0x6A, 0x1B, 0x9A);
        BColor brandText = BColor.FromArgb(0xFF, 0xD3, 0xA6, 0xF0);
        StandardControlPaint.ApplyTheme(StandardThemeTokens.Dark);
        try
        {
            // Unthemed, the list takes the shared palette's pair, and a new accent brings no shade of the old one.
            var list = new StandardListView();
            Assert.Equal(StandardThemeTokens.Dark.AccentText, list.AccentText);
            list.Accent = brand;
            Assert.Equal(brand, list.AccentText);
            list.Accent = StandardThemeTokens.Dark.Accent;
            Assert.Equal(StandardThemeTokens.Dark.AccentText, list.AccentText);

            // A shade the application sets is its own, whatever the accent, until the next theme.
            list.AccentText = brandText;
            list.Accent = brand;
            Assert.Equal(brandText, list.AccentText);
            list.ApplyTheme(StandardThemeTokens.Light);
            Assert.Equal(StandardThemeTokens.Light.AccentText, list.AccentText);
            list.Accent = brand;
            Assert.Equal(brand, list.AccentText);
        }
        finally
        {
            StandardControlPaint.ApplyTheme(StandardThemeTokens.Light);
        }
    }

    public static TheoryData<string> HostingPaletteNames => HostingShapedPalettes.Names;

    /// <summary>The dots of an unread row and an unread selected row in a themed list.</summary>
    private static (BColor Unselected, BColor Selected) RenderDots(StandardThemeTokens theme, Action<StandardListView>? adjust = null)
    {
        var list = new StandardListView { ItemPresenter = StandardTwoLineListItemPresenter.Instance };
        list.ApplyTheme(theme);
        adjust?.Invoke(list);
        list.SetItems(
        [
            new UiListItem("a", "Ann", "Agenda", "09:00", isRead: false),
            new UiListItem("b", "Ben", "Budget", "10:00", isRead: false),
        ]);
        list.SelectedItemId = "b";

        BColor[] dots = Render(list).Commands.OfType<BRenderCommand.FillRect>()
            .Where(static fill => fill.Rect.Width == 6 && fill.Rect.Height == 6)
            .Select(static fill => fill.Color)
            .ToArray();
        Assert.Equal(2, dots.Length);
        return (dots[0], dots[1]);
    }

    /// <summary>A selected unread row's context with no accent text shade of its own.</summary>
    private static UiListItemRenderContext Context(BColor accent, BColor fill, BColor text) => new()
    {
        RenderList = new BRenderList(),
        Bounds = new BRect(0, 0, 320, 52),
        Item = new UiListItem("a", "Ann", "Agenda", "09:00", isRead: false),
        State = new UiListItemState(true, false, false, 0),
        Font = StandardThemeTokens.Dark.FontBody,
        Foreground = text,
        SecondaryForeground = text,
        Background = StandardThemeTokens.Dark.Surface,
        SelectedBackground = fill,
        FocusRing = text,
        Accent = accent,
    };

    private static BColor DotColor(UiListItemRenderContext context)
    {
        StandardTwoLineListItemPresenter.Instance.Render(context);
        return Assert.Single(context.RenderList.Commands.OfType<BRenderCommand.FillRect>(), static fill => fill.Rect.Width == 6 && fill.Rect.Height == 6).Color;
    }

    private static BRenderList Render(StandardListView list)
    {
        BSize size = new(320, 200);
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new ScrollbarThemeRoleTests.TestHost(size));
        session.AddRoot(list);
        list.Measure(size);
        list.Arrange(new BRect(0, 0, size.Width, size.Height));
        BRenderList renderList = StandardRenderTraversal.Render(session);
        session.RemoveRoot(list);
        return renderList;
    }

    private static void AssertStandsOut(BColor dot, BColor fill, StandardThemeTokens theme, string row)
    {
        double ratio = StandardContrast.Ratio(dot, fill);
        Assert.True(ratio >= StandardContrast.AaLargeOrUi, $"{theme.Name}: the dot on the {row} is {ratio:0.00}:1.");
    }
}
