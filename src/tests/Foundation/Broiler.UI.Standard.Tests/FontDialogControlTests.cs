using Broiler.Graphics;
using Broiler.UI.FontDialog;
using Broiler.UI.FontDialog.Standard;
using Broiler.UI.Standard;
using Broiler.UI.ToggleButton;

namespace Broiler.UI.Standard.Tests;

public sealed class FontDialogControlTests
{
    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Updates_Selected_Font_From_Controls()
    {
        var dialog = new StandardFontDialog();
        dialog.SetFontFamilies(["Alpha Sans", "Beta Serif"]);

        dialog.FamilyList.SelectItem("Beta Serif");
        dialog.SizeSpin.Value = 18;
        dialog.WeightCombo.SelectIndex(5);
        dialog.ItalicToggle.ToggleState = UiToggleState.On;

        Assert.Equal("Beta Serif", dialog.SelectedFont.FamilyName);
        Assert.Equal(18, dialog.SelectedFont.SizeInPixels);
        Assert.Equal(BFontWeight.Bold, dialog.SelectedFont.Weight);
        Assert.Equal(BFontSlant.Italic, dialog.SelectedFont.Slant);
    }

    /// <summary>
    /// The size box steps rather than being retyped, which is the whole point of it being a spin
    /// box: the arrows and Up/Down move the size without the user selecting the old one first.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Steps_The_Size()
    {
        var dialog = new StandardFontDialog { SelectedFont = new BFontStyle("Alpha Sans", 16) };

        dialog.SizeSpin.StepUp();
        dialog.SizeSpin.StepUp();

        Assert.Equal(18, dialog.SelectedFont.SizeInPixels);

        dialog.SizeSpin.PageDown();

        Assert.Equal(8, dialog.SelectedFont.SizeInPixels);
    }

    /// <summary>
    /// A size kept in half points survives the round trip through the box. The box formats without
    /// trailing zeros, so 10.5 reads as "10.5" and 16 as "16".
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Keeps_A_Half_Point_Size()
    {
        var dialog = new StandardFontDialog { SelectedFont = new BFontStyle("Alpha Sans", 10.5) };

        Assert.Equal(10.5, dialog.SizeSpin.Value);
        Assert.Equal("10.5", dialog.SizeSpin.Edit.Text);
    }

    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Shows_The_Weight_As_A_Named_Choice()
    {
        var dialog = new StandardFontDialog { SelectedFont = new BFontStyle("Alpha Sans", 16, BFontWeight.SemiBold) };

        Assert.Equal("Semi-bold", dialog.WeightCombo.SelectedItem?.Text);

        // A weight between two the box offers — one a document can carry — shows as the nearest.
        dialog.SelectedFont = dialog.SelectedFont with { Weight = (BFontWeight)820 };

        Assert.Equal("Black", dialog.WeightCombo.SelectedItem?.Text);
    }

    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Carries_Underline_And_Strikethrough()
    {
        var dialog = new StandardFontDialog();

        dialog.UnderlineToggle.ToggleState = UiToggleState.On;
        dialog.StrikethroughToggle.ToggleState = UiToggleState.On;

        Assert.True(dialog.Underline);
        Assert.True(dialog.Strikethrough);

        dialog.Underline = false;

        Assert.Equal(UiToggleState.Off, dialog.UnderlineToggle.ToggleState);
    }

    /// <summary>
    /// A font list is as long as the host's font set and a preview is worth the room, so this
    /// dialog opts out of <see cref="UiDialog"/>'s fixed-size default.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Can_Be_Resized()
    {
        var dialog = new StandardFontDialog();

        Assert.True(dialog.CanResize);
    }

    /// <summary>
    /// Every control has to stay inside the dialog at any size the user drags it to, including one
    /// well under what the layout was designed against.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Lays_Out_At_Any_Size()
    {
        var dialog = new StandardFontDialog();

        foreach (BSize size in new[] { new BSize(360, 240), new BSize(560, 384), new BSize(1200, 900) })
        {
            BRect bounds = new(0, 0, size.Width, size.Height);
            dialog.Measure(size);
            dialog.Arrange(bounds);

            foreach (UiElement child in dialog.Children)
            {
                Assert.True(child.Bounds.Width >= 0 && child.Bounds.Height >= 0, $"{child.GetType().Name} has a negative extent at {size.Width}x{size.Height}");
                Assert.True(
                    child.Bounds.Left >= bounds.Left - 0.5 && child.Bounds.Right <= bounds.Right + 0.5,
                    $"{child.GetType().Name} escapes the dialog at {size.Width}x{size.Height}");
            }
        }
    }

    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Renders_Selected_Font_Preview()
    {
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardFontDialog
        {
            SelectedFont = new BFontStyle("Consolas", 22, BFontWeight.Bold, BFontSlant.Italic),
            SampleText = "Preview text",
        };

        session.AddRoot(dialog);
        BRenderList renderList = session.RenderFrame();

        renderList.Validate();
        Assert.Contains(
            renderList.Commands.OfType<BRenderCommand.DrawText>(),
            command =>
                command.Text.Text == "Preview text" &&
                command.Text.Font.FamilyName == "Consolas" &&
                command.Text.Font.SizeInPixels == 22 &&
                command.Text.Font.Weight == BFontWeight.Bold &&
                command.Text.Font.Slant == BFontSlant.Italic);
    }

