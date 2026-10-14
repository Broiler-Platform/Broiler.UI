using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Broiler.UI;

/// <summary>
/// An optional host capability: printing. A control or an application looks for it with
/// <c>Session.Host is IUiPrintHost</c> and offers no Print command when it is absent.
/// </summary>
/// <remarks>
/// <para>
/// The host owns the platform side - the spooler, the printer list, the driver, the system
/// print UI where there is one - and nothing above it does. What crosses the boundary is
/// platform-neutral: printer names, capabilities as paper sizes and flags, and pages as
/// <c>Broiler.Graphics.Paging</c> pages in points, which the host rasterizes or replays for
/// its printer.
/// </para>
/// <para>
/// Desktop hosts leave the choices to Broiler's own print dialog
/// (<c>Broiler.UI.PrintDialog</c>), which fills <see cref="UiPrintSettings"/> from
/// <see cref="GetPrinters"/> and <see cref="GetCapabilities"/>. A platform whose print UI is
/// part of the job - Android's - reports <see cref="ShowsSystemPrintDialog"/>, and the
/// application hands it the document straight away: that UI picks the printer, previews the
/// pages and asks the document for them itself.
/// </para>
/// </remarks>
public interface IUiPrintHost
{
    /// <summary>
    /// True when <see cref="PrintAsync"/> shows the platform's own print UI, so the caller must
    /// not show Broiler's dialog first. The settings a job carries are then only where that UI
    /// starts.
    /// </summary>
    bool ShowsSystemPrintDialog { get; }

    /// <summary>The printers the user can print to, the default one marked. Empty when there are none.</summary>
    IReadOnlyList<UiPrinterInfo> GetPrinters();

    /// <summary>
    /// What <paramref name="printerName"/> can do. A printer the host cannot query answers
    /// <see cref="UiPrinterCapabilities.Unknown"/> rather than throwing; a dialog can still
    /// print to it with the catalogue's papers.
    /// </summary>
    UiPrinterCapabilities GetCapabilities(string printerName);

    /// <summary>
    /// Prints <paramref name="job"/>. Completes when the pages have been handed to the spooler,
    /// not when the paper comes out. A job the user cancels in a system print UI completes as
    /// <see cref="UiPrintOutcome.Cancelled"/>; one the platform refuses as
    /// <see cref="UiPrintOutcome.Failed"/> with the reason, never with an exception for a
    /// printer that is simply offline.
    /// </summary>
    Task<UiPrintResult> PrintAsync(UiPrintJob job, CancellationToken cancellationToken = default);
}
