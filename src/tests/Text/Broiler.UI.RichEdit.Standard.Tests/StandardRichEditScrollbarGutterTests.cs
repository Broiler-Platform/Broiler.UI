using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.UI.Standard;
using static Broiler.UI.RichEdit.Standard.Tests.RichEditStandardHarness;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Where the scrollbars sit against the text (ADR 0033). The translucent bars of the Light and Dark presets
/// overlay the right edge of the text column, which shows through them. Opaque bars, as a high-contrast theme
/// draws, would hide that text, so they are drawn in a strip of their own beside it and the text wraps short of
/// them.
/// </summary>
public sealed class StandardRichEditScrollbarGutterTests
{
    private const string Prose = "The quick brown fox jumps over the lazy dog and keeps on running";

    public static TheoryData<string, TextAlignment> ContrastCases => new()
    {
        { nameof(StandardThemeTokens.HighContrastLight), TextAlignment.Left },
        { nameof(StandardThemeTokens.HighContrastLight), TextAlignment.Right },
        { nameof(StandardThemeTokens.HighContrastDark), TextAlignment.Left },
        { nameof(StandardThemeTokens.HighContrastDark), TextAlignment.Right },
    };

    public static TheoryData<string> ContrastThemes => new()
    {
        nameof(StandardThemeTokens.HighContrastLight),
        nameof(StandardThemeTokens.HighContrastDark),
    };

    [Theory]
    [MemberData(nameof(ContrastCases))]
    public void In_High_Contrast_No_Text_Runs_Under_The_Vertical_Bar(string theme, TextAlignment alignment)
    {
        RichEditScene scene = Paragraphs(alignment);
        scene.Edit.ApplyTheme(Theme(theme));

        BRenderList list = scene.Session.RenderFrame();
        BRect track = VerticalTrack(list, scene.Edit);
        (BRenderCommand.DrawText Text, BRect Clip)[] lines = DrawnText(list);

        Assert.True(scene.Edit.HasVerticalScrollbar);
        Assert.Equal(255, scene.Edit.ScrollbarTrack.A);
        Assert.NotEmpty(lines);
        foreach ((BRenderCommand.DrawText text, BRect clip) in lines)
        {
            Assert.True(InkRight(text) <= track.Left + 0.01, $"'{text.Text.Text}' ends at {InkRight(text):F2}, the bar starts at {track.Left:F2}");
            Assert.True(clip.Right <= track.Left + 0.01, $"the text is clipped at {clip.Right:F2}, the bar starts at {track.Left:F2}");
        }

        // The column ends where the bar starts: a right-aligned line meets it, with the text's padding to the left.
        if (alignment == TextAlignment.Right)
            Assert.Contains(lines, line => Math.Abs(InkRight(line.Text) - track.Left) < 0.01);

        // The bar itself has not moved.
        Assert.Equal(scene.Edit.Bounds.Right - scene.Edit.PaddingX - scene.Edit.ScrollbarThickness, track.Left, 6);
        scene.Session.Dispose();
    }

    [Fact]
    public void The_Translucent_Bars_Of_The_Light_Preset_Still_Overlay_The_Edge_Of_The_Text()
    {
        RichEditScene scene = Paragraphs(TextAlignment.Right);
        scene.Edit.ApplyTheme(StandardThemeTokens.Light);

        BRenderList list = scene.Session.RenderFrame();
        BRect track = VerticalTrack(list, scene.Edit);
        double columnRight = scene.Edit.Bounds.Right - scene.Edit.PaddingX;

        Assert.True(scene.Edit.ScrollbarTrack.A < 255 && scene.Edit.ScrollbarThumb.A < 255);
        Assert.Equal(columnRight - scene.Edit.ScrollbarThickness, track.Left, 6);
        Assert.All(DrawnText(list), line => Assert.Equal(columnRight, line.Clip.Right, 6));
        Assert.Contains(DrawnText(list), line => Math.Abs(InkRight(line.Text) - columnRight) < 0.01);
        scene.Session.Dispose();
    }

    [Fact]
    public void An_Opaque_Bar_The_Application_Sets_Is_Drawn_Beside_The_Text_Too()
    {
        RichEditScene scene = Paragraphs(TextAlignment.Right);
        scene.Edit.ApplyTheme(StandardThemeTokens.Light);
        scene.Edit.ScrollbarThumb = BColor.FromArgb(0xFF, 0x33, 0x33, 0x33);

        BRenderList list = scene.Session.RenderFrame();
        BRect track = VerticalTrack(list, scene.Edit);

        Assert.All(DrawnText(list), line => Assert.True(InkRight(line.Text) <= track.Left + 0.01));
        Assert.Contains(DrawnText(list), line => Math.Abs(InkRight(line.Text) - track.Left) < 0.01);
        scene.Session.Dispose();
    }

