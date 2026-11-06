// Copyright (c) 2026 Broiler Platform contributors. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Paging;
using Broiler.Graphics.Resources;

namespace Broiler.UI;

/// <summary>
/// A scanner reported by <see cref="IUiScanHost.GetScanners"/>.
/// </summary>
/// <param name="Name">The name passed back in <see cref="UiScanSettings.ScannerName"/>.</param>
/// <param name="IsDefault">True for the scanner the platform selects when the user has not chosen one.</param>
/// <param name="Location">Physical or network location reported by the driver, when any.</param>
/// <param name="Description">Driver or model description, when any.</param>
public sealed record UiScannerInfo(
    string Name,
    bool IsDefault = false,
    string? Location = null,
    string? Description = null);

/// <summary>
/// Paper source for a scan job.
/// </summary>
public enum UiScanSource
{
    Flatbed = 0,
    FeederOneSided = 1,
    FeederTwoSided = 2,
}

/// <summary>
/// Colour mode requested for a scan job.
/// </summary>
public enum UiScanColorMode
{
    Color = 0,
    Grayscale = 1,
    BlackAndWhite = 2,
}

/// <summary>
/// What a scanner can do, used by the scan dialog to enable or hide controls and pick defaults.
/// </summary>
public sealed record UiScannerCapabilities
{
    private static readonly IReadOnlyList<UiScanSource> DefaultSources = [UiScanSource.Flatbed];
    private static readonly IReadOnlyList<UiScanColorMode> DefaultColorModes =
    [
        UiScanColorMode.Color,
        UiScanColorMode.Grayscale,
        UiScanColorMode.BlackAndWhite,
    ];
    private static readonly IReadOnlyList<int> DefaultResolutions = [75, 150, 300, 600];

    /// <summary>Conservative capabilities for a scanner whose driver could not be queried.</summary>
    public static UiScannerCapabilities Unknown { get; } = new();

    /// <summary>Paper sources the scanner exposes. Never empty when returned by a host; defaults to <see cref="UiScanSource.Flatbed"/>.</summary>
    public IReadOnlyList<UiScanSource> Sources { get; init; } = DefaultSources;

    /// <summary>Colour modes the scanner supports.</summary>
    public IReadOnlyList<UiScanColorMode> ColorModes { get; init; } = DefaultColorModes;

    /// <summary>Discrete optical resolutions in dots per inch exposed by the scanner.</summary>
    public IReadOnlyList<int> Resolutions { get; init; } = DefaultResolutions;

    /// <summary>The scanner's preferred optical resolution in dots per inch.</summary>
    public int DefaultResolution { get; init; } = 300;

    /// <summary>Paper sizes the scan bed or feeder accepts. Never empty when returned by a host; defaults to <see cref="BPaperSize.Standard"/>.</summary>
    public IReadOnlyList<BPaperSize> Papers { get; init; } = BPaperSize.Standard;

    /// <summary>The scanner's default paper size, or null when it did not report one.</summary>
    public BPaperSize? DefaultPaper { get; init; }

    /// <summary>Highest page count the scanner accepts for a feeder scan.</summary>
    public int MaximumPages { get; init; } = UiScanSettings.MaximumPages;
}

/// <summary>
/// Everything the user (or the caller) chose for a scan job.
/// </summary>
public sealed record UiScanSettings
{
    /// <summary>Upper bound enforced on <see cref="MaxPages"/>.</summary>
    public const int MaximumPages = 999;

    /// <summary>Lower bound enforced on <see cref="Dpi"/>.</summary>
    public const int MinimumDpi = 50;

    /// <summary>Upper bound enforced on <see cref="Dpi"/>.</summary>
    public const int MaximumDpi = 4800;

    private readonly int _dpi = 300;
    private readonly int _maxPages = 1;
    private readonly double _brightness;
    private readonly double _contrast;
    private readonly BPageSetup _pageSetup = BPageSetup.Default;

    /// <summary>Target scanner name, or null for the host's default scanner.</summary>
    public string? ScannerName { get; init; }

    /// <summary>Paper source (flatbed or feeder).</summary>
    public UiScanSource Source { get; init; } = UiScanSource.Flatbed;

    /// <summary>Colour, grayscale, or black-and-white scanning.</summary>
    public UiScanColorMode ColorMode { get; init; } = UiScanColorMode.Color;

    /// <summary>
    /// Optical resolution in dots per inch, between <see cref="MinimumDpi"/> and <see cref="MaximumDpi"/>.
    /// </summary>
    public int Dpi
    {
        get => _dpi;
        init
        {
            if (value is < MinimumDpi or > MaximumDpi)
                throw new ArgumentOutOfRangeException(nameof(Dpi), value, $"Dpi must be between {MinimumDpi} and {MaximumDpi}.");
            _dpi = value;
        }
    }

    /// <summary>Scan area paper size and orientation.</summary>
    public BPageSetup PageSetup
    {
        get => _pageSetup;
        init => _pageSetup = value ?? throw new ArgumentNullException(nameof(PageSetup));
    }

    /// <summary>
    /// Maximum number of pages to acquire, between 1 and <see cref="MaximumPages"/>.
    /// Flatbed scans always acquire 1 page.
    /// </summary>
    public int MaxPages
    {
        get => _maxPages;
        init
        {
            if (value is < 1 or > MaximumPages)
                throw new ArgumentOutOfRangeException(nameof(MaxPages), value, $"MaxPages must be between 1 and {MaximumPages}.");
            _maxPages = value;
        }
    }

