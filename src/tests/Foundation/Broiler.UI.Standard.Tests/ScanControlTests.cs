// Copyright (c) 2026 Broiler Platform contributors. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Paging;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Resources;
using Broiler.UI.ScanDialog;
using Broiler.UI.ScanDialog.Standard;
using Broiler.UI.Window.Standard;

namespace Broiler.UI.Standard.Tests;

public sealed class ScanControlTests
{
    [Fact]
    public void The_Dialog_Selects_The_Default_Scanner_And_Shows_No_Pages_Before_Preview()
    {
        var scanner = new FakeScanHost();
        var dialog = new StandardScanDialog { ScanHost = scanner };

        Assert.Equal("Office Scanner", dialog.Settings.ScannerName);
        Assert.Equal(1, dialog.ScannerCombo.SelectedIndex);
        Assert.Null(dialog.Pages);
        Assert.Equal(0, dialog.Preview.PageCount);
        Assert.Equal("No pages", dialog.Preview.PageCaption);
        Assert.True(dialog.CanScan);
        Assert.Null(dialog.Problem);
    }

    [Fact]
    public void Switching_To_A_Restricted_Scanner_Clamps_Source_Color_Resolution_And_MaxPages()
    {
        var scanner = new FakeScanHost();
        var dialog = new StandardScanDialog { ScanHost = scanner };

        dialog.Settings = dialog.Settings with
        {
            Source = UiScanSource.FeederTwoSided,
            ColorMode = UiScanColorMode.Color,
            Dpi = 600,
            MaxPages = 25,
        };
        Assert.Equal(UiScanSource.FeederTwoSided, dialog.Settings.Source);
        Assert.Equal(UiScanColorMode.Color, dialog.Settings.ColorMode);
        Assert.Equal(600, dialog.Settings.Dpi);
        Assert.Equal(25, dialog.Settings.MaxPages);

        dialog.ScannerCombo.SelectIndex(0);

        Assert.Equal("Flatbed Only", dialog.Settings.ScannerName);
        Assert.Equal(UiScanSource.Flatbed, dialog.Settings.Source);
        Assert.Equal(UiScanColorMode.Grayscale, dialog.Settings.ColorMode);
        Assert.Equal(300, dialog.Settings.Dpi);
        Assert.Equal(1, dialog.Settings.MaxPages);
        Assert.False(dialog.SourceCombo.IsEnabled);
        Assert.False(dialog.ColorCombo.IsEnabled);
        Assert.False(dialog.PagesSpin.IsEnabled);
    }

    [Fact]
    public async Task Preview_Scan_Populates_The_Preview_And_Updates_Pager_Controls()
    {
        var scanner = new FakeScanHost { PagesPerPreview = 2 };
        var dialog = new StandardScanDialog { ScanHost = scanner };

        UiScanResult previewResult = await dialog.PreviewAsync();

        Assert.Equal(UiScanOutcome.Scanned, previewResult.Outcome);
        Assert.NotNull(scanner.LastJob);
        Assert.True(scanner.LastJob!.IsPreview);
        Assert.Equal(1, scanner.LastJob.Settings.MaxPages);
        Assert.Equal(2, dialog.Preview.PageCount);
        Assert.Equal("Page 1 of 2", dialog.Preview.PageCaption);
        Assert.False(dialog.PreviousButton.IsEnabled);
        Assert.True(dialog.NextButton.IsEnabled);

        dialog.NextButton.Click();
        Assert.Equal(1, dialog.Preview.PageIndex);
        Assert.True(dialog.PreviousButton.IsEnabled);
        Assert.False(dialog.NextButton.IsEnabled);
    }

    [Fact]
    public async Task A_Scanner_Less_Host_Or_Failed_Preview_Disables_The_Scan_Button_With_A_Reason()
    {
        var emptyDialog = new StandardScanDialog { ScanHost = new FakeScanHost(scanners: []) };

        Assert.False(emptyDialog.CanScan);
        Assert.False(emptyDialog.ScanButton.IsEnabled);
        Assert.False(emptyDialog.PreviewButton.IsEnabled);
        Assert.Equal("No scanners found.", emptyDialog.Problem);
        Assert.False(emptyDialog.AcceptScan());

        var failingHost = new FakeScanHost { FailWithMessage = "Scanner lamp warming up" };
        var failingDialog = new StandardScanDialog { ScanHost = failingHost };

        UiScanResult result = await failingDialog.PreviewAsync();

        Assert.Equal(UiScanOutcome.Failed, result.Outcome);
        Assert.False(failingDialog.CanScan);
        Assert.False(failingDialog.ScanButton.IsEnabled);
        Assert.Equal("Scanner lamp warming up", failingDialog.Problem);
    }

