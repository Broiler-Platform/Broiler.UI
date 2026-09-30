using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Resources;

namespace Broiler.UI.ImageView;

// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
// Broiler-Human:        PENDING
public abstract class UiImageView : UiElement
{
    private BImageHandle _image = BImageHandle.Invalid;
    private BRect? _sourceRect;
    private string _altText = string.Empty;
    private UiImageStretch _stretch = UiImageStretch.Uniform;
    private double _opacity = 1;
    private BSize _preferredSize = BSize.Empty;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public BImageHandle Image
    {
        get => _image;
        set
        {
            ThrowIfDisposed();
            if (_image == value)
                return;

            _image = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a source rectangle whose width or height is NaN passes the non-negative check and is stored
    // Broiler-Human:        PENDING
    public BRect? SourceRect
    {
        get => _sourceRect;
        set
        {
            ThrowIfDisposed();
            if (value.HasValue && (value.Value.Width < 0 || value.Value.Height < 0))
                throw new ArgumentOutOfRangeException(nameof(value), "Source rectangle size must be non-negative.");
            if (_sourceRect == value)
                return;

            _sourceRect = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning null stores null rather than an empty string, giving the semantic node a null name
    // Broiler-Human:        PENDING
    public string AltText
    {
        get => _altText;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_altText, value))
                return;

            _altText = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an undefined UiImageStretch value is stored instead of being refused
    // Broiler-Human:        PENDING
    public UiImageStretch Stretch
    {
        get => _stretch;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_stretch == value)
                return;

            _stretch = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a NaN opacity, or one below 0 or above 1, is stored
    // Broiler-Human:        PENDING
    public double Opacity
    {
        get => _opacity;
        set
        {
            ThrowIfDisposed();
            if (value is < 0 or > 1 || double.IsNaN(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Opacity must be within [0, 1].");
            if (_opacity.Equals(value))
                return;

            _opacity = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a size whose width or height is NaN passes the non-negative check and is stored as the preferred size
    // Broiler-Human:        PENDING
    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred image view size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an invalid image handle reports a non-empty natural size
    // Broiler-Human:        PENDING
    protected BSize NaturalImageSize => Image.IsValid ? Image.PixelSize : BSize.Empty;

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the ImageView node's name is something other than the current AltText
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ImageView,
            AltText,
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None,
            []);
}
