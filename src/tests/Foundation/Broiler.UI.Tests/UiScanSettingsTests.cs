// Copyright (c) 2026 Broiler Platform contributors. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Paging;

namespace Broiler.UI.Tests;

public sealed class UiScanSettingsTests
{
    [Fact]
    public void Defaults_Are_Flatbed_Color_300Dpi_And_Single_Page()
    {
        var settings = new UiScanSettings();

        Assert.Null(settings.ScannerName);
        Assert.Equal(UiScanSource.Flatbed, settings.Source);
        Assert.Equal(UiScanColorMode.Color, settings.ColorMode);
        Assert.Equal(300, settings.Dpi);
        Assert.Equal(1, settings.MaxPages);
        Assert.Equal(0.0, settings.Brightness);
        Assert.Equal(0.0, settings.Contrast);
        Assert.Equal(BPageSetup.Default, settings.PageSetup);
    }

    [Fact]
    public void Out_Of_Range_Values_Are_Rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { Dpi = UiScanSettings.MinimumDpi - 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { Dpi = UiScanSettings.MaximumDpi + 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { MaxPages = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { MaxPages = UiScanSettings.MaximumPages + 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { Brightness = -101.0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { Brightness = 101.0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { Brightness = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { Contrast = -101.0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { Contrast = 101.0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiScanSettings { Contrast = double.NaN });
        Assert.Throws<ArgumentNullException>(() => new UiScanSettings { PageSetup = null! });
    }

    [Fact]
    public void Scan_Job_And_Result_Convert_Pixels_To_Pages_In_Points()
    {
        var settings = new UiScanSettings
        {
            ScannerName = "DeskScanner",
            Source = UiScanSource.FeederOneSided,
            ColorMode = UiScanColorMode.Grayscale,
            Dpi = 150,
            MaxPages = 2,
        };
        var job = new UiScanJob(settings, isPreview: true);

        Assert.Same(settings, job.Settings);
        Assert.True(job.IsPreview);

        var pixels = new BPixelBuffer(300, 150, new byte[300 * 150 * 4]);
        UiScanResult result = UiScanResult.Scanned([pixels], dpi: 150);

        Assert.Equal(UiScanOutcome.Scanned, result.Outcome);
        Assert.Equal(1, result.PagesScanned);
        Assert.NotNull(result.Pages);
        BPage page = result.Pages!.GetPage(0);
        Assert.Equal(new BSize(144.0, 72.0), page.Size);
        Assert.Single(page.Images);
    }
}