    [Fact]
    public void The_Dialog_Renders_Its_Controls_Preview_And_Action_Buttons()
    {
        var host = new ImageTestHost(new BSize(1000, 800));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var dialog = new StandardScanDialog { ScanHost = new FakeScanHost() };
        dialog.SetPreviewPages(UiScanResult.Scanned(
        [
            new BPixelBuffer(100, 100, new byte[100 * 100 * 4]),
            new BPixelBuffer(100, 100, new byte[100 * 100 * 4]),
        ], dpi: 100).Pages);
        _ = dialog.ShowScanModal(owner, new BRect(20, 20, 760, 520));

        BRenderList frame = session.RenderFrame();
        string[] drawn = [.. frame.Commands.OfType<BRenderCommand.DrawText>().Select(c => c.Text.Text)];

        Assert.Contains("Scan", drawn);
        Assert.Contains("Scanner", drawn);
        Assert.Contains("Source", drawn);
        Assert.Contains("Colour", drawn);
        Assert.Contains("Resolution", drawn);
        Assert.Contains("Paper", drawn);
        Assert.Contains("Orientation", drawn);
        Assert.Contains("Pages", drawn);
        Assert.Contains("Preview", drawn);
        Assert.Contains("Cancel", drawn);
        Assert.Contains("Page 1 of 2", drawn);
        Assert.True(dialog.Preview.Bounds.Width > 300 && dialog.Preview.Bounds.Height > 300);
    }

    [Fact]
    public async Task A_System_Dialog_Host_Bypasses_The_Broiler_Dialog()
    {
        var host = new FakeScanHost { ShowsSystemScanDialog = true };
        bool created = false;

        UiScanResult result = await UiScanDialog.ScanAsync(host, () =>
        {
            created = true;
            return new StandardScanDialog();
        }, new StandardWindow());

        Assert.False(created);
        Assert.Equal(UiScanOutcome.Scanned, result.Outcome);
        Assert.Equal(1, result.PagesScanned);
        Assert.NotNull(host.LastJob);
        Assert.False(host.LastJob!.IsPreview);
    }

    [Fact]
    public async Task Accepting_The_Modal_Dialog_Sends_The_Configured_Job_To_The_Host()
    {
        var host = new ImageTestHost(new BSize(1000, 800));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);
        var scanner = new FakeScanHost();

        Task<UiScanResult> scanning = UiScanDialog.ScanAsync(scanner, () => new StandardScanDialog(), owner);
        StandardScanDialog dialog = Assert.IsType<StandardScanDialog>(session.ModalElement);
        dialog.SourceCombo.SelectIndex(1); // FeederOneSided
        dialog.PagesSpin.Value = 3;
        Assert.True(dialog.AcceptScan());

        UiScanResult result = await scanning;

