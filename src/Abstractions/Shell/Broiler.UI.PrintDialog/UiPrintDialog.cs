using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Paging;
using Broiler.UI.Dialog;
using Broiler.UI.Window;

namespace Broiler.UI.PrintDialog;

/// <summary>
/// The print dialog: which printer, how many copies, which pages, on what paper and which way
/// up - with the document laid out again, and previewed, for whatever page is chosen.
/// </summary>
/// <remarks>
/// <para>
/// The dialog owns the decisions that are the same on every platform, so an implementation
/// only draws them: it asks <see cref="PrintHost"/> for the printers and for what the chosen
/// one can do, keeps <see cref="Settings"/> within that, and re-paginates
/// <see cref="Document"/> whenever the page setup changes, keeping the pages it previewed in
/// <see cref="Pages"/> so a job prints exactly those.
/// </para>
/// <para>
/// <see cref="PrintAsync"/> is the whole flow an application needs, including a platform
/// whose host shows its own print UI, where no Broiler dialog is shown at all.
/// </para>
/// </remarks>
// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=4; Fingerprint=TBF
// Broiler-Human:        PENDING
public abstract class UiPrintDialog : UiDialog
{
    private IUiPrintHost? _printHost;
    private IBPagedDocument? _document;
    private UiPrintSettings _settings = new();
    private IReadOnlyList<UiPrinterInfo> _printers = [];
    private UiPrinterCapabilities _capabilities = UiPrinterCapabilities.Unknown;
    private IBPageSource? _pages;
    private BPageSetup? _paginatedFor;
    private string? _paginationError;
    private UiPrintJob? _acceptedJob;
    private bool _updating;

    protected UiPrintDialog()
    {
        Title = "Print";
        // A preview is worth more the bigger it is, which is the reason UiDialog leaves room for
        // a dialog that may be stretched.
        CanResize = true;
    }

    /// <summary>Raised when <see cref="Settings"/> changes, whatever changed it.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? SettingsChanged;

    /// <summary>Raised when <see cref="Pages"/> is replaced by a new pagination.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? PagesChanged;

