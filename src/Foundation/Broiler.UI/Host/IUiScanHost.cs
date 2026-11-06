// Copyright (c) 2026 Broiler Platform contributors. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.UI;

/// <summary>
/// An optional host capability: scanning. A control or an application looks for it with
/// <c>Session.Host is IUiScanHost</c> and offers no Scan command when it is absent.
/// </summary>
/// <remarks>
/// <para>
/// The host owns the platform side - WIA 2.0 on Windows, SANE on Linux, or Android's document
/// scanner - and nothing above it does. What crosses the boundary is platform-neutral: scanner
/// names, capabilities as paper sizes, sources, colour modes and resolutions, and scanned pages as
/// <c>Broiler.Graphics.Paging</c> pages in points.
/// </para>
/// <para>
/// Desktop hosts leave the choices to Broiler's own scan dialog
/// (<c>Broiler.UI.ScanDialog</c>), which fills <see cref="UiScanSettings"/> from
/// <see cref="GetScanners"/> and <see cref="GetCapabilities"/>, optionally runs a preview scan
/// through <see cref="ScanAsync"/>, and builds the accepted <see cref="UiScanJob"/>. A platform
/// whose scan UI is part of the OS flow - Android's - reports <see cref="ShowsSystemScanDialog"/>,
/// and the application hands it the job straight away.
/// </para>
/// </remarks>
public interface IUiScanHost
{
    /// <summary>
    /// True when <see cref="ScanAsync"/> opens a platform scan UI of its own (as on Android), so
    /// an application should call it directly rather than showing <c>UiScanDialog</c> first.
    /// </summary>
    bool ShowsSystemScanDialog { get; }

    /// <summary>
    /// The scanners the system knows about right now. An empty list means none is configured;
    /// the host does not throw for that.
    /// </summary>
    IReadOnlyList<UiScannerInfo> GetScanners();

    /// <summary>
    /// What <paramref name="scannerName"/> can do, or <see cref="UiScannerCapabilities.Unknown"/>
    /// when the scanner cannot be asked.
    /// </summary>
    UiScannerCapabilities GetCapabilities(string scannerName);

    /// <summary>
    /// Acquires pages from the scanner according to <paramref name="job"/>.
    /// </summary>
    Task<UiScanResult> ScanAsync(UiScanJob job, CancellationToken cancellationToken = default);
}
