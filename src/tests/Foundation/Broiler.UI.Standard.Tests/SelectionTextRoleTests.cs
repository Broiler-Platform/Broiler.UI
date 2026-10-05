using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Xunit;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The selection text roles (ADR 0029): text on the selection fill has a color of its own that the presets
/// leave at the text color, and list rows draw with it.
/// </summary>
public sealed class SelectionTextRoleTests
{
    private static readonly BColor Highlight = BColor.FromArgb(0xFF, 0x1A, 0xEB, 0xFF);
    private static readonly BColor HighlightText = BColor.FromArgb(0xFF, 0x00, 0x00, 0x00);

    public static TheoryData<StandardThemeTokens> Presets() => new()
    {
        StandardThemeTokens.Light,
        StandardThemeTokens.Dark,
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
    };

    [Theory]
    [MemberData(nameof(Presets))]
    public void Presets_Draw_Selected_Text_In_Their_Text_Colors(StandardThemeTokens theme)
    {
        Assert.Equal(theme.Text, theme.SelectionText);
        Assert.Equal(theme.TextMuted, theme.SelectionTextMuted);
        Assert.Equal(theme.Name.StartsWith("HighContrast", StringComparison.Ordinal), theme.IsHighContrast);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Selection_Text_Meets_WCAG_AA_On_The_Selection_Fill(StandardThemeTokens theme)
    {
        AssertMeets(theme.SelectionText, theme.AccentSoft, theme, "SelectionText/AccentSoft");
        AssertMeets(theme.SelectionTextMuted, theme.AccentSoft, theme, "SelectionTextMuted/AccentSoft");
        Assert.Equal(StandardContrast.Ratio(theme.SelectionText, theme.AccentSoft), theme.SelectionTextContrast, 6);
    }

    [Fact]
    public void A_System_Highlight_Pair_Gives_All_Selected_Text_Its_Color()
    {
        StandardThemeTokens system = SystemHighContrast();

        Assert.Equal(HighlightText, system.SelectionText);
        // No muted variant of a system pair exists, so secondary text on the selection takes the pair's text.
        // (Whether a real system pair is readable is the host's to check, where its palette is built.)
        Assert.Equal(HighlightText, system.SelectionTextMuted);

        // An explicit muted role still wins.
        BColor muted = BColor.FromArgb(0xFF, 0x20, 0x20, 0x20);
        Assert.Equal(muted, (system with { SelectionTextMuted = muted }).SelectionTextMuted);
    }

    [Fact]
    public void Copies_Keep_High_Contrast_And_Follow_A_New_Text_Color()
    {
        Assert.True(StandardThemeTokens.HighContrastDark.WithTextScale(2).IsHighContrast);
        Assert.True(StandardThemeTokens.Select(UiSystemSettings.Default with { ContrastPreference = UiContrastPreference.More, TextScale = 1.5 }).IsHighContrast);
        Assert.False(StandardThemeTokens.Select(UiSystemSettings.Default with { TextScale = 1.5 }).IsHighContrast);
        Assert.False(new StandardThemeTokens(BColor.Black, BColor.White, BColor.Green, BColor.Red).IsHighContrast);

        // The presets leave the selection roles unset, so a copy with other text colors selects with them.
        BColor text = BColor.FromArgb(0xFF, 0xEE, 0xEE, 0x00);
        StandardThemeTokens recolored = StandardThemeTokens.HighContrastDark with { Text = text, TextMuted = text };
        Assert.Equal(text, recolored.SelectionText);
        Assert.Equal(text, recolored.SelectionTextMuted);
        Assert.Equal(BColor.White, new StandardThemeTokens(BColor.Black, BColor.White, BColor.Green, BColor.Red).SelectionText);
    }

    [Fact]
    public void Selected_Rows_Draw_With_The_Selection_Text_Color()
    {
        StandardThemeTokens system = SystemHighContrast();
        var list = new StandardListView();
        list.ApplyTheme(system);
        list.SetItems([new UiListItem("a", "Alpha"), new UiListItem("b", "Bravo")]);
        list.SelectedItemId = "b";

        BRenderList renderList = Render(list, new BSize(240, 120));

        Assert.Equal(system.Text, TextColor(renderList, "Alpha"));
        Assert.Equal(HighlightText, TextColor(renderList, "Bravo"));
        Assert.Equal(HighlightText, list.SelectedForeground);
        Assert.Equal(HighlightText, list.SelectedSecondaryForeground);
    }

    [Fact]
    public void Selected_Two_Line_Rows_Draw_Every_Line_And_The_Unread_Dot_With_The_Selection_Text_Color()
    {
        StandardThemeTokens system = SystemHighContrast();
        var list = new StandardListView { ItemPresenter = StandardTwoLineListItemPresenter.Instance };
        list.ApplyTheme(system);
        list.SetItems(
        [
            new UiListItem("a", "Ann", "Agenda", "09:00", isRead: false),
            new UiListItem("b", "Ben", "Budget", "10:00", isRead: false),
        ]);
        list.SelectedItemId = "b";

        BRenderList renderList = Render(list, new BSize(320, 160));

        Assert.Equal(system.Text, TextColor(renderList, "Ann"));
        Assert.Equal(system.TextMuted, TextColor(renderList, "Agenda"));
        Assert.Equal(system.TextMuted, TextColor(renderList, "09:00"));
        Assert.Equal(HighlightText, TextColor(renderList, "Ben"));
        Assert.Equal(HighlightText, TextColor(renderList, "Budget"));
        Assert.Equal(HighlightText, TextColor(renderList, "10:00"));

        // The accent is the selection fill here, so only the unselected row's dot is in the accent.
        List<BColor> dots = renderList.Commands.OfType<BRenderCommand.FillRect>()
            .Where(static fill => fill.Rect.Width == 6 && fill.Rect.Height == 6)
            .Select(static fill => fill.Color)
            .ToList();
        Assert.Equal([system.Accent, HighlightText], dots);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void Presets_Draw_Selected_Rows_As_Before(StandardThemeTokens theme)
    {
        var list = new StandardListView { ItemPresenter = StandardTwoLineListItemPresenter.Instance };
        list.ApplyTheme(theme);
        list.SetItems([new UiListItem("a", "Ann", "Agenda", "09:00", isRead: false)]);
        list.SelectedItemId = "a";

        BRenderList renderList = Render(list, new BSize(320, 160));

        Assert.Equal(theme.Text, TextColor(renderList, "Ann"));
        Assert.Equal(theme.TextMuted, TextColor(renderList, "Agenda"));
        Assert.Equal(theme.TextMuted, TextColor(renderList, "09:00"));
        Assert.Contains(renderList.Commands.OfType<BRenderCommand.FillRect>(), fill => fill.Rect.Width == 6 && fill.Color == theme.Accent);

        // A foreground the application sets after the theme still reaches the selected row, as it always has.
        BColor custom = BColor.FromArgb(0xFF, 0x80, 0x10, 0x10);
        list.Foreground = custom;
        Assert.Equal(custom, list.SelectedForeground);
        Assert.Equal(custom, TextColor(Render(list, new BSize(320, 160)), "Ann"));
    }

    [Fact]
    public void The_High_Contrast_Token_Outlines_The_Selected_Row()
    {
        // Light's surface and text are not far enough apart for the luminance test to call it high contrast.
        StandardThemeTokens flagged = StandardThemeTokens.Light with { IsHighContrast = true };

        Assert.Empty(SelectionOutlines(StandardThemeTokens.Light));
        Assert.Single(SelectionOutlines(flagged));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_Focus_Ring_Of_A_High_Contrast_Row_Leaves_The_Selection_Outline_Visible(bool twoLine)
    {
        // A system palette whose focus ring is the highlight that fills the selection.
        StandardThemeTokens system = SystemHighContrast() with { FocusRing = Highlight };
        var bounds = new BRect(0, 0, 240, 52);

        List<BRenderCommand.StrokeRect> strokes = RenderFocusedSelectedRow(system, bounds, twoLine);

        // The outline is not painted over, and the ring inside it is drawn in the selected text color, since
        // the ring's own color would vanish on the fill.
        Assert.Equal(
            [(StandardControlPaint.Inset(bounds, 2), system.Text), (StandardControlPaint.Inset(bounds, 4), HighlightText)],
            strokes.Select(stroke => (stroke.Rect, stroke.Color)));

        // A high-contrast preset keeps its own ring color, inside the outline.
        StandardThemeTokens preset = StandardThemeTokens.HighContrastDark;
        Assert.Equal(
            [(StandardControlPaint.Inset(bounds, 2), preset.Text), (StandardControlPaint.Inset(bounds, 4), preset.FocusRing)],
            RenderFocusedSelectedRow(preset, bounds, twoLine).Select(stroke => (stroke.Rect, stroke.Color)));

        // Elsewhere the ring is where it always was, and there is no outline.
        StandardThemeTokens light = StandardThemeTokens.Light;
        Assert.Equal(
            [(StandardControlPaint.Inset(bounds, 2), light.FocusRing)],
            RenderFocusedSelectedRow(light, bounds, twoLine).Select(stroke => (stroke.Rect, stroke.Color)));
    }

    [Fact]
    public void A_Context_For_Another_Item_Keeps_Every_Member()
    {
        var context = new UiListItemRenderContext
        {
            RenderList = new BRenderList(),
            Bounds = new BRect(1, 2, 3, 4),
            Item = new UiListItem("a", "Alpha"),
            State = new UiListItemState(true, true, false, 3, UiDensity.Compact),
            Font = StandardThemeTokens.Light.FontBody,
            Foreground = BColor.Black,
            SecondaryForeground = BColor.Green,
            Background = BColor.White,
            SelectedBackground = Highlight,
            SelectedForeground = HighlightText,
            SelectedSecondaryForeground = BColor.Red,
            FocusRing = BColor.Blue,
            Accent = BColor.FromArgb(0xFF, 0xFF, 0xD7, 0x00),
            IsHighContrast = true,
        };
        var other = new UiListItem("b", "Bravo");

        UiListItemRenderContext copy = context.WithItem(other);

        Assert.Same(other, copy.Item);
        Assert.Same(context.RenderList, copy.RenderList);
        Assert.Equal(context.Bounds, copy.Bounds);
        Assert.Equal(context.State, copy.State);
        Assert.Equal(context.Font, copy.Font);
        Assert.Equal(
            [context.Foreground, context.SecondaryForeground, context.Background, context.SelectedBackground, context.SelectedForeground, context.SelectedSecondaryForeground, context.FocusRing, context.Accent],
            [copy.Foreground, copy.SecondaryForeground, copy.Background, copy.SelectedBackground, copy.SelectedForeground, copy.SelectedSecondaryForeground, copy.FocusRing, copy.Accent]);
        Assert.True(copy.IsHighContrast);

        // Unset selected colors stay unset, so they keep following the copied foregrounds.
        var plain = new UiListItemRenderContext
        {
            RenderList = context.RenderList,
            Bounds = context.Bounds,
            Item = context.Item,
            State = context.State,
            Font = context.Font,
            Foreground = BColor.Black,
            SecondaryForeground = BColor.Green,
            Background = BColor.White,
            SelectedBackground = Highlight,
            FocusRing = BColor.Blue,
            Accent = BColor.FromArgb(0xFF, 0xFF, 0xD7, 0x00),
        };
        Assert.Equal(BColor.Black, plain.WithItem(other).SelectedForeground);
        Assert.Equal(BColor.Green, plain.WithItem(other).SelectedSecondaryForeground);
    }

    /// <summary>
    /// The shape of a palette a host builds from a system contrast theme: the preset with the system's window
    /// and highlight pairs, where the accent and the selection fill are the same highlight color.
    /// </summary>
    internal static StandardThemeTokens SystemHighContrast() =>
        StandardThemeTokens.HighContrastDark with
        {
            Name = "HighContrastSystem",
            Surface = BColor.FromArgb(0xFF, 0x20, 0x20, 0x20),
            Text = BColor.White,
            TextMuted = BColor.White,
            Accent = Highlight,
            AccentSoft = Highlight,
            OnAccent = HighlightText,
            SelectionText = HighlightText,
        };

    private static List<BRenderCommand.StrokeRect> RenderFocusedSelectedRow(StandardThemeTokens theme, BRect bounds, bool twoLine)
    {
        var renderList = new BRenderList();
        bool distinct = theme.SelectionText != theme.Text;
        var context = new UiListItemRenderContext
        {
            RenderList = renderList,
            Bounds = bounds,
            Item = new UiListItem("a", "Alpha", "Agenda", "09:00"),
            State = new UiListItemState(true, true, true, 0),
            Font = theme.FontBody,
            Foreground = theme.Text,
            SecondaryForeground = theme.TextMuted,
            Background = theme.Surface,
            SelectedBackground = theme.AccentSoft,
            SelectedForeground = distinct ? theme.SelectionText : theme.Text,
            FocusRing = theme.FocusRing,
            Accent = theme.Accent,
            IsHighContrast = theme.IsHighContrast,
        };
        IUiListItemPresenter presenter = twoLine ? StandardTwoLineListItemPresenter.Instance : DefaultListItemPresenter.Instance;
        presenter.Render(context);
        return renderList.Commands.OfType<BRenderCommand.StrokeRect>().ToList();
    }

    private static List<BRenderCommand.StrokeRect> SelectionOutlines(StandardThemeTokens theme)
    {
        var list = new StandardListView();
        list.ApplyTheme(theme);
        list.SetItems([new UiListItem("a", "Alpha")]);
        list.SelectedItemId = "a";
        BRenderList renderList = Render(list, new BSize(240, 120));
        return renderList.Commands.OfType<BRenderCommand.StrokeRect>().Where(stroke => stroke.Color == theme.Text).ToList();
    }

    private static BColor TextColor(BRenderList renderList, string text) =>
        renderList.Commands.OfType<BRenderCommand.DrawText>().Single(command => command.Text.Text == text).Text.Color;

    private static BRenderList Render(UiElement element, BSize size)
    {
        using UiSession session = new StandardUiSessionBuilder().WithDispatcher(new ImmediateUiDispatcher()).Build(new TestHost(size));
        session.AddRoot(element);
        element.Measure(size);
        element.Arrange(new BRect(0, 0, size.Width, size.Height));
        BRenderList renderList = StandardRenderTraversal.Render(session);
        session.RemoveRoot(element);
        return renderList;
    }

    private static void AssertMeets(BColor foreground, BColor background, StandardThemeTokens theme, string pair)
    {
        double ratio = StandardContrast.Ratio(foreground, background);
        Assert.True(
            ratio >= StandardContrast.AaNormalText,
            $"{theme.Name}: {pair} contrast {ratio:0.00}:1 is below the required {StandardContrast.AaNormalText:0.0}:1.");
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
