using System;
using Broiler.Graphics;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Resources;

namespace Broiler.UI;

/// <summary>
/// An optional host capability: turning a document's image into a
/// <see cref="BImageHandle"/> the render list can draw. Only a host that owns a
/// renderer can do this, so controls that draw document images look for this
/// interface on <see cref="IUiHost"/> and fall back to a placeholder box when it
/// is absent.
/// </summary>
public interface IUiImageHost
{
    /// <summary>
    /// Creates a drawable image from encoded bytes (PNG, JPEG, and whatever else
    /// the backend decodes). Returns <see cref="BImageHandle.Invalid"/> when the
    /// bytes cannot be decoded — a malformed image in a document must not throw
    /// out of a render pass.
    /// </summary>
    BImageHandle CreateImage(ReadOnlySpan<byte> encodedImage);

    /// <summary>
    /// Creates a drawable image from decoded samples: straight-alpha RGBA, which
    /// is how a picture recovered from inside a container arrives. A PDF image
    /// is the case that matters, and it has no encoded bytes to hand to
    /// <see cref="CreateImage(ReadOnlySpan{byte})"/>.
    /// </summary>
    /// <remarks>
    /// The default returns <see cref="BImageHandle.Invalid"/>, so a host written
    /// before this member draws such a picture's outline, as it always did. A
    /// host whose renderer takes samples - every
    /// <c>IBroilerRenderer</c> does - overrides it, and nothing is encoded or
    /// decoded on the way.
    /// </remarks>
    BImageHandle CreateImage(BPixelBuffer pixels) => BImageHandle.Invalid;

    /// <summary>
    /// Releases a handle returned by either <see cref="CreateImage(ReadOnlySpan{byte})"/>
    /// or <see cref="CreateImage(BPixelBuffer)"/>.
    /// </summary>
    void ReleaseImage(BImageHandle image);
}
