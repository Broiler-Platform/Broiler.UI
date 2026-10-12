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
using Broiler.UI.Dialog;
using Broiler.UI.PrintDialog;
using Broiler.UI.PrintDialog.Standard;
using Broiler.UI.PrintPreview;
using Broiler.UI.PrintPreview.Standard;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The print preview and the print dialog: what the preview draws and how it pages, and how the
/// dialog keeps its choices within what the printer can do and the preview within the choices.
/// </summary>
public sealed class PrintControlTests
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

    [Fact]
    public void The_Dialog_Starts_On_The_Default_Printer_And_The_Documents_Own_Page()
    {
        var printer = new FakePrintHost();
        var document = Document(4, new BPageSetup(BPaperSize.Letter, BPageOrientation.Landscape, BPageMargins.Uniform(36)));
        var dialog = new StandardPrintDialog { PrintHost = printer, Document = document };

        Assert.Equal("Office", dialog.Settings.PrinterName);
        Assert.Equal("Office (default)", dialog.PrinterCombo.SelectedItem?.Text);
        Assert.Equal(BPaperSize.Letter, dialog.Settings.PageSetup.Paper);
        Assert.Equal("Landscape", dialog.OrientationCombo.SelectedItem?.Text);
        Assert.Equal(4, dialog.Pages!.PageCount);
        Assert.Equal(new BSize(792, 612), dialog.Preview.Pages!.GetPage(0).Size);
        Assert.True(dialog.CanPrint);
        Assert.True(dialog.PrintButton.IsEnabled);
    }

    [Fact]
    public void The_Dialog_Offers_Only_What_The_Printer_Can_Do()
    {
        var printer = new FakePrintHost();
        var dialog = new StandardPrintDialog { PrintHost = printer, Document = Document(1) };
        dialog.Settings = dialog.Settings with { ColorMode = UiPrintColorMode.Color, Duplex = UiPrintDuplex.LongEdge, Copies = 50 };

        Assert.Equal(["Colour", "Black and white"], dialog.ColorCombo.Items.Select(item => item.Text));
        Assert.Equal(3, dialog.DuplexCombo.Items.Count);
        Assert.Equal(UiPrintDuplex.LongEdge, dialog.Settings.Duplex);

        dialog.PrinterCombo.SelectIndex(dialog.PrinterCombo.Items.ToList().FindIndex(item => item.Id == "Basement"));

        Assert.Equal("Basement", dialog.Settings.PrinterName);
        Assert.Equal(UiPrintColorMode.Monochrome, dialog.Settings.ColorMode);
        Assert.Equal(UiPrintDuplex.OneSided, dialog.Settings.Duplex);
        Assert.Equal(10, dialog.Settings.Copies);
        Assert.Equal(["Black and white"], dialog.ColorCombo.Items.Select(item => item.Text));
        Assert.Single(dialog.DuplexCombo.Items);
        Assert.Equal(10, dialog.CopiesSpin.Maximum);
    }

    [Fact]
    public void Choosing_A_Paper_Lays_The_Document_Out_Again()
    {
        var document = Document(2);
        var dialog = new StandardPrintDialog { PrintHost = new FakePrintHost(), Document = document };
        int before = document.Paginations;

        dialog.PaperCombo.SelectIndex(dialog.PaperCombo.Items.ToList().FindIndex(item => item.Id == "A5"));

        Assert.Equal(before + 1, document.Paginations);
        Assert.Equal(BPaperSize.A5.Size, dialog.Preview.Pages!.GetPage(0).Size);

        dialog.CopiesSpin.Value = 3;
        Assert.Equal(before + 1, document.Paginations);
        Assert.Equal(3, dialog.Settings.Copies);
    }

    [Fact]
    public void A_Page_Range_The_Dialog_Cannot_Read_Stops_It_Printing()
    {
        var dialog = new StandardPrintDialog { PrintHost = new FakePrintHost(), Document = Document(5) };

        dialog.PagesEdit.Text = "2-";
        Assert.Equal(4, dialog.SelectedPageCount);
        Assert.True(dialog.PrintButton.IsEnabled);

        dialog.PagesEdit.Text = "4-2";
        Assert.False(dialog.PrintButton.IsEnabled);
        Assert.NotNull(dialog.DisplayedProblem);

        dialog.PagesEdit.Text = "9";
        Assert.Equal(0, dialog.SelectedPageCount);
        Assert.False(dialog.CanPrint);
        Assert.False(dialog.AcceptPrint());
    }

    [Fact]
    public void The_Job_Prints_Exactly_The_Pages_That_Were_Previewed()
    {
        var document = Document(3);
        var dialog = new StandardPrintDialog { PrintHost = new FakePrintHost(), Document = document };
        dialog.PagesEdit.Text = "1, 3";

        UiPrintJob job = dialog.CreateJob();

        Assert.Same(dialog.Pages, job.Pages);
        Assert.Equal([0, 2], job.Settings.SelectPages(job.ResolvePages().PageCount));
        Assert.True(dialog.AcceptPrint());
        Assert.Equal(UiDialogResultKind.Accepted, dialog.CompletedResult.Kind);
        Assert.Equal("Office", dialog.CompletedResult.Value);
    }

    [Fact]
    public void Without_Printers_The_Dialog_Says_So()
    {
        var dialog = new StandardPrintDialog { PrintHost = new FakePrintHost(printers: []), Document = Document(1) };

        Assert.False(dialog.CanPrint);
        Assert.Equal("No printers are installed.", dialog.Problem);
        Assert.Empty(dialog.PrinterCombo.Items);
    }

    [Fact]
    public void A_Document_That_Will_Not_Lay_Out_Is_Reported_Not_Thrown()
    {
        var dialog = new StandardPrintDialog { PrintHost = new FakePrintHost(), Document = new FailingDocument() };

        Assert.Null(dialog.Pages);
        Assert.StartsWith("The document could not be laid out", dialog.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Dialog_Renders_Its_Choices_And_The_Preview()
    {
        var host = new ImageTestHost(new BSize(900, 700));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var dialog = new StandardPrintDialog { PrintHost = new FakePrintHost(), Document = Document(2) };
        session.AddRoot(dialog);

        BRenderList frame = session.RenderFrame();

        frame.Validate();
        string[] texts = [.. frame.Commands.OfType<BRenderCommand.DrawText>().Select(command => command.Text.Text)];
        Assert.Contains("Print", texts);
        Assert.Contains("Printer", texts);
        Assert.Contains("Page 1 body", texts);
        Assert.Contains("Page 1 of 2", texts);
    }

    [Fact]
    public async Task A_Host_With_Its_Own_Print_UI_Is_Handed_The_Document_Without_A_Dialog()
    {
        var host = new FakePrintHost { ShowsSystemPrintDialog = true };
        var document = Document(2, new BPageSetup(BPaperSize.Legal, BPageOrientation.Portrait, BPageMargins.Uniform(20)));
        bool created = false;

        UiPrintResult result = await UiPrintDialog.PrintAsync(host, document, () =>
        {
            created = true;
            return new StandardPrintDialog();
        }, new StandardWindow());

        Assert.False(created);
        Assert.Equal(UiPrintOutcome.Printed, result.Outcome);
        Assert.NotNull(host.LastJob);
        Assert.Equal(BPaperSize.Legal, host.LastJob!.Settings.PageSetup.Paper);
        Assert.Null(host.LastJob.Pages);
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
        public int Paginations { get; private set; }

        public string Title => "Test document";

        public BPageSetup PreferredSetup => preferred;

        public IBPageSource Paginate(BPageSetup setup, CancellationToken cancellationToken = default)
        {
            Paginations++;
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

    private sealed class FailingDocument : IBPagedDocument
    {
        public string Title => "Broken";

        public BPageSetup PreferredSetup => BPageSetup.Default;

        public IBPageSource Paginate(BPageSetup setup, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("no fonts");
    }

    private sealed class FakePrintHost(IReadOnlyList<UiPrinterInfo>? printers = null) : IUiPrintHost
    {
        private readonly IReadOnlyList<UiPrinterInfo> _printers = printers ??
        [
            new UiPrinterInfo("Basement"),
            new UiPrinterInfo("Office", IsDefault: true),
        ];

        public bool ShowsSystemPrintDialog { get; init; }

        public UiPrintJob? LastJob { get; private set; }

        public IReadOnlyList<UiPrinterInfo> GetPrinters() => _printers;

        public UiPrinterCapabilities GetCapabilities(string printerName) => printerName == "Basement"
            ? new UiPrinterCapabilities { SupportsColor = false, SupportsDuplex = false, MaximumCopies = 10, Papers = [BPaperSize.A4, BPaperSize.Letter] }
            : new UiPrinterCapabilities { SupportsColor = true, SupportsDuplex = true };

        public Task<UiPrintResult> PrintAsync(UiPrintJob job, CancellationToken cancellationToken = default)
        {
            LastJob = job;
            return Task.FromResult(UiPrintResult.Printed(job.ResolvePages(cancellationToken).PageCount));
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
