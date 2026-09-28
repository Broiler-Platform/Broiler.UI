using System;
using System.Collections.Generic;
using Broiler.Documents.Model;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Resources;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// The backend handles of the pictures a rich edit draws, created once per image
/// object and kept until the picture leaves the document or the control leaves
/// its session.
/// </summary>
/// <remarks>
/// <para>
/// Handles belong to the renderer of the session that made them, so the cache
/// never holds a host: it asks for the current one each time, which is the one
/// that can create a handle now and the one that must release it.
/// </para>
/// <para>
/// Bytes the backend cannot decode cache as invalid, so the failure is not
/// retried on every frame. Having no image host - no session yet, or a host
/// without the capability - attempts nothing, so there is nothing to cache.
/// </para>
/// </remarks>
internal sealed class RichEditImageCache
{
    private readonly Dictionary<InlineImage, BImageHandle> _handles = new(ReferenceEqualityComparer.Instance);
    private readonly Func<IUiImageHost?> _host;

    /// <param name="host">The image capability of the session the control is in now, or null.</param>
    public RichEditImageCache(Func<IUiImageHost?> host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <summary>How many pictures have a handle, valid or not.</summary>
    public int Count => _handles.Count;

    /// <summary>The handle for a picture, creating it on first use.</summary>
    /// <remarks>
    /// With no host there is nothing to create a handle with, and that is not
    /// remembered: nothing was tried, so nothing failed. A control laid out before
    /// it joined a session - moving the selection is enough - would otherwise keep
    /// its pictures as placeholders after it had a host that could decode them.
    /// </remarks>
    public BImageHandle Resolve(InlineImage image)
    {
        if (_handles.TryGetValue(image, out BImageHandle cached))
            return cached;

        if (_host() is not IUiImageHost imageHost)
            return BImageHandle.Invalid;

        BImageHandle handle = BImageHandle.Invalid;
        if (image.Resource.TryGetPixels(out BPixelBuffer? pixels))
            handle = CreateFromSamples(imageHost, image, pixels);
        else if (!image.Data.IsEmpty)
            handle = Create(imageHost, image);

        _handles[image] = handle;
        return handle;
    }

    /// <summary>
    /// Releases every handle. Called when the control leaves its session, whose
    /// renderer the handles belong to and which could not draw them anyway.
    /// </summary>
    public void ReleaseAll()
    {
        if (_host() is IUiImageHost imageHost)
        {
            foreach (BImageHandle handle in _handles.Values)
            {
                if (handle.IsValid)
                    imageHost.ReleaseImage(handle);
            }
        }

        _handles.Clear();
    }

    /// <summary>
    /// Releases the handles of pictures <paramref name="document"/> no longer
    /// contains.
    /// </summary>
    /// <remarks>
    /// The control calls this after a layout, which is the right moment: layout
    /// runs when the document actually changed, not once per frame, and opening a
    /// second document would otherwise leave the first document's pictures
    /// uploaded for the life of the control.
    /// </remarks>
    public void ReleaseUnused(RichTextDocument document)
    {
        if (_handles.Count == 0)
            return;

        var live = new HashSet<InlineImage>(ReferenceEqualityComparer.Instance);
        foreach (RichTextParagraph paragraph in document.Paragraphs)
        {
            foreach (StyleRun run in paragraph.Runs)
            {
                if (run.Style.Image is InlineImage image)
                    live.Add(image);
            }
        }

        // A floating picture is in the document without being in a paragraph, and
        // releasing its handle here would drop the logo off every letterhead the
        // moment layout ran.
        foreach (DocumentShape shape in document.Shapes)
        {
            if (shape.Image is InlineImage image)
                live.Add(image);
        }

        List<InlineImage>? stale = null;
        foreach (KeyValuePair<InlineImage, BImageHandle> entry in _handles)
        {
            if (!live.Contains(entry.Key))
                (stale ??= []).Add(entry.Key);
        }

        if (stale is null)
            return;

        IUiImageHost? imageHost = _host();
        foreach (InlineImage image in stale)
        {
            if (_handles.Remove(image, out BImageHandle handle) && handle.IsValid)
                imageHost?.ReleaseImage(handle);
        }
    }

    /// <summary>
    /// The handle for a picture held as decoded samples, which is how a picture
    /// recovered from inside a container arrives - a PDF image is the case that
    /// matters. It has no encoded bytes, and waiting for some drew every such
    /// picture as its outline.
    /// </summary>
    /// <remarks>
    /// The samples go to the host as they are. A crop or a mask is applied to
    /// them here first, in the same managed pass an encoded picture's
    /// presentation takes, but with nothing to decode and nothing to encode
    /// again. So no codec is involved at all, and a shaped picture is shaped
    /// even where none is composed.
    /// </remarks>
    private static BImageHandle CreateFromSamples(IUiImageHost host, InlineImage image, BPixelBuffer pixels)
    {
        if (image.Presentation.IsDefault)
            return host.CreateImage(pixels);

        // Copied rather than wrapped: the resource is shared across the
        // document and immutable, and a bitmap is mutable by API.
        using var samples = new BBitmap(pixels.Width, pixels.Height, (byte[])pixels.Rgba.Clone(), takeOwnership: true);
        BBitmap presented = image.Presentation.Apply(samples);
        try
        {
            return host.CreateImage(presented.ToPixelBuffer());
        }
        finally
        {
            if (!ReferenceEquals(presented, samples))
                presented.Dispose();
        }
    }

    /// <summary>
    /// The handle for one picture, as the document presents it: cropped to the
    /// part it uses and masked to its shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A picture that states neither takes the path it always took - the host
    /// decodes the bytes it was given - so the ordinary picture costs nothing.
    /// </para>
    /// <para>
    /// One that states either is decoded here, presented, and encoded again for
    /// the host, which is a round trip through PNG that buys a great deal: the
    /// alternative is an elliptical clip in the render list, and that is a real
    /// primitive in four backends with four different answers - a stencil buffer,
    /// a layer over a geometry, per-pixel coverage, and a canvas path - where
    /// this is one managed pass. It happens once per picture, behind the cache
    /// this class exists to be, and never per frame.
    /// </para>
    /// </remarks>
    private static BImageHandle Create(IUiImageHost host, InlineImage image)
    {
        if (image.Presentation.IsDefault)
            return host.CreateImage(image.Data.Span);

        try
        {
            using BBitmap decoded = BBitmap.Decode(image.Data.Span);
            BBitmap presented = image.Presentation.Apply(decoded);
            try
            {
                return host.CreateImage(presented.Encode());
            }
            finally
            {
                if (!ReferenceEquals(presented, decoded))
                    presented.Dispose();
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException
                or NotSupportedException or FormatException)
        {
            // The picture did not decode here. The host may still manage it - it
            // composes codecs this control does not - so the bytes go on as they
            // are and the picture draws unshaped rather than not at all.
            return host.CreateImage(image.Data.Span);
        }
    }
}
