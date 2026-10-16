using System;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Resources;

namespace Broiler.UI.AboutDialog.Standard;

/// <summary>Uploads artwork on the actual dialog host, including after native window breakout.</summary>
internal sealed class AboutDialogPicture : UiElement
{
    private BImageResource? _resource;
    private IUiImageHost? _imageHost;
    private BImageHandle _image = BImageHandle.Invalid;
    private bool _attempted;

    public string AltText { get; set; } = string.Empty;

    public BImageResource? Resource
    {
        get => _resource;
        set
        {
            if (ReferenceEquals(_resource, value)) return;
            ReleaseImage();
            _resource = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    protected override void RenderCore(UiRenderContext context)
    {
        if (_resource is null || Bounds.IsEmpty) return;
        if (!_attempted && Session?.Host is IUiImageHost host)
        {
            _imageHost = host;
            _attempted = true;
            if (_resource.TryGetPixels(out BPixelBuffer? pixels))
                _image = host.CreateImage(pixels);
            else if (_resource.TryGetEncoded(out ReadOnlyMemory<byte> bytes, out _))
                _image = host.CreateImage(bytes.Span);
        }
        if (!_image.IsValid || _image.PixelSize.IsEmpty) return;
        BSize size = _image.PixelSize;
        double scale = Math.Min(1, Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height));
        double width = size.Width * scale;
        double height = size.Height * scale;
        context.RenderList.DrawImage(_image, new BRect(0, 0, size.Width, size.Height),
            new BRect(Bounds.Left + (Bounds.Width - width) / 2,
                Bounds.Top + (Bounds.Height - height) / 2, width, height));
    }

    protected override UiSemanticNode GetSemanticNodeCore() => new(
        UiSemanticRole.ImageView, AltText, Bounds,
        Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None, []);

    protected override void OnDetached()
    {
        ReleaseImage();
        base.OnDetached();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ReleaseImage();
        base.Dispose(disposing);
    }

    private void ReleaseImage()
    {
        if (_image.IsValid) _imageHost?.ReleaseImage(_image);
        _image = BImageHandle.Invalid;
        _imageHost = null;
        _attempted = false;
    }
}
