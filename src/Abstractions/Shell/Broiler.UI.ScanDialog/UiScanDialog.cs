// Copyright (c) 2026 Broiler Platform contributors. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Paging;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.ScanDialog;

/// <summary>
/// The scan dialog: which scanner, from what source, in what colour mode and optical resolution,
/// on what paper and orientation, and how many pages - with a live preview of scanned pages.
/// </summary>
/// <remarks>
/// <para>
/// The dialog owns the decisions that are the same on every platform, so an implementation
/// only draws them: it asks <see cref="ScanHost"/> for the scanners and for what the chosen
/// one can do, keeps <see cref="Settings"/> within that, optionally acquires a preview page into
/// <see cref="Pages"/>, and builds the <see cref="UiScanJob"/> when the user accepts.
/// </para>
/// <para>
/// <see cref="ScanAsync"/> is the whole flow an application needs, including a platform
/// whose host shows its own scan UI, where no Broiler dialog is shown at all.
/// </para>
/// </remarks>
public abstract class UiScanDialog : UiDialog
{
    private IUiScanHost? _scanHost;
    private UiScanSettings _settings = new();
    private UiScanSettings? _previewedSettings;
    private IReadOnlyList<UiScannerInfo> _scanners = [];
    private UiScannerCapabilities _capabilities = UiScannerCapabilities.Unknown;
    private IBPageSource? _pages;
    private string? _scanError;
    private string? _statusText;
    private double _progress;
    private bool _isProgressIndeterminate;
    private bool _isBusy;
    private UiScanJob? _acceptedJob;

    protected UiScanDialog()
    {
        Title = "Scan";
        CanResize = true;
    }

    /// <summary>Raised when <see cref="Settings"/> changes.</summary>
    public event EventHandler? SettingsChanged;

    /// <summary>Raised when <see cref="Pages"/> is replaced by a preview scan or reset.</summary>
    public event EventHandler? PagesChanged;

    /// <summary>Raised when <see cref="IsBusy"/>, <see cref="Progress"/>, or <see cref="StatusText"/> changes.</summary>
    public event EventHandler? ProgressChanged;