        Assert.True(dialog.IsDisposed);
        Assert.Equal(UiScanOutcome.Scanned, result.Outcome);
        Assert.Equal(3, result.PagesScanned);
        Assert.Same(dialog.AcceptedJob, scanner.LastJob);
        Assert.Equal(UiScanSource.FeederOneSided, scanner.LastJob!.Settings.Source);
        Assert.Equal(3, scanner.LastJob.Settings.MaxPages);
    }

    [Fact]
    public async Task Preview_Scan_Shows_Progress_Bar_Updates_Percentage_And_ScanAsync_Reuses_Matching_Preview()
    {
        var host = new ImageTestHost(new BSize(1000, 800));
        using UiSession session = new StandardUiSessionBuilder().Build(host);
        var owner = new StandardWindow();
        session.AddRoot(owner);

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        UiScanJob? activePreviewJob = null;
        int scanCallCount = 0;
        var scanner = new FakeScanHost
        {
            OnScanAsync = async job =>
            {
                scanCallCount++;
                activePreviewJob = job;
                job.ReportProgress(0, 0.45, 4500);
                await gate.Task.ConfigureAwait(false);
                job.ReportProgress(0, 1.0, 10000);
            },
        };

        Task<UiScanResult> scanning = UiScanDialog.ScanAsync(scanner, () => new StandardScanDialog(), owner);
        StandardScanDialog dialog = Assert.IsType<StandardScanDialog>(session.ModalElement);
        Assert.Equal(UiVisibility.Collapsed, dialog.ProgressBar.Visibility);
        Assert.False(dialog.IsBusy);

        Task<UiScanResult> previewTask = dialog.PreviewAsync();

        Assert.NotNull(activePreviewJob);
        Assert.True(dialog.IsBusy);
        Assert.Equal(UiVisibility.Visible, dialog.ProgressBar.Visibility);
        Assert.False(dialog.ProgressBar.IsIndeterminate);
        Assert.Equal(45.0, dialog.ProgressBar.Value, precision: 3);
        Assert.Equal("Scanning preview (45%)...", dialog.StatusText);
        Assert.False(dialog.PreviewButton.IsEnabled);
        Assert.False(dialog.ScanButton.IsEnabled);

        BRenderList busyFrame = session.RenderFrame();
        string[] busyText = [.. busyFrame.Commands.OfType<BRenderCommand.DrawText>().Select(c => c.Text.Text)];
        Assert.Contains("Scanning preview (45%)...", busyText);
        Assert.Contains("45%", busyText);

        gate.SetResult(true);
        UiScanResult previewResult = await previewTask;

        Assert.Equal(UiScanOutcome.Scanned, previewResult.Outcome);
        Assert.False(dialog.IsBusy);
        Assert.Equal(UiVisibility.Collapsed, dialog.ProgressBar.Visibility);
        Assert.Null(dialog.StatusText);
        Assert.True(dialog.HasMatchingPreview);
        Assert.Equal(1, dialog.Preview.PageCount);
        Assert.True(dialog.ScanButton.IsEnabled);

        Assert.True(dialog.AcceptScan());
        UiScanResult finalResult = await scanning;

        Assert.Equal(UiScanOutcome.Scanned, finalResult.Outcome);
        Assert.Equal(1, finalResult.PagesScanned);
        Assert.Equal(1, scanCallCount);
        Assert.Same(previewResult.Pages, finalResult.Pages);
    }

    private sealed class FakeScanHost(IReadOnlyList<UiScannerInfo>? scanners = null) : IUiScanHost
    {
        private readonly IReadOnlyList<UiScannerInfo> _scanners = scanners ??
        [
            new UiScannerInfo("Flatbed Only"),
            new UiScannerInfo("Office Scanner", IsDefault: true),
        ];

        public bool ShowsSystemScanDialog { get; init; }

        public int PagesPerPreview { get; init; } = 1;

        public string? FailWithMessage { get; init; }

        public Func<UiScanJob, Task>? OnScanAsync { get; init; }

        public UiScanJob? LastJob { get; private set; }

        public IReadOnlyList<UiScannerInfo> GetScanners() => _scanners;

        public UiScannerCapabilities GetCapabilities(string scannerName) => scannerName == "Flatbed Only"
            ? new UiScannerCapabilities
            {
                Sources = [UiScanSource.Flatbed],
                ColorModes = [UiScanColorMode.Grayscale],
                Resolutions = [150, 300],
                DefaultResolution = 300,
                Papers = [BPaperSize.A4, BPaperSize.Letter],
                MaximumPages = 1,
            }
            : new UiScannerCapabilities
            {
                Sources = [UiScanSource.Flatbed, UiScanSource.FeederOneSided, UiScanSource.FeederTwoSided],
                ColorModes = [UiScanColorMode.Color, UiScanColorMode.Grayscale, UiScanColorMode.BlackAndWhite],
                Resolutions = [75, 150, 300, 600],
                DefaultResolution = 300,
                MaximumPages = 50,
            };

        public async Task<UiScanResult> ScanAsync(UiScanJob job, CancellationToken cancellationToken = default)
        {
            LastJob = job;
            if (OnScanAsync is not null)
                await OnScanAsync(job).ConfigureAwait(false);

            if (FailWithMessage is not null)
                return UiScanResult.Failed(FailWithMessage);

            int count = job.IsPreview ? PagesPerPreview : job.Settings.MaxPages;
            var buffers = new BPixelBuffer[count];
            for (int i = 0; i < count; i++)
                buffers[i] = new BPixelBuffer(10, 10, new byte[10 * 10 * 4]);

            return UiScanResult.Scanned(buffers, job.Settings.Dpi, job.Settings.PageSetup.PageSize);
        }
    }

    private sealed class ImageTestHost(BSize viewportSize) : IUiHost, IUiImageHost
    {
        private int _nextId;

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

        public BImageHandle CreateImage(BPixelBuffer pixels) =>
            BImageHandle.FromId(5000 + (ulong)++_nextId, new BSize(pixels.Width, pixels.Height));

        public void ReleaseImage(BImageHandle image)
        {
        }
    }
}
