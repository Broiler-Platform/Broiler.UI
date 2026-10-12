using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Broiler.Graphics.Paging;

namespace Broiler.UI;

/// <summary>A printer the user can choose.</summary>
/// <param name="Name">The name the host prints to it by: unique within one host.</param>
/// <param name="IsDefault">True for the printer the system prints to unless told otherwise.</param>
/// <param name="Location">Where the printer is, when the host knows.</param>
/// <param name="Description">The driver's or the administrator's description, when the host knows.</param>
public sealed record UiPrinterInfo(string Name, bool IsDefault = false, string? Location = null, string? Description = null)
{
    public string Name { get; init; } = !string.IsNullOrWhiteSpace(Name) ? Name : throw new ArgumentException("A printer needs a name.", nameof(Name));
}

/// <summary>How a page is coloured on the sheet.</summary>
public enum UiPrintColorMode
{
    Color = 0,
    Monochrome = 1,
}

/// <summary>Which sides of a sheet are printed, and how the second is turned.</summary>
public enum UiPrintDuplex
{
    /// <summary>One side of each sheet.</summary>
    OneSided = 0,

    /// <summary>Both sides, turned on the long edge, as a book is read.</summary>
    LongEdge = 1,

    /// <summary>Both sides, turned on the short edge, as a notepad is flipped.</summary>
    ShortEdge = 2,
}

/// <summary>What a printer can do, as far as its host could find out.</summary>
public sealed record UiPrinterCapabilities
{
    /// <summary>
    /// What a printer the host could not query is assumed to do: print in colour on any of the
    /// catalogue's papers, one-sided, with no hardware margin.
    /// </summary>
    public static UiPrinterCapabilities Unknown { get; } = new();

    /// <summary>The papers the printer takes, in the order to offer them.</summary>
    public IReadOnlyList<BPaperSize> Papers { get; init; } = BPaperSize.Standard;

    /// <summary>The paper the printer is set to, when the host knows.</summary>
    public BPaperSize? DefaultPaper { get; init; }

    public bool SupportsColor { get; init; } = true;

    public bool SupportsDuplex { get; init; }

    /// <summary>The most copies one job may ask for.</summary>
    public int MaximumCopies { get; init; } = UiPrintSettings.MaximumCopies;

    /// <summary>The resolutions the printer prints at, in dots per inch, highest first. Empty when unknown.</summary>
    public IReadOnlyList<int> Resolutions { get; init; } = [];

    /// <summary>
    /// The edge of the sheet the printer cannot reach, in points. A page is still laid out on the
    /// whole sheet; content inside this band may be clipped by the printer.
    /// </summary>
    public BPageMargins HardMargins { get; init; } = BPageMargins.None;
}

/// <summary>
/// Every choice a print job is made with. Immutable; a dialog edits it with <c>with</c>.
/// </summary>
public sealed record UiPrintSettings
{
    /// <summary>The copies one job may ask for at most, whatever a printer says it allows.</summary>
    public const int MaximumCopies = 999;

    private readonly int _copies = 1;
    private readonly BPageSetup _pageSetup = BPageSetup.Default;
    private readonly IReadOnlyList<UiPageRange>? _pageRanges;
    private readonly double? _dpi;

    /// <summary>The printer to print to, or null for the host's default.</summary>
    public string? PrinterName { get; init; }

    public int Copies
    {
        get => _copies;
        init => _copies = value is >= 1 and <= MaximumCopies
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Copies), $"Copies must be between 1 and {MaximumCopies}.");
    }

    /// <summary>True to print each copy whole before the next; false to print every copy of a page together.</summary>
    public bool Collate { get; init; } = true;

    /// <summary>The pages to print, one-based and inclusive, or null for all of them.</summary>
    public IReadOnlyList<UiPageRange>? PageRanges
    {
        get => _pageRanges;
        init => _pageRanges = value is null || value.Count == 0 ? null : [.. value];
    }

    /// <summary>The page the document is laid out on: paper, orientation and margins.</summary>
    public BPageSetup PageSetup
    {
        get => _pageSetup;
        init => _pageSetup = value ?? throw new ArgumentNullException(nameof(PageSetup));
    }

    public UiPrintColorMode ColorMode { get; init; } = UiPrintColorMode.Color;

    public UiPrintDuplex Duplex { get; init; } = UiPrintDuplex.OneSided;

    /// <summary>The resolution to rasterize at, in dots per inch, or null for the host's choice.</summary>
    public double? Dpi
    {
        get => _dpi;
        init => _dpi = value is null || (double.IsFinite(value.Value) && value.Value > 0 && value.Value <= BPageRasterizer.MaximumDpi)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Dpi), $"Resolution must be positive and at most {BPageRasterizer.MaximumDpi} dpi.");
    }

    /// <summary>
    /// The zero-based indexes of the pages to print, in printing order, for a document of
    /// <paramref name="pageCount"/> pages. Ranges past the end are cut at it; a page named twice
    /// is printed twice, as the user asked.
    /// </summary>
    public IReadOnlyList<int> SelectPages(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);

        var pages = new List<int>();
        if (PageRanges is null)
        {
            for (int i = 0; i < pageCount; i++)
                pages.Add(i);
            return pages;
        }

        foreach (UiPageRange range in PageRanges)
        {
            for (int page = range.First; page <= Math.Min(range.Last, pageCount); page++)
                pages.Add(page - 1);
        }

        return pages;
    }
}

