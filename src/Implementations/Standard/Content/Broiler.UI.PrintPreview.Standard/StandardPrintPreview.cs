using System;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Paging;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Resources;
using Broiler.Graphics.Text;
using Broiler.Graphics.Windowing;
using Broiler.Input.Keyboard;
using Broiler.Input.Mouse;
using Broiler.UI.Standard;

namespace Broiler.UI.PrintPreview.Standard;

/// <summary>
/// The standard print preview: the page centred on a neutral backdrop, with a shadow under it
/// and its number below, stepped through with the wheel or PageUp, PageDown, Home and End.
/// </summary>
/// <remarks>
/// Page images are uploaded through the session host's <see cref="IUiImageHost"/> once each
/// and kept until the pages change, the host changes - a dialog that breaks out into its own
/// window gets a new one - or the preview is detached. A host that cannot take images shows
/// the page without them, which is still the page's text and layout.
/// </remarks>
public sealed class StandardPrintPreview : UiPrintPreview, IStandardThemedControl
{
    private BPageImageCache? _images;
    private IUiImageHost? _imagesHost;
    private double _wheelRemainder;
    private BRect _pageRect;

    public BColor Backdrop { get; set; } = StandardControlPaint.SurfaceDisabled;

    public BColor PaperColor { get; set; } = BColor.White;

    public BColor PaperBorder { get; set; } = StandardControlPaint.Border;

    public BColor ShadowColor { get; set; } = BColor.FromArgb(0x30, 0x00, 0x00, 0x00);

    public BColor CaptionForeground { get; set; } = StandardControlPaint.TextMuted;

    public BFontStyle CaptionFont { get; set; } = BFontStyle.Default with { Size = 12 };

    /// <summary>Space kept between the page and the edge of the view, in device-independent pixels.</summary>
    public double PagePadding { get; set; } = 16;

    /// <summary>Draws "Page 2 of 5" under the page.</summary>
    public bool ShowCaption { get; set; } = true;

    public BSize PreferredSize { get; set; } = new(320, 420);

    /// <summary>Where the page was last drawn, in view coordinates; empty when there is no page.</summary>
    public BRect PageBounds => _pageRect;

    public void ApplyTheme(StandardThemeTokens theme)
    {
        Backdrop = theme.SurfaceDisabled;
        PaperBorder = theme.Border;
        CaptionForeground = theme.TextMuted;
        Invalidate(UiInvalidationKind.Render);
    }

    /// <summary>
    /// The scale, in device-independent pixels per point, a page of <paramref name="pageSize"/>
    /// is drawn at in a view of <paramref name="viewSize"/>.
    /// </summary>
    public double GetScale(BSize pageSize, BSize viewSize)
    {
        if (pageSize.IsEmpty)
            return 0;

        double captionHeight = ShowCaption ? CaptionHeight : 0;
        double width = Math.Max(0, viewSize.Width - (PagePadding * 2));
        double height = Math.Max(0, viewSize.Height - (PagePadding * 2) - captionHeight);
        return ZoomMode switch
        {
            UiPrintPreviewZoom.FitWidth => width / pageSize.Width,
            UiPrintPreviewZoom.Custom => Zoom,
            _ => Math.Min(width / pageSize.Width, height / pageSize.Height),
        };
    }

    private double CaptionHeight => BTextMeasurer.GetLineHeight(CaptionFont) + 6;

    protected override BSize MeasureCore(BSize availableSize) =>
        new(ClampDesired(PreferredSize.Width, availableSize.Width), ClampDesired(PreferredSize.Height, availableSize.Height));

    protected override void OnPagesChanged()
    {
        // The new pages carry new images; the uploaded ones belong to pages no longer shown.
        _images?.Clear();
    }

