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
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class VerticalTextArrangementTests
{
    [Theory]
    [InlineData(1, 26)]
    [InlineData(1, 27)]
    [InlineData(1.25, 26)]
    [InlineData(1.5, 27)]
    [InlineData(2, 26)]
    public void Default_Button_And_Menu_Baselines_Align_To_Device_Pixels(double scale, double height)
    {
        using var button = new StandardButton { Text = "Hg" };
        using var buttonSession = new StandardUiSessionBuilder().Build(new Host(new BSize(220, height), scale));
        buttonSession.AddRoot(button);
        AssertPixelAligned(Assert.Single(buttonSession.RenderFrame().Commands.OfType<BRenderCommand.DrawText>()), button.Bounds, scale);

        using var menu = new StandardMenu();
        menu.SetItems([new UiMenuItem("item", "Hg")]);
        using var menuSession = new StandardUiSessionBuilder().Build(new Host(new BSize(220, height), scale));
        menuSession.AddRoot(menu);
        AssertPixelAligned(Assert.Single(menuSession.RenderFrame().Commands.OfType<BRenderCommand.DrawText>()), menu.TopLevelBounds[0], scale);
    }

    private static void AssertPixelAligned(BRenderCommand.DrawText command, BRect bounds, double scale)
    {
        double baseline = command.Origin.Y + command.Text.Baseline!.Value;
        Assert.Equal(Math.Round(baseline * scale), baseline * scale, 8);
        AssertCentered(command, bounds);
    }

    [Theory]
    [InlineData(1, 26)]
    [InlineData(1, 27)]
    [InlineData(1.25, 26)]
    [InlineData(1.5, 27)]
    [InlineData(2, 26)]
    public void Popup_Rows_Keep_Their_Text_Centered_After_Device_Rounding(double scale, double height)
    {
        using var menu = new StandardMenu { PresentationMode = UiMenuPresentationMode.ContextMenu, ItemHeight = height };
        menu.SetItems([new UiMenuItem("first", "Hg"), new UiMenuItem("second", "Hg"), new UiMenuItem("third", "Hg")]);
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(400, 300), scale));
        session.AddRoot(menu);
        session.RenderFrame();
        menu.OpenPopup();
        var commands = session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>().ToArray();
        Assert.Equal(3, commands.Length);
        for (int i = 0; i < commands.Length; i++)
            AssertPixelAligned(commands[i], new BRect(menu.OverlayBounds.Left, menu.OverlayBounds.Top + i * height, menu.OverlayBounds.Width, height), scale);
    }

    [Theory]
    [InlineData(12, 30)]
    [InlineData(24, 60)]
    public void Buttons_Center_Their_Caption_Ink_When_The_Caption_Changes(double size, double height)
    {
        using var button = new StandardButton { Text = "OK", Font = new BFontStyle("Arial", size) };
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(220, height)));
        session.AddRoot(button);
        var first = Assert.Single(session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>());
        AssertCentered(first, button.Bounds);
        button.Text = "gy";
        var second = Assert.Single(session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>());
        AssertCentered(second, button.Bounds);
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
        if (lines == 1)
        {
            AssertCentered(commands[0], label.Bounds);
            return;
        }
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
        Assert.Equal(50, list.EffectiveItemHeight);
        for (int i = 0; i < commands.Length; i++)
            AssertCentered(commands[i], new BRect(list.ContentBounds.Left,
                list.ContentBounds.Top + i * list.EffectiveItemHeight - list.VerticalOffset,
                list.ContentBounds.Width, list.EffectiveItemHeight), optical: true);
    }

    [Theory]
    [InlineData(12, 26)]
    [InlineData(13, 26)]
    [InlineData(14, 26)]
    [InlineData(14, 27)]
    [InlineData(14, 30)]
    [InlineData(14.5, 31)]
    [InlineData(24, 80)]
    public void Rendered_Label_Ink_Is_Centered_In_Pixels(double size, int height)
    {
        using var label = new StandardLabel { Text = "Hg", Font = new BFontStyle("Arial", size), Foreground = BColor.Black };
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(200, height)));
        session.AddRoot(label);
        using var renderer = new BImageRenderer();
        using var bitmap = renderer.RenderToImage(session.RenderFrame(), BSurfaceDescriptor.Default(new BSize(200, height)), new BFrameContext(BColor.White));
        var rows = new List<int>();
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).R < 128)
                {
                    rows.Add(y);
                    break;
                }
        Assert.NotEmpty(rows);
        Assert.InRange((rows[0] + rows[^1] + 1) / 2.0, height / 2.0 - 0.5, height / 2.0 + 0.5);
    }

    private static void AssertCentered(BRenderCommand.DrawText command, BRect bounds, bool optical = false)
    {
        Assert.NotNull(command.Text.Baseline);
        // Check the caption that is actually drawn, not the unrelated reference string "Hg".
        var list = new BRenderList();
        list.DrawText(new BTextRun(command.Text.Text, command.Text.Font, BColor.Black) { Baseline = command.Text.Baseline },
            new BPoint(8, command.Origin.Y - bounds.Top + 4));
        using var renderer = new BImageRenderer();
        using var bitmap = renderer.RenderToImage(list,
            BSurfaceDescriptor.Default(new BSize(Math.Ceiling(BTextMeasurer.MeasureAdvance(command.Text.Text, command.Text.Font)) + 16, Math.Ceiling(bounds.Height) + 8)),
            new BFrameContext(BColor.White));
        var rows = Enumerable.Range(0, bitmap.Height)
            .Where(y => Enumerable.Range(0, bitmap.Width).Any(x => bitmap.GetPixel(x, y).R < 128)).ToArray();
        Assert.NotEmpty(rows);
        double center = (rows[0] + rows[^1] + 1) / 2.0;
        if (optical)
        {
            double coverage = 0, moment = 0;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    double weight = 255 - bitmap.GetPixel(x, y).R;
                    coverage += weight;
                    moment += weight * (y + 0.5);
                }
            center = moment / coverage;
        }
        Assert.InRange(Math.Abs(center - (4 + bounds.Height / 2)), 0, 1);
        Assert.True(rows[0] >= 4 && rows[^1] < 4 + bounds.Height);
    }

    private sealed class Host(BSize size, double scale = 1) : IUiHost
    {
        public BSize ViewportSize => size;
        public double Scale => scale;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
