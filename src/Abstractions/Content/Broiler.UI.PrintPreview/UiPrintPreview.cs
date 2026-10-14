using System;
using System.Globalization;
using Broiler.Graphics.Paging;

namespace Broiler.UI.PrintPreview;

/// <summary>How a print preview sizes the page it shows.</summary>
public enum UiPrintPreviewZoom
{
    /// <summary>The whole page fits the view.</summary>
    FitPage = 0,

    /// <summary>The page fills the view's width.</summary>
    FitWidth = 1,

    /// <summary>The page is drawn at <see cref="UiPrintPreview.Zoom"/> device-independent pixels per point.</summary>
    Custom = 2,
}

/// <summary>
/// Shows the pages of a laid-out document, one at a time, as they will print.
/// </summary>
/// <remarks>
/// <para>
/// A preview is a replay, not a picture: it draws each page's own commands, scaled into the
/// view, so it is sharp at any zoom and costs nothing to rasterize. The pages it shows are
/// the ones a print job sends, which is the point of showing them.
/// </para>
/// <para>
/// <see cref="PageIndex"/> counts from zero, as <see cref="IBPageSource"/> does; the
/// accessible name and any caption a standard implementation draws count from one, as a
/// reader does.
/// </para>
/// </remarks>
// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=2; Fingerprint=TBF
// Broiler-Human:        PENDING
public abstract class UiPrintPreview : UiElement
{
    /// <summary>The smallest zoom a preview accepts: a tenth of a point per pixel.</summary>
    public const double MinimumZoom = 0.05;

    /// <summary>The largest zoom a preview accepts.</summary>
    public const double MaximumZoom = 16;

    private IBPageSource? _pages;
    private int _pageIndex;
    private UiPrintPreviewZoom _zoomMode = UiPrintPreviewZoom.FitPage;
    private double _zoom = 1;

    protected UiPrintPreview()
    {
        Focusable = true;
    }

    /// <summary>Raised when <see cref="PageIndex"/> changes, including when new pages reset it.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler? PageIndexChanged;

    /// <summary>
    /// The pages to show, or null for none. Assigning new pages keeps the page index where it
    /// can - a preview re-paginated for a new paper size stays near the page the user was on -
    /// and moves it onto the last page when the new pagination is shorter.
    /// </summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=2; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public IBPageSource? Pages
    {
        get => _pages;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_pages, value))
                return;

            _pages = value;
            OnPagesChanged();
            SetPageIndex(Math.Clamp(_pageIndex, 0, Math.Max(0, PageCount - 1)), force: true);
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>The number of pages there are to show.</summary>
    public int PageCount => _pages?.PageCount ?? 0;

    /// <summary>The page shown, counting from zero. Kept within the pages there are.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public int PageIndex
    {
        get => _pageIndex;
        set
        {
            ThrowIfDisposed();
            SetPageIndex(Math.Clamp(value, 0, Math.Max(0, PageCount - 1)), force: false);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiPrintPreviewZoom ZoomMode
    {
        get => _zoomMode;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_zoomMode == value)
                return;

            _zoomMode = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    /// <summary>Pixels per point when <see cref="ZoomMode"/> is <see cref="UiPrintPreviewZoom.Custom"/>.</summary>
    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public double Zoom
    {
        get => _zoom;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < MinimumZoom || value > MaximumZoom)
                throw new ArgumentOutOfRangeException(nameof(value), $"Zoom must be between {MinimumZoom} and {MaximumZoom}.");
            if (_zoom.Equals(value))
                return;

            _zoom = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    /// <summary>The page shown, or null when there is none.</summary>
    protected BPage? CurrentPage => PageCount == 0 ? null : _pages!.GetPage(_pageIndex);

    /// <summary>Shows the next page; false when already on the last.</summary>
    public bool NextPage() => MoveTo(_pageIndex + 1);

    /// <summary>Shows the previous page; false when already on the first.</summary>
    public bool PreviousPage() => MoveTo(_pageIndex - 1);

    /// <summary>Shows the first page.</summary>
    public bool FirstPage() => MoveTo(0);

    /// <summary>Shows the last page.</summary>
    public bool LastPage() => MoveTo(PageCount - 1);

    /// <summary>"Page 2 of 5", or "No pages": the caption and the accessible name.</summary>
    public string PageCaption => PageCount == 0
        ? "No pages"
        : string.Format(CultureInfo.CurrentCulture, "Page {0} of {1}", _pageIndex + 1, PageCount);

    /// <summary>Called when <see cref="Pages"/> is replaced, before the page index is fitted to them.</summary>
    protected virtual void OnPagesChanged()
    {
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ImageView,
            PageCaption,
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None,
            []);

    private bool MoveTo(int index)
    {
        ThrowIfDisposed();
        if (index < 0 || index >= PageCount || index == _pageIndex)
            return false;

        SetPageIndex(index, force: false);
        return true;
    }

    private void SetPageIndex(int index, bool force)
    {
        if (!force && index == _pageIndex)
            return;

        bool changed = index != _pageIndex;
        _pageIndex = index;
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        if (changed || force)
            PageIndexChanged?.Invoke(this, EventArgs.Empty);
    }
}
