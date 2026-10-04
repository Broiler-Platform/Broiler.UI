using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.UI.Standard;

namespace Broiler.UI.FormatCodeView.Standard.Tests;

/// <summary>Inline codes are accent text on the pane, in a color that reads there in every preset (ADR 0031).</summary>
public sealed class StandardFormatCodeViewAccentTextTests
{
    public static TheoryData<StandardThemeTokens> Presets() => new()
    {
        StandardThemeTokens.Light,
        StandardThemeTokens.Dark,
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
    };

    [Theory]
    [MemberData(nameof(Presets))]
    public void Inline_Codes_Are_Drawn_In_The_Accent_Text_And_Read_On_The_Pane(StandardThemeTokens theme)
    {
        RichTextDocument document = RichTextDocument.FromParagraphs(
            [RichTextParagraph.Create("x", new InlineStyle { Bold = true })]);
        using FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(
            new BSize(320, 100),
            FormatCodeProjector.Project(document));
        scene.View.ApplyTheme(theme);

        BRenderList list = scene.Session.RenderFrame();

        BRenderCommand.DrawText code = Assert.Single(list.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "[Bold ON]");
        Assert.Equal(theme.AccentText, code.Text.Color);
        Assert.Equal(theme.SurfaceAlt, scene.View.Background);
        double ratio = StandardContrast.Ratio(code.Text.Color, scene.View.Background);
        Assert.True(ratio >= StandardContrast.AaNormalText, $"{theme.Name}: inline code at {ratio:0.00}:1.");
    }
}
