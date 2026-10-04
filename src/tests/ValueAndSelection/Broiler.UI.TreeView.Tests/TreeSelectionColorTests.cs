using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.RenderList;
using Broiler.UI.Standard;
using Broiler.UI.TreeView.Standard;

namespace Broiler.UI.TreeView.Tests;

/// <summary>
/// The selected row's text in the theme's selection text color (ADR 0029). A palette built from a system
/// highlight pair fills the selection with a color the ordinary text is not readable on.
/// </summary>
public sealed class TreeSelectionColorTests
{
    private static readonly BColor HighlightText = BColor.FromArgb(0xFF, 0x00, 0x00, 0x00);

    private static readonly StandardThemeTokens SystemPalette = StandardThemeTokens.HighContrastDark with
    {
        Accent = BColor.FromArgb(0xFF, 0x1A, 0xEB, 0xFF),
        AccentSoft = BColor.FromArgb(0xFF, 0x1A, 0xEB, 0xFF),
        SelectionText = HighlightText,
    };

    [Fact]
    public void The_Selected_Row_Of_A_Focused_Tree_Is_Drawn_In_The_Selection_Text_Color()
    {
        using TreeScene scene = TreeStandardHarness.Create(Source());
        scene.Tree.ApplyTheme(SystemPalette);
        scene.Click(scene.RowPoint(0));

        BRenderList list = scene.Render();

        Assert.Equal(HighlightText, ColorOf(list, "alpha"));
        Assert.Equal(HighlightText, ColorOf(list, "about alpha"));
        Assert.Equal(SystemPalette.Text, ColorOf(list, "beta"));
        Assert.Equal(SystemPalette.TextMuted, ColorOf(list, "about beta"));

        // Without focus the selection is the inactive surface, which the ordinary text reads on.
        scene.Session.SetFocus(null);
        list = scene.Render();
        Assert.Equal(SystemPalette.Text, ColorOf(list, "alpha"));
    }

    [Fact]
    public void A_Preset_Draws_The_Selected_Row_As_Before()
    {
        using TreeScene scene = TreeStandardHarness.Create(Source());
        scene.Tree.ApplyTheme(StandardThemeTokens.Light);
        scene.Click(scene.RowPoint(0));

        BRenderList list = scene.Render();

        Assert.Equal(StandardThemeTokens.Light.Text, ColorOf(list, "alpha"));
        Assert.Equal(StandardThemeTokens.Light.TextMuted, ColorOf(list, "about alpha"));
    }

    [Fact]
    public void A_Theme_That_Says_It_Is_High_Contrast_Gets_Glyph_Decorations()
    {
        using TreeScene scene = TreeStandardHarness.Create(Source(TreeNodeDecoration.Error));

        // Light's surface and text are not far enough apart for the luminance test.
        scene.Tree.ApplyTheme(StandardThemeTokens.Light);
        Assert.DoesNotContain(Texts(scene.Render()), text => text == "!");

        scene.Tree.ApplyTheme(StandardThemeTokens.Light with { IsHighContrast = true });
        Assert.Contains(Texts(scene.Render()), text => text == "!");
    }

    [Theory]
    [InlineData(TreeNodeDecoration.Dirty, "*")]
    [InlineData(TreeNodeDecoration.Error, "!")]
    [InlineData(TreeNodeDecoration.Warning, "?")]
    [InlineData(TreeNodeDecoration.Information, "i")]
    public void The_Decoration_Glyph_On_A_Focused_Selection_Is_Drawn_In_The_Selection_Text_Color(TreeNodeDecoration decoration, string glyph)
    {
        using TreeScene scene = TreeStandardHarness.Create(Source(decoration));
        scene.Tree.ApplyTheme(SystemPalette);
        scene.Click(scene.RowPoint(0));

        // Top to bottom: the selected row's glyph, then the unselected row's in its own color.
        BColor own = decoration switch
        {
            TreeNodeDecoration.Error => SystemPalette.Danger,
            TreeNodeDecoration.Warning => SystemPalette.Warning,
            TreeNodeDecoration.Information => SystemPalette.Info,
            _ => SystemPalette.Text,
        };
        Assert.Equal([HighlightText, own], GlyphColors(scene.Render(), glyph));

        // Without focus the selection is the inactive surface, where the glyph keeps its own color.
        scene.Session.SetFocus(null);
        Assert.Equal([own, own], GlyphColors(scene.Render(), glyph));
    }

    [Fact]
    public void The_Decoration_Shape_On_A_Focused_Selection_Is_Drawn_In_The_Selection_Text_Color()
    {
        // Not high contrast, so the decoration is a shape: a dirty row's square.
        BColor selectionText = BColor.White;
        StandardThemeTokens theme = StandardThemeTokens.Light with
        {
            AccentSoft = BColor.FromArgb(0xFF, 0x00, 0x3E, 0x92),
            SelectionText = selectionText,
        };
        using TreeScene scene = TreeStandardHarness.Create(Source(TreeNodeDecoration.Dirty));
        scene.Tree.ApplyTheme(theme);
        scene.Click(scene.RowPoint(0));

        List<BRenderCommand.FillRect> squares = scene.Render().Commands.OfType<BRenderCommand.FillRect>()
            .Where(fill => fill.Rect.Width == fill.Rect.Height && fill.Rect.Width < 20)
            .OrderBy(fill => fill.Rect.Top)
            .ToList();
        Assert.Equal([selectionText, theme.Text], squares.Select(fill => fill.Color));

        // A preset keeps the square in the text color on the selection, as before.
        scene.Tree.ApplyTheme(StandardThemeTokens.Light);
        squares = scene.Render().Commands.OfType<BRenderCommand.FillRect>()
            .Where(fill => fill.Rect.Width == fill.Rect.Height && fill.Rect.Width < 20)
            .ToList();
        Assert.All(squares, fill => Assert.Equal(StandardThemeTokens.Light.Text, fill.Color));
    }

    private static List<BColor> GlyphColors(BRenderList list, string glyph) =>
        list.Commands.OfType<BRenderCommand.DrawText>()
            .Where(command => command.Text.Text == glyph)
            .OrderBy(command => command.Origin.Y)
            .Select(command => command.Text.Color)
            .ToList();

    private static BColor ColorOf(BRenderList list, string text) =>
        Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == text).Text.Color;

    private static IEnumerable<string> Texts(BRenderList list) =>
        list.Commands.OfType<BRenderCommand.DrawText>().Select(command => command.Text.Text);

    private static DecoratedTreeSource Source(TreeNodeDecoration decoration = TreeNodeDecoration.None) =>
        new(decoration, "/alpha", "/beta");

    private sealed class DecoratedTreeSource(TreeNodeDecoration decoration, params string[] children) : ITreeDataSource
    {
        public TreeNodeId Root => new("/");

        public int GetChildCount(TreeNodeId node) => node.Value == "/" ? children.Length : 0;

        public TreeNodeId GetChild(TreeNodeId node, int index) => new(children[index]);

        public bool CanExpand(TreeNodeId node) => GetChildCount(node) > 0;

        public TreeNodePresentation GetPresentation(TreeNodeId node)
        {
            string name = node.Value[(node.Value.LastIndexOf('/') + 1)..];
            return new(node, name, $"about {name}", Decoration: decoration);
        }
    }
}
