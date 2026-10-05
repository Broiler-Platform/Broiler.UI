using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;

namespace Broiler.UI.FormatCodeView.Standard.Tests;

/// <summary>
/// Where the scrollbars sit against the text (ADR 0033). The bars run along the very edge of the box, inside the
/// text's padding, so a translucent bar of the Light and Dark presets overlays the last few DIP of the text. An
/// opaque bar, as a high-contrast theme draws, would hide them, so the text stops where the bar starts.
/// </summary>
public sealed class StandardFormatCodeViewScrollbarRoomTests
{
    public static TheoryData<StandardThemeTokens> ContrastThemes => new()
    {
        StandardThemeTokens.HighContrastLight,
        StandardThemeTokens.HighContrastDark,
    };

    [Theory]
    [MemberData(nameof(ContrastThemes))]
    public void In_High_Contrast_The_Text_Stops_Where_The_Bars_Start(StandardThemeTokens theme)
    {
        using FormatCodeViewScene scene = Scene(FormatCodeViewWrapping.NoWrap);
        scene.View.ApplyTheme(theme);

        BRenderList list = scene.Session.RenderFrame();
        (BRect vertical, BRect horizontal) = Tracks(list, scene.View);

        Assert.True(scene.View.HasVerticalScrollbar && scene.View.HasHorizontalScrollbar);
        Assert.All(DrawnText(list), line =>
        {
            Assert.True(line.Clip.Right <= vertical.Left + 0.01, $"the text is clipped at {line.Clip.Right:F2}, the bar starts at {vertical.Left:F2}");
            Assert.True(line.Clip.Bottom <= horizontal.Top + 0.01, $"the text is clipped at {line.Clip.Bottom:F2}, the bar starts at {horizontal.Top:F2}");
        });
    }

    [Theory]
    [MemberData(nameof(ContrastThemes))]
    public void In_High_Contrast_Wrapped_Text_Wraps_Short_Of_The_Vertical_Bar(StandardThemeTokens theme)
    {
        // Lines of the letter a column is measured by, in a view as wide as the padding and 24 columns, so a line
        // wrapped to the padded width would end 4 DIP inside the bar along the edge.
        double column = BTextMeasurer.MeasureAdvance("M", new StandardFormatCodeView().Font);
        using FormatCodeViewScene scene = Scene(FormatCodeViewWrapping.Wrap, 16 + (24 * column), 'M');
        scene.View.ApplyTheme(theme);

        BRenderList list = scene.Session.RenderFrame();
        (BRect vertical, _) = Tracks(list, scene.View);

        Assert.True(scene.View.HasVerticalScrollbar);
        Assert.True(vertical.Left < scene.View.Bounds.Right - scene.View.PaddingX, "the bar reaches into the padded width");
        Assert.All(DrawnText(list), line =>
            Assert.True(InkRight(line.Text) <= vertical.Left + 0.01, $"'{line.Text.Text.Text}' ends at {InkRight(line.Text):F2}, the bar starts at {vertical.Left:F2}"));
    }

    [Fact]
    public void The_Light_Preset_Keeps_The_Text_Inside_Its_Padding_As_Before()
    {
        using FormatCodeViewScene scene = Scene(FormatCodeViewWrapping.NoWrap);
        scene.View.ApplyTheme(StandardThemeTokens.Light);

        BRenderList list = scene.Session.RenderFrame();
        BRect bounds = scene.View.Bounds;

        Assert.True(scene.View.ScrollbarTrack.A < 255 && scene.View.ScrollbarThumb.A < 255);
        Assert.All(DrawnText(list), line =>
        {
            Assert.Equal(bounds.Right - scene.View.PaddingX, line.Clip.Right, 6);
            Assert.Equal(bounds.Bottom - scene.View.PaddingY, line.Clip.Bottom, 6);
        });
    }

    [Fact]
    public void An_Opaque_Bar_The_Application_Sets_Keeps_Clear_Of_The_Text_Too()
    {
        using FormatCodeViewScene scene = Scene(FormatCodeViewWrapping.NoWrap);
        scene.View.ApplyTheme(StandardThemeTokens.Light);
        scene.View.ScrollbarTrack = BColor.FromArgb(0xFF, 0xF0, 0xF0, 0xF0);

        BRenderList list = scene.Session.RenderFrame();
        (BRect vertical, BRect horizontal) = Tracks(list, scene.View);

        Assert.All(DrawnText(list), line =>
            Assert.True(line.Clip.Right <= vertical.Left + 0.01 && line.Clip.Bottom <= horizontal.Top + 0.01));
    }

    /// <summary>A view 120 DIP tall, and 220 wide unless given, of more long lines of a letter than it shows.</summary>
    private static FormatCodeViewScene Scene(FormatCodeViewWrapping wrapping, double width = 220, char letter = 'x')
    {
        string text = string.Join('\n', Enumerable.Range(0, 30).Select(line => $"line {line:00} " + new string(letter, 40)));
        FormatCodeViewScene scene = FormatCodeViewStandardHarness.Create(new BSize(width, 120), FormatCodeViewStandardHarness.Project(text));
        scene.View.Wrapping = wrapping;
        return scene;
    }

    /// <summary>The vertical and horizontal tracks; the horizontal one is empty when it does not show, as wrapped.</summary>
    private static (BRect Vertical, BRect Horizontal) Tracks(BRenderList list, StandardFormatCodeView view)
    {
        BRenderCommand.FillRoundedRect[] tracks = list.Commands.OfType<BRenderCommand.FillRoundedRect>()
            .Where(fill => fill.Color == view.ScrollbarTrack)
            .ToArray();
        BRect vertical = Assert.Single(tracks, fill => fill.Rect.Width == view.ScrollbarThickness && fill.Rect.Height > fill.Rect.Width).Rect;
        BRenderCommand.FillRoundedRect[] horizontal = tracks
            .Where(fill => fill.Rect.Height == view.ScrollbarThickness && fill.Rect.Width > fill.Rect.Height)
            .ToArray();
        return (vertical, horizontal.Length == 1 ? horizontal[0].Rect : BRect.Empty);
    }

    /// <summary>Every run of visible text drawn, with the clip it was drawn in.</summary>
    private static (BRenderCommand.DrawText Text, BRect Clip)[] DrawnText(BRenderList list)
    {
        var clips = new Stack<BRect>();
        var drawn = new List<(BRenderCommand.DrawText, BRect)>();
        foreach (BRenderCommand command in list.Commands)
        {
            if (command is BRenderCommand.PushClip push)
                clips.Push(push.Rect);
            else if (command is BRenderCommand.PopClip)
                clips.Pop();
            else if (command is BRenderCommand.DrawText text && !string.IsNullOrWhiteSpace(text.Text.Text) && clips.Count > 0)
                drawn.Add((text, clips.Peek()));
        }

        Assert.NotEmpty(drawn);
        return drawn.ToArray();
    }

    private static double InkRight(BRenderCommand.DrawText text) =>
        text.Origin.X + BTextMeasurer.MeasureAdvance(text.Text.Text.TrimEnd(), text.Text.Font);
}