    /// <summary>
    /// The preview draws the decorations too. Without this it would show four of the six things the
    /// dialog decides, and the two it left out are the two a user turns on to see what they look
    /// like.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Draws_The_Decorations_In_The_Preview()
    {
        var host = new TestHost();
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardFontDialog
        {
            SelectedFont = new BFontStyle("Consolas", 22),
            SampleText = "Preview text",
        };
        session.AddRoot(dialog);

        int plain = CountRules(session.RenderFrame(), dialog);

        dialog.Underline = true;
        dialog.Strikethrough = true;

        Assert.Equal(plain + 2, CountRules(session.RenderFrame(), dialog));
    }

    [Fact(Timeout = 600000)]
    public void Standard_Font_Dialog_Adds_Selected_Custom_Family_To_List()
    {
        var dialog = new StandardFontDialog();
        dialog.SetFontFamilies(["Alpha Sans"]);

        dialog.SelectedFont = new BFontStyle("Custom Face", 16);

        Assert.Contains(dialog.FamilyList.Items, item => item.Text == "Custom Face");
        Assert.Equal("Custom Face", dialog.FamilyList.SelectedItemId);
    }

    /// <summary>
    /// The list a dialog opens with comes from the host rather than from a fixed set of names. With
    /// no host font source registered the built-in names stand in, which is what this asserts —
    /// a test box's own font set is not something to assert against.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Font_Dialog_Lists_The_Host_Font_Families()
    {
        BSystemFonts.Use(() => ["Zeta Display", "Alpha Sans"]);
        try
        {
            var dialog = new StandardFontDialog();

            Assert.Contains("Zeta Display", dialog.FontFamilies);
            Assert.Contains("Alpha Sans", dialog.FontFamilies);

            // The generic families are not installed anywhere, and are what BFontStyle.Default
            // names, so the picker keeps them at the head of the list.
            Assert.Equal("sans-serif", dialog.FontFamilies[0]);
        }
        finally
        {
            BSystemFonts.Clear();
        }
    }

    [Fact(Timeout = 600000)]
    public void Font_Dialog_Result_Value_Round_Trips_Fonts()
    {
        var font = new BFontStyle("Family|With\\Escapes", 13.5, BFontWeight.SemiBold, BFontSlant.Oblique);

        string value = UiFontDialog.FormatFontValue(font, underline: true, strikethrough: false);
        bool parsed = UiFontDialog.TryParseFontValue(value, out BFontStyle result, out bool underline, out bool strikethrough);

        Assert.True(parsed);
        Assert.Equal(font, result);
        Assert.True(underline);
        Assert.False(strikethrough);
    }

    /// <summary>
    /// The decorations were appended to the result value rather than woven into it, so a value
    /// written before they existed still parses — as a font with neither.
    /// </summary>
    [Fact(Timeout = 600000)]
    public void Font_Dialog_Reads_A_Result_Value_Without_Decorations()
    {
        bool parsed = UiFontDialog.TryParseFontValue(
            "Georgia|18|700|Italic",
            out BFontStyle font,
            out bool underline,
            out bool strikethrough);

        Assert.True(parsed);
        Assert.Equal(new BFontStyle("Georgia", 18, BFontWeight.Bold, BFontSlant.Italic), font);
        Assert.False(underline);
        Assert.False(strikethrough);
    }

    /// <summary>
    /// The rules the preview draws are thin filled rectangles the width of the sample. Counting
    /// them is how a decoration is told from the preview's own frame and background.
    /// </summary>
    private static int CountRules(BRenderList renderList, StandardFontDialog dialog) =>
        renderList.Commands
            .OfType<BRenderCommand.FillRect>()
            .Count(command => command.Rect.Height <= 3 && command.Rect.Width > 20);

    private sealed class TestHost : IUiHost
    {
        public BSize ViewportSize { get; } = new(640, 360);

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }
    }
}