    [Theory]
    [MemberData(nameof(ContrastThemes))]
    public void In_High_Contrast_The_Last_Line_Scrolls_Clear_Of_The_Horizontal_Bar(string theme)
    {
        RichEditScene scene = Create(new BSize(220, 120));
        scene.Edit.Wrapping = RichEditWrapping.NoWrap;
        scene.Edit.HorizontalScrollPolicy = RichEditScrollPolicy.Auto;
        scene.Edit.SetPlainText(string.Join('\n', Enumerable.Range(0, 20).Select(line => $"line {line:00} " + new string('x', 60))));
        scene.Edit.ApplyTheme(Theme(theme));
        scene.Session.RenderFrame();
        scene.Edit.ScrollToEnd();

        BRenderList list = scene.Session.RenderFrame();
        BRect vertical = VerticalTrack(list, scene.Edit);
        BRect horizontal = Assert.Single(
            list.Commands.OfType<BRenderCommand.FillRoundedRect>(),
            fill => fill.Color == scene.Edit.ScrollbarTrack && fill.Rect.Height == scene.Edit.ScrollbarThickness && fill.Rect.Width > fill.Rect.Height).Rect;
        BRenderCommand.DrawText last = Assert.Single(DrawnText(list), line => line.Text.Text.Text.StartsWith("line 19", StringComparison.Ordinal)).Text;

        Assert.True(scene.Edit.HasHorizontalScrollbar && scene.Edit.HasVerticalScrollbar);
        Assert.True(last.Origin.Y + BTextMeasurer.GetLineHeight(last.Text.Font) <= horizontal.Top + 0.01, "the last line sits above the horizontal bar");
        Assert.All(DrawnText(list), line => Assert.True(line.Clip.Bottom <= horizontal.Top + 0.01));

        // Each bar keeps to its own strip: neither runs under the other at the corner.
        Assert.True(vertical.Bottom <= horizontal.Top + 0.01);
        Assert.True(horizontal.Right <= vertical.Left + 0.01);
        scene.Session.Dispose();
    }

    [Fact]
    public void A_Box_Sized_To_Its_Text_Grows_By_The_Strip_Its_Opaque_Horizontal_Bar_Takes()
    {
        var edit = new StandardRichEdit
        {
            VerticalScrollPolicy = RichEditScrollPolicy.Never,
            HorizontalScrollPolicy = RichEditScrollPolicy.Auto,
            Wrapping = RichEditWrapping.NoWrap,
        };
        edit.SetPlainText("one\ntwo");

        edit.ApplyTheme(StandardThemeTokens.Light);
        edit.Measure(new BSize(200, double.PositiveInfinity));
        double light = edit.DesiredSize.Height;
        edit.ApplyTheme(StandardThemeTokens.HighContrastDark);
        edit.Measure(new BSize(200, double.PositiveInfinity));

        Assert.Equal(light + edit.ScrollbarThickness, edit.DesiredSize.Height, 6);
    }

    private static StandardThemeTokens Theme(string name) => name switch
    {
        nameof(StandardThemeTokens.HighContrastLight) => StandardThemeTokens.HighContrastLight,
        nameof(StandardThemeTokens.HighContrastDark) => StandardThemeTokens.HighContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    /// <summary>A 220 x 120 editor of wrapped prose, more of it than shows, every paragraph aligned the same way.</summary>
    private static RichEditScene Paragraphs(TextAlignment alignment)
    {
        RichEditScene scene = Create(new BSize(220, 120));
        scene.Edit.Document = RichTextDocument.FromParagraphs(Enumerable.Range(0, 6).Select(_ =>
            RichTextParagraph.Create(Prose, InlineStyle.Default, ParagraphStyle.Default with { Alignment = alignment })));
        return scene;
    }

    private static BRect VerticalTrack(BRenderList list, StandardRichEdit edit) =>
        Assert.Single(
            list.Commands.OfType<BRenderCommand.FillRoundedRect>(),
            fill => fill.Color == edit.ScrollbarTrack && fill.Rect.Width == edit.ScrollbarThickness && fill.Rect.Height > fill.Rect.Width).Rect;

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

        return drawn.ToArray();
    }

    /// <summary>Where a run's ink ends: its advance less any trailing space, which aligns past the column.</summary>
    private static double InkRight(BRenderCommand.DrawText text) =>
        text.Origin.X + BTextMeasurer.MeasureAdvance(text.Text.Text.TrimEnd(), text.Text.Font);
}