    /// <summary>The host that lists the printers and prints the job. Assigning it lists the printers again.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=2; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public IUiPrintHost? PrintHost
    {
        get => _printHost;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_printHost, value))
                return;

            _printHost = value;
            RefreshPrinters();
        }
    }

    /// <summary>
    /// What is printed. Assigning it starts the page setup from the page the document asks for
    /// and lays it out on that page.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public IBPagedDocument? Document
    {
        get => _document;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_document, value))
                return;

            _document = value;
            _paginatedFor = null;
            if (value is not null)
                Settings = Settings with { PageSetup = value.PreferredSetup };
            else
                UpdatePages();
        }
    }

    /// <summary>
    /// The choices made. Assigning settings fits them to the chosen printer - copies within its
    /// limit, black and white where it has no colour, one-sided where it cannot turn a sheet - and
    /// lays the document out again when the page setup changed.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiPrintSettings Settings
    {
        get => _settings;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_updating)
            {
                _settings = value;
                return;
            }

            _updating = true;
            try
            {
                if (!string.Equals(value.PrinterName, _settings.PrinterName, StringComparison.Ordinal) || _printers.Count == 0)
                    _capabilities = QueryCapabilities(value.PrinterName);

                UiPrintSettings fitted = Fit(value, _capabilities);
                bool changed = fitted != _settings;
                _settings = fitted;
                UpdatePages();
                if (changed)
                {
                    OnSettingsChanged();
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
                }
            }
            finally
            {
                _updating = false;
            }
        }
    }

    /// <summary>The printers <see cref="PrintHost"/> listed, the default first among equals.</summary>
    public IReadOnlyList<UiPrinterInfo> Printers => _printers;

    /// <summary>What the chosen printer can do.</summary>
    public UiPrinterCapabilities Capabilities => _capabilities;

    /// <summary>The document laid out for <see cref="Settings"/>' page setup, or null before there is one.</summary>
    public IBPageSource? Pages => _pages;

    /// <summary>The pages that would print: <see cref="Settings"/>' page ranges applied to <see cref="Pages"/>.</summary>
    public int SelectedPageCount => _pages is null ? 0 : _settings.SelectPages(_pages.PageCount).Count;

    /// <summary>
    /// Why the dialog cannot print, or null when it can: no printer, no document, a layout that
    /// failed, or a page range that selects nothing.
    /// </summary>
    public string? Problem
    {
        get
        {
            if (_printHost is null)
                return "Printing is not available here.";
            if (_printers.Count == 0 && !_printHost.ShowsSystemPrintDialog)
                return "No printers are installed.";
            if (_document is null)
                return "There is nothing to print.";
            if (_paginationError is not null)
                return "The document could not be laid out: " + _paginationError;
            if (_pages is null || _pages.PageCount == 0)
                return "The document has no pages.";
            if (SelectedPageCount == 0)
                return "The page range selects no pages.";

            return null;
        }
    }

    /// <summary>True when <see cref="AcceptPrint"/> would accept.</summary>
    public bool CanPrint => Problem is null;

    /// <summary>Lists the printers again and keeps the chosen one if it is still there.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public void RefreshPrinters()
    {
        ThrowIfDisposed();
        IReadOnlyList<UiPrinterInfo> printers;
        try
        {
            printers = _printHost?.GetPrinters() ?? [];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A spooler that will not answer leaves the user with no printer to pick, which the
            // dialog says, rather than with a dialog that would not open.
            printers = [];
        }

        _printers = printers;
        OnPrintersChanged();

        string? chosen = _settings.PrinterName;
        if (chosen is null || !ContainsPrinter(printers, chosen))
            chosen = PickDefault(printers);

        _capabilities = QueryCapabilities(chosen);
        UiPrintSettings fitted = Fit(_settings with { PrinterName = chosen }, _capabilities);
        bool changed = fitted != _settings;
        _settings = fitted;
        if (changed)
        {
            OnSettingsChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>Shows the dialog modal over <paramref name="owner"/>.</summary>
    public Task<UiDialogResult> ShowPrintModal(UiWindow owner, BRect placement = default) =>
        ShowModal(owner, placement);

    /// <summary>
    /// The job the dialog was accepted with, or null before it is. Kept past the dialog's close:
    /// a closed window is disposed, and whoever awaited the dialog reads the job afterwards.
    /// </summary>
    public UiPrintJob? AcceptedJob => _acceptedJob;

    /// <summary>
    /// Accepts the dialog when it can print, with the chosen printer's name as the result value,
    /// and keeps the job it was accepted with in <see cref="AcceptedJob"/>.
    /// </summary>
    public bool AcceptPrint()
    {
        if (!CanPrint)
            return false;

        // Taken before Accept, which closes - and so disposes - the dialog.
        _acceptedJob = CreateJob();
        if (Accept(_settings.PrinterName))
            return true;

        _acceptedJob = null;
        return false;
    }

    /// <summary>The job the current choices make, printing exactly the pages the dialog previewed.</summary>
    public UiPrintJob CreateJob()
    {
        ThrowIfDisposed();
        if (_document is null)
            throw new InvalidOperationException("There is no document to print.");

        return new UiPrintJob(_document, _settings, _pages);
    }

    /// <summary>
    /// Prints <paramref name="document"/>: through <paramref name="host"/>'s own print UI when it
    /// has one, otherwise through the dialog <paramref name="createDialog"/> makes, shown modal over
    /// <paramref name="owner"/> at <paramref name="placement"/>.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=4; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public static async Task<UiPrintResult> PrintAsync(IUiPrintHost host, IBPagedDocument document, Func<UiPrintDialog> createDialog,
        UiWindow owner, BRect placement = default, UiPrintSettings? initial = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(createDialog);
        ArgumentNullException.ThrowIfNull(owner);

        if (host.ShowsSystemPrintDialog)
        {
            UiPrintSettings settings = initial ?? new UiPrintSettings { PageSetup = document.PreferredSetup };
            return await host.PrintAsync(new UiPrintJob(document, settings), cancellationToken).ConfigureAwait(true);
        }

        UiPrintDialog dialog = createDialog();
        dialog.PrintHost = host;
        dialog.Document = document;
        // After the document, whose own page would otherwise replace the one asked for.
        if (initial is not null)
            dialog.Settings = initial;

        UiDialogResult result = await dialog.ShowModal(owner, placement).ConfigureAwait(true);
        if (result.Kind != UiDialogResultKind.Accepted || dialog.AcceptedJob is not UiPrintJob job)
            return UiPrintResult.Cancelled;

        return await host.PrintAsync(job, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Called when <see cref="Settings"/> changed, before <see cref="SettingsChanged"/>.</summary>
    protected virtual void OnSettingsChanged()
    {
    }

    /// <summary>Called when <see cref="Printers"/> was listed again.</summary>
    protected virtual void OnPrintersChanged()
    {
    }

    /// <summary>Called when <see cref="Pages"/> was replaced, before <see cref="PagesChanged"/>.</summary>
    protected virtual void OnPagesChanged()
    {
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=3; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private void UpdatePages()
    {
        if (_document is null)
        {
            ReplacePages(null, null);
            _paginatedFor = null;
            return;
        }

        if (_paginatedFor is not null && _paginatedFor == _settings.PageSetup && _pages is not null)
            return;

        try
        {
            IBPageSource pages = _document.Paginate(_settings.PageSetup);
            _paginatedFor = _settings.PageSetup;
            ReplacePages(pages, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            _paginatedFor = null;
            ReplacePages(null, exception.Message);
        }
    }

    private void ReplacePages(IBPageSource? pages, string? error)
    {
        if (ReferenceEquals(_pages, pages) && error == _paginationError)
            return;

        _pages = pages;
        _paginationError = error;
        OnPagesChanged();
        PagesChanged?.Invoke(this, EventArgs.Empty);
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=2; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private UiPrinterCapabilities QueryCapabilities(string? printer)
    {
        if (_printHost is null || string.IsNullOrEmpty(printer))
            return UiPrinterCapabilities.Unknown;

        try
        {
            return _printHost.GetCapabilities(printer) ?? UiPrinterCapabilities.Unknown;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return UiPrinterCapabilities.Unknown;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private static UiPrintSettings Fit(UiPrintSettings settings, UiPrinterCapabilities capabilities)
    {
        int maximum = Math.Clamp(capabilities.MaximumCopies, 1, UiPrintSettings.MaximumCopies);
        return settings with
        {
            Copies = Math.Min(settings.Copies, maximum),
            ColorMode = capabilities.SupportsColor ? settings.ColorMode : UiPrintColorMode.Monochrome,
            Duplex = capabilities.SupportsDuplex ? settings.Duplex : UiPrintDuplex.OneSided,
        };
    }

    private static bool ContainsPrinter(IReadOnlyList<UiPrinterInfo> printers, string name)
    {
        foreach (UiPrinterInfo printer in printers)
        {
            if (string.Equals(printer.Name, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? PickDefault(IReadOnlyList<UiPrinterInfo> printers)
    {
        foreach (UiPrinterInfo printer in printers)
        {
            if (printer.IsDefault)
                return printer.Name;
        }

        return printers.Count > 0 ? printers[0].Name : null;
    }
}
