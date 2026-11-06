using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Text;
using Broiler.Graphics.Rendering;
using Broiler.UI.AboutDialog.Standard;
using Broiler.UI.Button.Standard;
using Broiler.UI.Dialog.Standard;
using Broiler.UI.FileDialog.Standard;
using Broiler.UI.FontDialog.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Label;
using Broiler.UI.PrintDialog.Standard;
using Broiler.UI.ScanDialog.Standard;
using Broiler.UI.Window.Standard;
using Broiler.UI.ListView.Standard;
using Broiler.UI.ListView;

namespace Broiler.UI.Standard.Tests;

public sealed class VerticalTextArrangementTests
{
    [Theory]
    [InlineData(12, 30)]
    [InlineData(24, 60)]
    public void Buttons_Center_Reference_Ink_And_Keep_A_Stable_Baseline(double size, double height)
    {
        using var button = new StandardButton { Text = "OK", Font = new BFontStyle("Arial", size) };
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(220, height)));
        session.AddRoot(button);
        var first = Assert.Single(session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>());
        AssertCentered(first, button.Bounds);
        button.Text = "gy";
        var second = Assert.Single(session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>());
        Assert.Equal(first.Origin.Y + first.Text.Baseline, second.Origin.Y + second.Text.Baseline);
    }

    [Theory]
    [InlineData("One line", 1)]
    [InlineData("First\nSecond", 2)]
    public void Labels_Center_The_Whole_Text_Block(string text, int lines)
    {
        using var label = new StandardLabel { Text = text, Wrapping = UiTextWrapping.Wrap };
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(300, 100)));
        session.AddRoot(label);
        var commands = session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>().ToArray();
        Assert.Equal(lines, commands.Length);
        var metrics = BTextMeasurer.GetVerticalMetrics(label.Font);
        double top = commands[0].Origin.Y + commands[0].Text.Baseline!.Value - metrics.Ascent;
        double bottom = commands[^1].Origin.Y + commands[^1].Text.Baseline!.Value + metrics.Descent;
        Assert.Equal(label.Bounds.Top + label.Bounds.Height / 2, (top + bottom) / 2, 5);
    }

    [Theory]
    [InlineData("About", 30)]
    [InlineData("Dialog", 30)]
    [InlineData("Dialog", 50)]
    [InlineData("File", 34)]
    [InlineData("File", 50)]
    [InlineData("Font", 30)]
    [InlineData("Font", 50)]
    [InlineData("Print", 30)]
    [InlineData("Print", 50)]
    [InlineData("Scan", 30)]
    [InlineData("Scan", 50)]
    public void Every_Dialog_Title_Is_Centered_In_Its_Title_Bar(string kind, double height)
    {
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(900, 700)));
        var owner = new StandardWindow();
        session.AddRoot(owner);
        const string title = "Title with gy";
        UiElement dialog;
        BRect titleBounds;
        var bounds = new BRect(20, 25, 800, 600);
        switch (kind)
        {
            case "About":
                var about = new StandardAboutDialog { Title = title };
                _ = about.ShowModal(owner, bounds);
                dialog = about;
                session.RenderFrame();
                titleBounds = about.ChromeLayout.Title;
                break;
            case "Dialog":
                var standard = new StandardDialog { Title = title, TitleBarHeight = height };
                _ = standard.ShowModal(owner, bounds);
                dialog = standard;
                session.RenderFrame();
                titleBounds = standard.ChromeLayout.Title;
                break;
            case "File":
                var file = new StandardFileDialog { Title = title, TitleBarHeight = height };
                _ = file.ShowModal(owner, bounds);
                dialog = file;
                titleBounds = new(bounds.Left, bounds.Top, bounds.Width, height);
                break;
            case "Font":
                var font = new StandardFontDialog { Title = title, TitleBarHeight = height };
                _ = font.ShowModal(owner, bounds);
                dialog = font;
                titleBounds = new(bounds.Left, bounds.Top, bounds.Width, height);
                break;
            case "Scan":
                var scan = new StandardScanDialog { Title = title, TitleBarHeight = height };
                _ = scan.ShowModal(owner, bounds);
                dialog = scan;
                titleBounds = new(bounds.Left, bounds.Top, bounds.Width, height);
                break;
            default:
                var print = new StandardPrintDialog { Title = title, TitleBarHeight = height };
                _ = print.ShowModal(owner, bounds);
                dialog = print;
                titleBounds = new(bounds.Left, bounds.Top, bounds.Width, height);
                break;
        }
        var command = Assert.Single(session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>(), c => c.Text.Text == title);
        AssertCentered(command, titleBounds);
        dialog.Dispose();
    }

    [Theory]
    [InlineData(12)]
    [InlineData(24)]
    public void List_Text_Is_Centered_In_Each_Row(double size)
    {
        using var list = new StandardListView { Font = new BFontStyle("Arial", size), ItemHeight = 50 };
        list.SetItems([new UiListItem("first", "First"), new UiListItem("second", "Second")]);
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(400, 180)));
        session.AddRoot(list);
        var commands = session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>().ToArray();
        Assert.Equal(2, commands.Length);
        Assert.Equal(50, commands[1].Origin.Y - commands[0].Origin.Y, 5);
        for (int i = 0; i < commands.Length; i++)
            AssertCentered(commands[i], new BRect(list.ContentBounds.Left,
                list.ContentBounds.Top + i * list.EffectiveItemHeight - list.VerticalOffset,
                list.ContentBounds.Width, list.EffectiveItemHeight));
    }

    [Fact]
    public void Rendered_Label_Ink_Is_Centered_In_Pixels()
    {
        using var label = new StandardLabel { Text = "Hg", Font = new BFontStyle("Arial", 24), Foreground = BColor.Black };
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(200, 80)));
        session.AddRoot(label);
        using var renderer = new BImageRenderer();
        using var bitmap = renderer.RenderToImage(session.RenderFrame(), BSurfaceDescriptor.Default(new BSize(200, 80)), new BFrameContext(BColor.White));
        var rows = new List<int>();
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).R < 128)
                {
                    rows.Add(y);
                    break;
                }
        Assert.NotEmpty(rows);
        Assert.InRange((rows[0] + rows[^1] + 1) / 2.0, 38, 42);
    }

    private static void AssertCentered(BRenderCommand.DrawText command, BRect bounds)
    {
        Assert.NotNull(command.Text.Baseline);
        var metrics = BTextMeasurer.GetVerticalMetrics(command.Text.Font);
        double baseline = command.Origin.Y + command.Text.Baseline.Value;
        double top = baseline - metrics.Ascent;
        double bottom = baseline + metrics.Descent;
        Assert.Equal(bounds.Top + bounds.Height / 2, (top + bottom) / 2, 5);
        Assert.True(top >= bounds.Top && bottom <= bounds.Bottom);
    }

    private sealed class Host(BSize size) : IUiHost
    {
        public BSize ViewportSize => size;
        public double Scale => 1;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