/// <summary>A run of pages, one-based and inclusive, as a person types them: <c>3</c>, <c>5-9</c>.</summary>
public readonly record struct UiPageRange
{
    public UiPageRange(int first, int last)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(first, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        First = first;
        Last = last;
    }

    public int First { get; }

    public int Last { get; }

    /// <summary>True when the one-based <paramref name="pageNumber"/> is in the range.</summary>
    public bool Contains(int pageNumber) => pageNumber >= First && pageNumber <= Last;

    /// <summary>
    /// Reads a page list such as <c>1-3, 5, 8-</c>: comma- or semicolon-separated pages and
    /// ranges, where a range with no end runs to the last page. Blank text is all pages and
    /// answers an empty list.
    /// </summary>
    public static bool TryParse(string? text, out IReadOnlyList<UiPageRange> ranges)
    {
        var parsed = new List<UiPageRange>();
        ranges = parsed;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        foreach (string part in text.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int dash = part.IndexOfAny(['-', '–']);
            if (dash < 0)
            {
                if (!TryPage(part, out int only))
                    return Fail(out ranges);
                parsed.Add(new UiPageRange(only, only));
                continue;
            }

            string head = part[..dash].Trim();
            string tail = part[(dash + 1)..].Trim();
            int first = 1;
            if (head.Length > 0 && !TryPage(head, out first))
                return Fail(out ranges);

            int last = int.MaxValue;
            if (tail.Length > 0 && !TryPage(tail, out last))
                return Fail(out ranges);

            if (last < first)
                return Fail(out ranges);
            parsed.Add(new UiPageRange(first, last));
        }

        return true;
    }

    /// <summary>Writes <paramref name="ranges"/> the way <see cref="TryParse"/> reads them.</summary>
    public static string Format(IEnumerable<UiPageRange>? ranges)
    {
        if (ranges is null)
            return string.Empty;

        var text = new StringBuilder();
        foreach (UiPageRange range in ranges)
        {
            if (text.Length > 0)
                text.Append(", ");

            text.Append(range.First.ToString(CultureInfo.InvariantCulture));
            if (range.Last == int.MaxValue)
                text.Append('-');
            else if (range.Last != range.First)
                text.Append('-').Append(range.Last.ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static bool TryPage(string text, out int page) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out page) && page >= 1;

    private static bool Fail(out IReadOnlyList<UiPageRange> ranges)
    {
        ranges = [];
        return false;
    }
}

/// <summary>One print job: the document, the choices it is printed with, and, when the caller has them, its pages.</summary>
public sealed class UiPrintJob
{
    /// <param name="document">What is printed; a host paginates it when <paramref name="pages"/> is null.</param>
    /// <param name="settings">The choices made for it.</param>
    /// <param name="pages">
    /// The document already laid out for <paramref name="settings"/>' page setup - the pages a
    /// print dialog previewed - so the host prints exactly what was shown without laying it
    /// out again.
    /// </param>
    public UiPrintJob(IBPagedDocument document, UiPrintSettings settings, IBPageSource? pages = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Pages = pages;
    }

    public IBPagedDocument Document { get; }

    public UiPrintSettings Settings { get; }

    /// <summary>The pages to print from, or null when the host is to paginate the document itself.</summary>
    public IBPageSource? Pages { get; }

    /// <summary>The title a spooler shows for the job.</summary>
    public string Title => Document.Title;

    /// <summary>The pages laid out for the job's page setup: <see cref="Pages"/>, or a new pagination.</summary>
    public IBPageSource ResolvePages(System.Threading.CancellationToken cancellationToken = default) =>
        Pages ?? Document.Paginate(Settings.PageSetup, cancellationToken);
}

/// <summary>How a print job ended.</summary>
public enum UiPrintOutcome
{
    /// <summary>The pages reached the spooler.</summary>
    Printed = 0,

    /// <summary>The user, or the caller's cancellation token, stopped the job.</summary>
    Cancelled = 1,

    /// <summary>The platform refused or lost the job; <see cref="UiPrintResult.Message"/> says why.</summary>
    Failed = 2,
}

/// <summary>What became of a print job.</summary>
public sealed record UiPrintResult(UiPrintOutcome Outcome, int PagesPrinted = 0, string? Message = null)
{
    public static UiPrintResult Cancelled { get; } = new(UiPrintOutcome.Cancelled);

    public static UiPrintResult Printed(int pages) => new(UiPrintOutcome.Printed, pages);

    public static UiPrintResult Failed(string message) => new(UiPrintOutcome.Failed, 0, message);
}