    /// <summary>
    /// Normalized brightness adjustment in the range <c>[-100, 100]</c>, where <c>0</c> is neutral.
    /// </summary>
    public double Brightness
    {
        get => _brightness;
        init
        {
            if (double.IsNaN(value) || value is < -100.0 or > 100.0)
                throw new ArgumentOutOfRangeException(nameof(Brightness), value, "Brightness must be between -100 and 100.");
            _brightness = value;
        }
    }

    /// <summary>
    /// Normalized contrast adjustment in the range <c>[-100, 100]</c>, where <c>0</c> is neutral.
    /// </summary>
    public double Contrast
    {
        get => _contrast;
        init
        {
            if (double.IsNaN(value) || value is < -100.0 or > 100.0)
                throw new ArgumentOutOfRangeException(nameof(Contrast), value, "Contrast must be between -100 and 100.");
            _contrast = value;
        }
    }
}

/// <summary>
/// Live progress of an active scan job reported to <see cref="UiScanJob.ReportProgress"/>.
/// </summary>
/// <param name="PageNumber">The 0-based page index currently being transferred.</param>
/// <param name="Fraction">Estimated progress of the current page in <c>[0.0, 1.0]</c>, or <c>0</c> when indeterminate.</param>
/// <param name="BytesTransferred">Cumulative bytes transferred for the current page.</param>
public readonly record struct UiScanProgress(
    int PageNumber,
    double Fraction,
    long BytesTransferred = 0);

/// <summary>
/// A scan job handed to <see cref="IUiScanHost.ScanAsync"/>: the <see cref="UiScanSettings"/> and
/// whether the request is a quick single-page preview scan.
/// </summary>
public sealed class UiScanJob
{
    private readonly Action<UiScanProgress>? _progress;

    public UiScanJob(
        UiScanSettings settings,
        bool isPreview = false,
        Action<UiScanProgress>? progress = null)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        IsPreview = isPreview;
        _progress = progress;
    }

    /// <summary>The settings chosen for this job.</summary>
    public UiScanSettings Settings { get; }

    /// <summary>
    /// True when the dialog is requesting a quick single-page preview scan rather than a final acquisition.
    /// </summary>
    public bool IsPreview { get; }

    /// <summary>
    /// Optional progress callback notified by the scan host as scan data arrives.
    /// </summary>
    public Action<UiScanProgress>? Progress => _progress;

    /// <summary>
    /// Reports live scan progress to <see cref="Progress"/>, clamping <paramref name="fraction"/> to <c>[0.0, 1.0]</c>.
    /// </summary>
    public void ReportProgress(int pageNumber, double fraction, long bytesTransferred = 0)
    {
        if (_progress is null)
            return;

        double clamped = double.IsNaN(fraction) ? 0.0 : Math.Clamp(fraction, 0.0, 1.0);
        _progress(new UiScanProgress(Math.Max(0, pageNumber), clamped, Math.Max(0, bytesTransferred)));
    }
}

/// <summary>
/// How a scan attempt ended.
/// </summary>
public enum UiScanOutcome
{
    Scanned = 0,
    Cancelled = 1,
    Failed = 2,
}

/// <summary>
/// Outcome of <see cref="IUiScanHost.ScanAsync"/>.
/// </summary>
/// <param name="Outcome">Whether pages were scanned, the user cancelled, or the scanner reported an error.</param>
/// <param name="Pages">The scanned pages in points (1/72 inch), when <paramref name="Outcome"/> is <see cref="UiScanOutcome.Scanned"/>.</param>
/// <param name="Message">Diagnostic text when <paramref name="Outcome"/> is <see cref="UiScanOutcome.Failed"/>.</param>
public sealed record UiScanResult(
    UiScanOutcome Outcome,
    IBPageSource? Pages = null,
    string? Message = null)
{
    /// <summary>Number of pages acquired in <see cref="Pages"/>.</summary>
    public int PagesScanned => Pages?.PageCount ?? 0;

    public static UiScanResult Cancelled { get; } = new(UiScanOutcome.Cancelled);

    public static UiScanResult Scanned(IBPageSource pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return new(UiScanOutcome.Scanned, pages);
    }

    public static UiScanResult Scanned(IReadOnlyList<BPixelBuffer> pages, int dpi = 300, BSize? pageSize = null)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "Dpi must be positive.");

        var built = new BPage[pages.Count];
        for (int i = 0; i < pages.Count; i++)
        {
            BPixelBuffer pixels = pages[i] ?? throw new ArgumentException("Page pixel buffer cannot be null.", nameof(pages));
            BSize targetSize = pageSize ?? new BSize(pixels.Width * 72.0 / dpi, pixels.Height * 72.0 / dpi);
            var builder = new BPageBuilder(targetSize);
            BImageHandle image = builder.AddImage(pixels);
            builder.Content.DrawImage(
                image,
                new BRect(0, 0, pixels.Width, pixels.Height),
                new BRect(0, 0, targetSize.Width, targetSize.Height));
            built[i] = builder.Build();
        }

        return new(UiScanOutcome.Scanned, new BPageCollection(built));
    }

    public static UiScanResult Failed(string message) => new(UiScanOutcome.Failed, null, message);
}
