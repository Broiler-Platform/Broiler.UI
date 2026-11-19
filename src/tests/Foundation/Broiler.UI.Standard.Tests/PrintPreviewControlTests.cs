using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Paging;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Resources;
using Broiler.Graphics.Text;
using Broiler.Input;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.PrintPreview;
using Broiler.UI.PrintPreview.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class PrintPreviewControlTests
{
    [Fact]
    public void The_Preview_Draws_The_Page_Scaled_Into_The_View()
    {
        var host = new ImageTestHost(new BSize(400, 600));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var preview = new StandardPrintPreview { Pages = Document(2).Paginate(BPageSetup.Default) };
        session.AddRoot(preview);

        BRenderList frame = session.RenderFrame();

        frame.Validate();
        BRenderCommand.PushTransform scale = Assert.Single(frame.Commands.OfType<BRenderCommand.PushTransform>(),
            command => command.Transform.M11 is > 0 and < 1);
        Assert.Equal(scale.Transform.M11, scale.Transform.M22, 9);
        Assert.Contains(frame.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "Page 1 body");
        Assert.Contains(frame.Commands.OfType<BRenderCommand.DrawText>(), command => command.Text.Text == "Page 1 of 2");
        Assert.True(preview.PageBounds.Width <= 400 && preview.PageBounds.Height <= 600);
        Assert.Equal(BPaperSize.A4.Width / BPaperSize.A4.Height, preview.PageBounds.Width / preview.PageBounds.Height, 3);
    }

    [Fact]
    public void The_Preview_Pages_With_Keys_And_The_Wheel_And_Stays_On_Its_Pages()
    {
        var preview = new StandardPrintPreview { Pages = Document(3).Paginate(BPageSetup.Default) };
        int changes = 0;
        preview.PageIndexChanged += (_, _) => changes++;

        Assert.True(preview.DispatchInput(KeyDown("PageDown")));
        Assert.Equal(1, preview.PageIndex);
        Assert.True(preview.DispatchInput(KeyDown("End")));
        Assert.Equal(2, preview.PageIndex);
        Assert.False(preview.NextPage());

        preview.DispatchInput(Wheel(0.5));
        Assert.Equal(2, preview.PageIndex);
        preview.DispatchInput(Wheel(0.5));
        Assert.Equal(1, preview.PageIndex);
        Assert.Equal(3, changes);

        preview.Pages = Document(1).Paginate(BPageSetup.Default);
        Assert.Equal(0, preview.PageIndex);
        Assert.Equal("Page 1 of 1", preview.PageCaption);

        preview.Pages = null;
        Assert.Equal("No pages", preview.PageCaption);
    }

    [Fact]
    public void The_Preview_Uploads_A_Page_Image_Once_And_Releases_It_With_The_Pages()
    {
        var host = new ImageTestHost(new BSize(300, 400));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var preview = new StandardPrintPreview { Pages = new BPageCollection([PageWithImage()]) };
        session.AddRoot(preview);

        session.RenderFrame();
        preview.ZoomMode = UiPrintPreviewZoom.FitWidth;
        BRenderList second = session.RenderFrame();

        Assert.Equal(1, host.Created);
        BRenderCommand.DrawImage drawn = Assert.Single(second.Commands.OfType<BRenderCommand.DrawImage>());
        Assert.Equal(host.LastCreated, drawn.Image);

        preview.Pages = new BPageCollection([PageWithImage()]);
        Assert.Equal(1, host.Released);
    }

    private static TestDocument Document(int pages, BPageSetup? preferred = null) => new(pages, preferred ?? BPageSetup.Default);

    private static BPage PageWithImage()
    {
        var builder = new BPageBuilder(new BSize(200, 200));
        BImageHandle image = builder.AddImage(new BPixelBuffer(1, 1, [255, 0, 0, 255]));
        builder.Content.DrawImage(image, new BRect(0, 0, 1, 1), new BRect(20, 20, 100, 100));
        return builder.Build();
    }

    private static UiInputEvent KeyDown(string name) =>
        UiInputEvent.FromKeyboardKey(
            new KeyboardKeyEvent(
                Header("keyboard", 2),
                KeyboardKey.FromName(name),
                KeyboardKeyTransition.Down,
                KeyboardModifierState.None,
                0,
                0,
                0,
                false,
                false,
                Source: InputEventSource.Synthetic));

    private static UiInputEvent Wheel(double notches) =>
        UiInputEvent.FromMouseWheel(
            new MouseWheelEvent(
                Header("mouse", 3),
                InputPoint.ClientDeviceIndependentPixels(40, 40),
                MouseButtons.None,
                MouseWheelAxis.Vertical,
                notches,
                InputEventSource.Synthetic));

    private static InputEventHeader Header(string id, long sequence) =>
        new(
            InputDeviceId.FromOpaqueValue(id),
            new InputTimestamp(sequence, TimeSpan.TicksPerSecond, "print-test"),
            sequence);

    private sealed class TestDocument(int pages, BPageSetup preferred) : IBPagedDocument
    {
        public string Title => "Test document";

        public BPageSetup PreferredSetup => preferred;

        public IBPageSource Paginate(BPageSetup setup, CancellationToken cancellationToken = default)
        {
            var laidOut = new List<BPage>();
            for (int i = 0; i < pages; i++)
            {
                var builder = new BPageBuilder(setup.PageSize);
                builder.Content.DrawText(new BTextRun($"Page {i + 1} body", BFontStyle.Default, BColor.Black),
                    new BPoint(setup.Margins.Left, setup.Margins.Top));
                laidOut.Add(builder.Build());
            }

            return new BPageCollection(laidOut);
        }
    }

    private sealed class ImageTestHost(BSize viewportSize) : IUiHost, IUiImageHost
    {
        public int Created { get; private set; }

        public int Released { get; private set; }

        public BImageHandle LastCreated { get; private set; }

        public BSize ViewportSize { get; } = viewportSize;

        public double Scale => 1.0;

        public BRenderList CreateRenderList(int capacity = 0) => new(capacity);

        public void Invalidate(UiInvalidation invalidation)
        {
        }

        public void Present(BRenderList renderList)
        {
        }

        public BImageHandle CreateImage(ReadOnlySpan<byte> encodedImage) => BImageHandle.Invalid;

        public BImageHandle CreateImage(BPixelBuffer pixels)
        {
            Created++;
            LastCreated = BImageHandle.FromId(5000 + (ulong)Created, new BSize(pixels.Width, pixels.Height));
            return LastCreated;
        }

        public void ReleaseImage(BImageHandle image) => Released++;
    }
}