    /// <summary>
    /// The host that lists scanners and runs the scan job.
    /// </summary>
    public IUiScanHost? ScanHost
    {
        get => _scanHost;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_scanHost, value))
                return;

            _scanHost = value;
            RefreshScanners();
        }
    }

    /// <summary>
    /// The user's current choices. Setting this clamps unsupported options to the current
    /// scanner's <see cref="Capabilities"/>.
    /// </summary>
    public UiScanSettings Settings
    {
        get => _settings;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);

            string? previousScanner = _settings.ScannerName;
            bool scannerChanged = !string.Equals(previousScanner, value.ScannerName, StringComparison.Ordinal);

            if (scannerChanged)
            {
                _capabilities = QueryCapabilities(value.ScannerName);
                if (_capabilities.DefaultPaper is { } defaultPaper &&
                    value.PageSetup.Paper.Matches((previousScanner is null ? BPageSetup.Default.Paper : _settings.PageSetup.Paper).Size))
                {
                    value = value with { PageSetup = value.PageSetup with { Paper = defaultPaper } };
                }
            }

            UiScanSettings fitted = Fit(value, _capabilities);
            if (_settings == fitted && !scannerChanged)
                return;

            _settings = fitted;
            _scanError = null;
            OnSettingsChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>The scanners <see cref="ScanHost"/> listed, the default first among equals.</summary>
    public IReadOnlyList<UiScannerInfo> Scanners => _scanners;

    /// <summary>Capabilities of the currently selected scanner.</summary>
    public UiScannerCapabilities Capabilities => _capabilities;

    /// <summary>The pages most recently acquired by <see cref="PreviewAsync"/>, or null before a preview scan.</summary>
    public IBPageSource? Pages => _pages;

    /// <summary>True while <see cref="PreviewAsync"/> is actively acquiring a page.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Current scan progress in the range <c>[0.0, 1.0]</c>.</summary>
    public double Progress => _progress;

    /// <summary>True when <see cref="IsBusy"/> is true and the scanner has not yet reported a progress fraction.</summary>
    public bool IsProgressIndeterminate => _isProgressIndeterminate;

    /// <summary>Human-readable status of an active scan operation, or null when idle.</summary>
    public string? StatusText => _statusText;

    /// <summary>
    /// True when <see cref="Pages"/> holds a preview acquired with the current <see cref="Settings"/>
    /// and <see cref="UiScanSettings.MaxPages"/> is 1.
    /// </summary>
    public bool HasMatchingPreview =>
        _pages is { PageCount: > 0 } &&
        _settings.MaxPages == 1 &&
        _previewedSettings == _settings;

    /// <summary>
    /// Human-readable reason the dialog cannot scan right now (no scanner, or a failed preview),
    /// or null when it is ready.
    /// </summary>
    public string? Problem
    {
        get
        {
            if (_scanError is not null)
                return _scanError;
            if (_scanHost is null)
                return "Scanning is not available on this host.";
            if (_scanners.Count == 0 && !_scanHost.ShowsSystemScanDialog)
                return "No scanners found.";
            return null;
        }
    }

    /// <summary>True when <see cref="AcceptScan"/> would accept.</summary>
    public bool CanScan => !_isBusy && Problem is null;

    /// <summary>
    /// Queries <see cref="ScanHost"/> again for its scanner list and picks a default when the
    /// current <see cref="UiScanSettings.ScannerName"/> is unset or no longer present.
    /// </summary>
    public void RefreshScanners()
    {
        ThrowIfDisposed();
        IReadOnlyList<UiScannerInfo> scanners;
        try
        {
            scanners = _scanHost?.GetScanners() ?? [];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            scanners = [];
        }

        _scanners = scanners;
        OnScannersChanged();

        string? chosen = _settings.ScannerName;
        if (string.IsNullOrEmpty(chosen) || !ContainsScanner(scanners, chosen))
            chosen = ChooseDefaultScanner(scanners);

        _capabilities = QueryCapabilities(chosen);

        UiScanSettings candidate = _settings with { ScannerName = chosen };
        if (_capabilities.DefaultPaper is { } defaultPaper && _settings.ScannerName is null)
            candidate = candidate with { PageSetup = candidate.PageSetup with { Paper = defaultPaper } };

        if (_settings.ScannerName is null &&
            _capabilities.DefaultResolution is >= UiScanSettings.MinimumDpi and <= UiScanSettings.MaximumDpi)
        {
            candidate = candidate with { Dpi = _capabilities.DefaultResolution };
        }

        UiScanSettings fitted = Fit(candidate, _capabilities);
        bool changed = fitted != _settings;
        _settings = fitted;
        if (changed)
        {
            OnSettingsChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>
    /// Runs a single-page preview scan through <see cref="ScanHost"/> using the current
    /// <see cref="Settings"/> and places the resulting pages in <see cref="Pages"/>.
    /// </summary>
    public async Task<UiScanResult> PreviewAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_scanHost is null)
            return UiScanResult.Failed("Scanning is not available on this host.");

        if (_scanners.Count == 0 && !_scanHost.ShowsSystemScanDialog)
            return UiScanResult.Failed("No scanners found.");

        if (_isBusy)
            return UiScanResult.Cancelled;

        _scanError = null;
        _isBusy = true;
        _progress = 0.0;
        _isProgressIndeterminate = true;
        _statusText = "Scanning preview...";
        NotifyProgressChanged();

        UiScanSettings previewSettings = _settings with { MaxPages = 1 };
        var job = new UiScanJob(
            previewSettings,
            isPreview: true,
            progress: p =>
            {
                if (IsDisposed)
                    return;

                _progress = Math.Clamp(p.Fraction, 0.0, 1.0);
                _isProgressIndeterminate = _progress <= 0.0;
                int percent = (int)Math.Round(_progress * 100.0);
                _statusText = _isProgressIndeterminate
                    ? "Scanning preview..."
                    : $"Scanning preview ({percent}%)...";
                NotifyProgressChanged();
            });

        UiScanResult result;
        try
        {
            result = await _scanHost.ScanAsync(job, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            result = UiScanResult.Failed(ex.Message);
        }
        finally
        {
            _isBusy = false;
            _isProgressIndeterminate = false;
            _statusText = null;
            if (!IsDisposed)
                NotifyProgressChanged();
        }

        if (IsDisposed)
            return result;

        if (result.Outcome == UiScanOutcome.Scanned)
        {
            _scanError = null;
            _pages = result.Pages;
            _previewedSettings = previewSettings;
            OnPagesChanged();
            PagesChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (result.Outcome == UiScanOutcome.Failed)
        {
            _scanError = string.IsNullOrWhiteSpace(result.Message) ? "Preview scan failed." : result.Message;
            OnPagesChanged();
        }
        else if (result.Outcome == UiScanOutcome.Cancelled && !cancellationToken.IsCancellationRequested)
        {
            _scanError = "The scanner returned no pages.";
            OnPagesChanged();
        }

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return result;
    }

    /// <summary>
    /// Replaces <see cref="Pages"/> with <paramref name="pages"/> and clears any preview error.
    /// </summary>
    public void SetPreviewPages(IBPageSource? pages)
    {
        ThrowIfDisposed();
        _scanError = null;
        _previewedSettings = null;
        if (ReferenceEquals(_pages, pages))
            return;

        _pages = pages;
        OnPagesChanged();
        PagesChanged?.Invoke(this, EventArgs.Empty);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>Shows the dialog modal over <paramref name="owner"/>.</summary>
    public Task<UiDialogResult> ShowScanModal(UiWindow owner, BRect placement = default) =>
        ShowModal(owner, placement);

    /// <summary>
    /// The job the dialog was accepted with, or null before it is. Kept past the dialog's close:
    /// a closed window is disposed, and whoever awaited the dialog reads the job afterwards.
    /// </summary>
    public UiScanJob? AcceptedJob => _acceptedJob;

    /// <summary>
    /// Accepts the dialog when it can scan, with the chosen scanner's name as the result value,
    /// and keeps the job it was accepted with in <see cref="AcceptedJob"/>.
    /// </summary>
    public bool AcceptScan()
    {
        if (!CanScan)
            return false;

        _acceptedJob = CreateJob();
        if (Accept(_settings.ScannerName))
            return true;

        _acceptedJob = null;
        return false;
    }

    /// <summary>The job the current choices make.</summary>
    public UiScanJob CreateJob(Action<UiScanProgress>? progress = null)
    {
        ThrowIfDisposed();
        return new UiScanJob(_settings, isPreview: false, progress);
    }

    /// <summary>
    /// Full scan flow: if <paramref name="host"/> has its own system scan dialog, calls it
    /// directly; otherwise shows the dialog created by <paramref name="createDialog"/> and hands
    /// the accepted job to <paramref name="host"/> (or reuses the already-acquired preview page
    /// when <see cref="HasMatchingPreview"/> is true).
    /// </summary>
    public static async Task<UiScanResult> ScanAsync(
        IUiScanHost host,
        Func<UiScanDialog> createDialog,
        UiWindow owner,
        BRect placement = default,
        UiScanSettings? initial = null,
        CancellationToken cancellationToken = default,
        Action<UiScanProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(createDialog);
        ArgumentNullException.ThrowIfNull(owner);

        if (host.ShowsSystemScanDialog)
        {
            UiScanSettings settings = initial ?? new UiScanSettings();
            return await host.ScanAsync(new UiScanJob(settings, isPreview: false, progress), cancellationToken).ConfigureAwait(true);
        }

        UiScanDialog dialog = createDialog();
        dialog.ScanHost = host;
        if (initial is not null)
            dialog.Settings = initial;

        UiDialogResult result = await dialog.ShowModal(owner, placement).ConfigureAwait(true);
        if (result.Kind != UiDialogResultKind.Accepted || dialog.AcceptedJob is not UiScanJob acceptedJob)
            return UiScanResult.Cancelled;

        if (dialog.HasMatchingPreview && dialog.Pages is { PageCount: > 0 } previewedPages)
            return UiScanResult.Scanned(previewedPages);

        UiScanJob job = progress is not null && acceptedJob.Progress is null
            ? new UiScanJob(acceptedJob.Settings, acceptedJob.IsPreview, progress)
            : acceptedJob;
        return await host.ScanAsync(job, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Called when <see cref="Settings"/> has changed, before <see cref="SettingsChanged"/>.</summary>
    protected virtual void OnSettingsChanged()
    {
    }

    /// <summary>Called when <see cref="Scanners"/> was listed again.</summary>
    protected virtual void OnScannersChanged()
    {
    }

    /// <summary>Called when <see cref="Pages"/> or the preview state has changed, before <see cref="PagesChanged"/>.</summary>
    protected virtual void OnPagesChanged()
    {
    }

    /// <summary>Called when <see cref="IsBusy"/>, <see cref="Progress"/>, or <see cref="StatusText"/> has changed, before <see cref="ProgressChanged"/>.</summary>
    protected virtual void OnProgressChanged()
    {
    }

    private void NotifyProgressChanged()
    {
        OnProgressChanged();
        ProgressChanged?.Invoke(this, EventArgs.Empty);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    private UiScannerCapabilities QueryCapabilities(string? scannerName)
    {
        if (_scanHost is null || string.IsNullOrEmpty(scannerName))
            return UiScannerCapabilities.Unknown;

        try
        {
            return _scanHost.GetCapabilities(scannerName) ?? UiScannerCapabilities.Unknown;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return UiScannerCapabilities.Unknown;
        }
    }

    private static bool ContainsScanner(IReadOnlyList<UiScannerInfo> scanners, string name)
    {
        foreach (UiScannerInfo scanner in scanners)
        {
            if (string.Equals(scanner.Name, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? ChooseDefaultScanner(IReadOnlyList<UiScannerInfo> scanners)
    {
        foreach (UiScannerInfo scanner in scanners)
        {
            if (scanner.IsDefault)
                return scanner.Name;
        }

        return scanners.Count > 0 ? scanners[0].Name : null;
    }

    private static UiScanSettings Fit(UiScanSettings settings, UiScannerCapabilities capabilities)
    {
        UiScanSource source = settings.Source;
        if (capabilities.Sources.Count > 0 && !capabilities.Sources.Contains(source))
            source = capabilities.Sources[0];

        UiScanColorMode color = settings.ColorMode;
        if (capabilities.ColorModes.Count > 0 && !capabilities.ColorModes.Contains(color))
            color = capabilities.ColorModes[0];

        int dpi = settings.Dpi;
        if (capabilities.Resolutions.Count > 0 && !capabilities.Resolutions.Contains(dpi))
        {
            dpi = capabilities.Resolutions
                .OrderBy(candidate => Math.Abs(candidate - settings.Dpi))
                .ThenBy(candidate => candidate)
                .First();
            dpi = Math.Clamp(dpi, UiScanSettings.MinimumDpi, UiScanSettings.MaximumDpi);
        }

        BPageSetup setup = settings.PageSetup;
        if (capabilities.Papers.Count > 0 && !ContainsPaper(capabilities.Papers, setup.Paper))
        {
            BPaperSize fallback = capabilities.DefaultPaper is { } preferred && ContainsPaper(capabilities.Papers, preferred)
                ? preferred
                : capabilities.Papers[0];
            setup = setup with { Paper = fallback };
        }

        int maximumPages = Math.Clamp(capabilities.MaximumPages, 1, UiScanSettings.MaximumPages);
        int maxPages = source == UiScanSource.Flatbed
            ? 1
            : Math.Clamp(settings.MaxPages, 1, maximumPages);

        return settings with
        {
            Source = source,
            ColorMode = color,
            Dpi = dpi,
            PageSetup = setup,
            MaxPages = maxPages,
        };
    }

    private static bool ContainsPaper(IReadOnlyList<BPaperSize> papers, BPaperSize target)
    {
        foreach (BPaperSize paper in papers)
        {
            if (paper.Matches(target.Size))
                return true;
        }

        return false;
    }
}