    protected override void RenderCore(UiRenderContext context)
    {
        BRenderList list = context.RenderList;
        list.FillRect(Bounds, Backdrop);
        _pageRect = BRect.Empty;

        BPage? page = CurrentPage;
        if (page is null)
        {
            DrawCentredText(list, Bounds, PageCaption);
            return;
        }

        double scale = GetScale(page.Size, Bounds.Size);
        if (scale <= 0 || !double.IsFinite(scale))
            return;

        double pageWidth = page.Size.Width * scale;
        double pageHeight = page.Size.Height * scale;
        double captionHeight = ShowCaption ? CaptionHeight : 0;
        double left = Bounds.Left + Math.Max(PagePadding, (Bounds.Width - pageWidth) / 2);
        double top = Bounds.Top + Math.Max(PagePadding, (Bounds.Height - captionHeight - pageHeight) / 2);
        _pageRect = new BRect(left, top, pageWidth, pageHeight);

        list.PushClip(Bounds);
        list.FillRect(new BRect(left + 3, top + 3, pageWidth, pageHeight), ShadowColor);
        list.FillRect(_pageRect, PaperColor);

        list.PushClip(_pageRect);
        list.PushTransform(new BMatrix3x2(scale, 0, 0, scale, left, top));
        BPageImageCache? images = ImagesFor(context.Host);
        if (images is not null)
            images.AppendPage(page, list);
        else
            page.AppendTo(list, _ => BImageHandle.Invalid);
        list.PopTransform();
        list.PopClip();

        list.StrokeRect(_pageRect, PaperBorder, 1);
        if (ShowCaption)
            DrawCentredText(list, new BRect(Bounds.Left, top + pageHeight + 4, Bounds.Width, captionHeight), PageCaption);
        list.PopClip();

        if (Session?.FocusedElement == this)
        {
            BRect focus = new(Bounds.Left + 1, Bounds.Top + 1, Math.Max(0, Bounds.Width - 2), Math.Max(0, Bounds.Height - 2));
            list.StrokeRect(focus, StandardControlPaint.Focus, StandardControlPaint.FocusRingThickness);
        }
    }

    protected override bool OnInput(UiInputEvent input)
    {
        if (base.OnInput(input))
            return true;

        return input.Kind switch
        {
            UiInputEventKind.PointerWheel => HandleWheel(input),
            UiInputEventKind.PointerButton => HandlePointer(input),
            UiInputEventKind.KeyboardKey => HandleKeyboard(input),
            _ => false,
        };
    }

    protected override void OnDetached()
    {
        ReleaseImages();
        base.OnDetached();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ReleaseImages();
        base.Dispose(disposing);
    }

    private BPageImageCache? ImagesFor(IUiHost host)
    {
        if (host is not IUiImageHost imageHost)
        {
            ReleaseImages();
            return null;
        }

        if (!ReferenceEquals(imageHost, _imagesHost))
        {
            ReleaseImages();
            _imagesHost = imageHost;
            _images = new BPageImageCache(imageHost.CreateImage, imageHost.ReleaseImage);
        }

        return _images;
    }

    private void ReleaseImages()
    {
        _images?.Dispose();
        _images = null;
        _imagesHost = null;
    }

    private bool HandleWheel(UiInputEvent input)
    {
        if (input.WheelAxis != MouseWheelAxis.Vertical || PageCount == 0)
            return false;

        // A precision touchpad reports fractions of a notch; a page turns once a whole notch has
        // gathered, in the direction it gathered.
        _wheelRemainder += input.WheelDeltaNotches;
        bool moved = false;
        while (_wheelRemainder >= 1)
        {
            _wheelRemainder -= 1;
            moved |= PreviousPage();
        }

        while (_wheelRemainder <= -1)
        {
            _wheelRemainder += 1;
            moved |= NextPage();
        }

        return moved || PageCount > 0;
    }

    private bool HandlePointer(UiInputEvent input)
    {
        if (input.MouseButton != MouseButton.Left || input.MouseButtonTransition != MouseButtonTransition.Down)
            return false;

        Session?.SetFocus(this);
        return true;
    }

    private bool HandleKeyboard(UiInputEvent input)
    {
        if (input.KeyTransition != KeyboardKeyTransition.Down)
            return false;

        if (IsKey(input, BVirtualKey.PageDown, "PageDown") || IsKey(input, BVirtualKey.Right, "Right") || IsKey(input, BVirtualKey.Down, "Down"))
            return NextPage() || PageCount > 0;
        if (IsKey(input, BVirtualKey.PageUp, "PageUp") || IsKey(input, BVirtualKey.Left, "Left") || IsKey(input, BVirtualKey.Up, "Up"))
            return PreviousPage() || PageCount > 0;
        if (IsKey(input, BVirtualKey.Home, "Home"))
            return FirstPage() || PageCount > 0;
        if (IsKey(input, BVirtualKey.End, "End"))
            return LastPage() || PageCount > 0;

        return false;
    }

    private void DrawCentredText(BRenderList list, BRect area, string text)
    {
        if (area.IsEmpty || text.Length == 0)
            return;

        double width = BTextMeasurer.MeasureAdvance(text, CaptionFont);
        double x = area.Left + Math.Max(0, (area.Width - width) / 2);
        double y = area.Top + Math.Max(0, (area.Height - BTextMeasurer.GetLineHeight(CaptionFont)) / 2);
        list.DrawText(new BTextRun(text, CaptionFont, CaptionForeground), new BPoint(x, y));
    }

    private static bool IsKey(UiInputEvent input, int nativeKeyCode, string name) =>
        input.NativeKeyCode == nativeKeyCode ||
        string.Equals(input.KeyName, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(input.KeyName, "VirtualKey:" + nativeKeyCode.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static double ClampDesired(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
