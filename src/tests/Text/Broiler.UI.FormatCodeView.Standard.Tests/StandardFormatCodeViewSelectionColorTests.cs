using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.FormatCodeView.Standard.Tests;

/// <summary>
/// Selected text and codes in the theme's selection text color (ADR 0029), split at the selection's edges on
/// the character grid, and the presets' token runs kept whole.
/// </summary>
public sealed class StandardFormatCodeViewSelectionColorTests
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
    public void Selected_Text_Is_Drawn_In_The_Selection_Color_Exactly_Over_The_Selection_Fill()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("hello world"));
        scene.View.ApplyTheme(SystemPalette);
        scene.View.SetSelection(2, 5);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Equal(HighlightText, scene.View.SelectionForeground);
        BRect fill = Assert.Single(list.Commands.OfType<BRenderCommand.FillRect>(), command => command.Color == Highlight).Rect;
        Assert.Equal(SystemPalette.Text, Run(list, "he").Text.Color);
        BRenderCommand.DrawText selected = Run(list, "llo");
        Assert.Equal(HighlightText, selected.Text.Color);
        Assert.Equal(fill.Left, selected.Origin.X, 6);
        Assert.Equal(SystemPalette.Text, Run(list, " world").Text.Color);
        Assert.DoesNotContain(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text.Contains("hello", StringComparison.Ordinal));

        // A disabled view still fills its selection, so its selected text keeps the selection color.
        scene.View.IsEnabled = false;
        Assert.Equal(HighlightText, Run(scene.Session.RenderFrame(), "llo").Text.Color);
    }

    [Fact]
    public void Selected_Codes_Take_The_Selection_Color_And_Keep_Their_Weight()
    {
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("x", new InlineStyle { Bold = true })]);
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeProjector.Project(document));
        scene.View.ApplyTheme(SystemPalette);
        BTextRun unselected = Run(scene.Session.RenderFrame(), "[Bold ON]").Text;
        Assert.Equal(scene.View.InlineCodeForeground, unselected.Color);

        scene.View.SetSelection(0, scene.View.Text.Length);
        BRenderList list = scene.Session.RenderFrame();

        BTextRun code = Run(list, "[Bold ON]").Text;
        Assert.Equal(HighlightText, code.Color);
        Assert.Equal(unselected.Font, code.Font);
        Assert.Equal(HighlightText, Run(list, "x").Text.Color);
    }

    [Fact]
    public void A_Preset_Draws_Each_Token_Whole_In_Its_Own_Color()
    {
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeViewStandardHarness.Project("hello world"));
        scene.View.ApplyTheme(StandardThemeTokens.Light);
        scene.View.SetSelection(2, 5);

        BRenderList list = scene.Session.RenderFrame();

        Assert.Null(scene.View.SelectionForeground);
        Assert.Equal(StandardThemeTokens.Light.Text, Run(list, "hello world").Text.Color);
    }

    private static BRenderCommand.DrawText Run(BRenderList list, string text) =>
        Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == text);
}
