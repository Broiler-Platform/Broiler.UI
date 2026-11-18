using System.Runtime.Versioning;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Rendering;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windows;
using Broiler.UI.AboutDialog.Standard;
using Broiler.UI.ListView;
using Broiler.UI.ListView.Standard;
using Broiler.UI.Menu;
using Broiler.UI.Menu.Standard;
using Broiler.UI.Standard;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Standard.Windows.Tests;

// A separate process keeps DirectWrite's global font-provider registration out of
// the platform-neutral tests. These assertions inspect native pixels, not "Hg" metrics.
[SupportedOSPlatform("windows")]
public sealed class NativeCaptionAlignmentTests
{
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Theory(Skip = "Requires DirectWrite on Windows.", SkipUnless = nameof(IsWindows))]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void About_Dialog_Captions_Are_Visually_Centered(double scale)
    {
        using var renderer = new Direct2DRenderer();
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(620, 416), scale));
        using var owner = new StandardWindow();
        session.AddRoot(owner);
        using var dialog = new StandardAboutDialog { Title = "About", ProductName = "Broiler.UI Control Gallery" };
        _ = dialog.ShowModal(owner, new BRect(0, 0, dialog.PreferredSize.Width, dialog.PreferredSize.Height));
        var commands = session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>().ToArray();
        AssertCentered(renderer, Assert.Single(commands, c => c.Text.Text == "About"), dialog.ChromeLayout.Title, scale);
        AssertCentered(renderer, Assert.Single(commands, c => c.Text.Text == "OK"), dialog.OkButton.Bounds, scale);
        var list = dialog.ComponentList;
        Assert.Equal(8 * list.EffectiveItemHeight, list.Bounds.Height, 8);
        var row = new BRect(list.ContentBounds.Left, list.ContentBounds.Top, list.ContentBounds.Width, list.EffectiveItemHeight);
        AssertCentered(renderer, commands.First(c => c.Origin.Y >= row.Top && c.Origin.Y < row.Bottom), row, scale, optical: true);
    }

    [Theory(Skip = "Requires DirectWrite on Windows.", SkipUnless = nameof(IsWindows))]
    [InlineData(1, 26)]
    [InlineData(1, 27)]
    [InlineData(1.25, 26)]
    [InlineData(1.5, 26)]
    [InlineData(1.5, 27)]
    [InlineData(2, 26)]
    public void Popup_Captions_With_And_Without_Descenders_Are_Centered(double scale, double height)
    {
        using var renderer = new Direct2DRenderer();
        using var menu = new StandardMenu { PresentationMode = UiMenuPresentationMode.ContextMenu, ItemHeight = height };
        menu.SetItems([new("commit", "Commit"), new("reset", "Reset"), new("show", "Show dialog"), new("logical", "Show logical dialog")]);
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(400, 300), scale));
        session.AddRoot(menu);
        session.RenderFrame();
        menu.OpenPopup();
        var commands = session.RenderFrame().Commands.OfType<BRenderCommand.DrawText>().ToArray();
        Assert.Equal(4, commands.Length);
        for (int i = 0; i < commands.Length; i++)
            AssertCentered(renderer, commands[i], new BRect(menu.OverlayBounds.Left, menu.OverlayBounds.Top + i * height, menu.OverlayBounds.Width, height), scale);
    }

    [Theory(Skip = "Requires DirectWrite on Windows.", SkipUnless = nameof(IsWindows))]
    [InlineData(1, 0, 24)]
    [InlineData(1.25, 0, 24)]
    [InlineData(1.5, 0, 24)]
    [InlineData(2, 0, 24)]
    [InlineData(1.25, 1.4, 24)]
    [InlineData(1.5, 1.333333333333, 24)]
    [InlineData(2, 1.25, 24)]
    [InlineData(1, 0, 28)]
    [InlineData(1.25, 0, 28)]
    [InlineData(1.5, 0, 28)]
    [InlineData(2, 0, 28)]
    public void Selected_And_Scrolled_List_Rows_Use_Device_Aligned_Baselines(double scale, double offset, double height)
    {
        using var renderer = new Direct2DRenderer();
        using var list = new StandardListView { ItemHeight = height, CornerRadius = 0 };
        string[] names = ["Broiler.Graphics", "Broiler.Graphics.Windows", "Broiler.Input", "Broiler.Input.Camera",
            "Broiler.Input.Camera.Windows", "Broiler.Input.Keyboard", "Broiler.Input.Microphone", "Broiler.Input.Microphone.Windows"];
        list.SetItems(names.Select(name => new UiListItem(name, name + " — 0.1.0-preview.9")).ToArray());
        using var session = new StandardUiSessionBuilder().Build(new Host(new BSize(620, 220), scale));
        using var container = new OffsetContainer(list);
        session.AddRoot(container);
        session.RenderFrame();
        list.SelectIndex(2);
        session.SetFocus(list);
        if (offset > 0)
            Assert.True(list.MakeVisible(new BRect(list.ContentBounds.Left, list.ContentBounds.Bottom + offset - 1, 1, 1)));
        Assert.Equal(offset, list.VerticalOffset, 8);
        var frame = session.RenderFrame();
        var commands = frame.Commands.OfType<BRenderCommand.DrawText>().ToArray();
        Assert.True(commands.Length >= 6);
        foreach (var command in commands)
        {
            double baseline = (command.Origin.Y + command.Text.Baseline!.Value) * scale;
            Assert.Equal(Math.Round(baseline), baseline, 8);
            int index = Array.FindIndex(names, name => command.Text.Text == name + " — 0.1.0-preview.9");
            Assert.True(index >= 0);
            var row = new BRect(list.ContentBounds.Left, list.ContentBounds.Top + index * list.EffectiveItemHeight - offset,
                list.ContentBounds.Width, list.EffectiveItemHeight);
            if (row.Top >= list.ContentBounds.Top && row.Bottom <= list.ContentBounds.Bottom)
                AssertCentered(renderer, command, row, scale, optical: true);
        }
    }

    private sealed class OffsetContainer : UiElement
    {
        private readonly UiElement _child;

        public OffsetContainer(UiElement child)
        {
            _child = child;
            AddChild(child);
        }

        protected override void ArrangeCore(BRect finalRect)
        {
            _child.Arrange(new BRect(4.25, 6.5, 600, 190));
        }
    }

    private static void AssertCentered(Direct2DRenderer renderer, BRenderCommand.DrawText command, BRect bounds, double scale, bool optical = false)
    {
        var list = new BRenderList();
        list.DrawText(new BTextRun(command.Text.Text, command.Text.Font, BColor.Black) { Baseline = command.Text.Baseline }, command.Origin);
        using var bitmap = renderer.RenderToImage(list,
            new BSurfaceDescriptor(new BSize(Math.Ceiling(Math.Max(bounds.Right, command.Origin.X + BTextMeasurer.MeasureAdvance(command.Text.Text, command.Text.Font))) + 16, Math.Ceiling(bounds.Bottom) + 8), scale),
            new BFrameContext(BColor.White));
        var rows = Enumerable.Range(0, bitmap.Height)
            .Where(y => Enumerable.Range(0, bitmap.Width).Any(x => bitmap.GetPixel(x, y).R < 128)).ToArray();
        Assert.NotEmpty(rows);
        double inkCenter = (rows[0] + rows[^1] + 1) / 2.0;
        if (optical)
        {
            double coverage = 0, moment = 0;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    double weight = 255 - (pixel.R + pixel.G + pixel.B) / 3.0;
                    coverage += weight;
                    moment += weight * (y + 0.5);
                }
            inkCenter = moment / coverage;
        }
        double target = (bounds.Top + bounds.Height / 2) * scale;
        Assert.True(Math.Abs(inkCenter - target) <= 1,
            $"'{command.Text.Text}' at {scale * 100}%: ink center {inkCenter}, row center {target}.");
    }

    private sealed class Host(BSize size, double scale) : IUiHost
    {
        public BSize ViewportSize => size;
        public double Scale => scale;
        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);
        public void Invalidate(UiInvalidation invalidation) { }
        public void Present(BRenderList renderList) { }
    }
}
