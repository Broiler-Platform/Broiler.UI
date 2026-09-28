using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Resources;

namespace Broiler.UI.RichEdit.Standard.Tests;

/// <summary>
/// Picture handle ownership: created once per picture, remembered when a
/// picture cannot be decoded, and given back to the host that made it.
/// </summary>
public sealed class RichEditImageCacheTests
{
    private static readonly byte[] Bytes = [1, 2, 3, 4];

    private static InlineImage Picture() => new(Bytes, "image/png", 20, 10);

    private static RichTextParagraph Holding(InlineImage image) =>
        RichTextParagraph.Create(InlineImage.PlaceholderText, InlineStyle.Default with { Image = image });

    [Fact]
    public void Creates_One_Handle_Per_Picture_Object()
    {
        var host = new TestHost(new BSize(100, 100));
        var cache = new RichEditImageCache(() => host);
        InlineImage first = Picture();
        InlineImage twin = Picture();

        BImageHandle a = cache.Resolve(first);
        BImageHandle again = cache.Resolve(first);
        BImageHandle b = cache.Resolve(twin);

        // By identity, not by content: two objects with equal bytes are two
        // pictures as far as the document is concerned.
        Assert.Equal(a, again);
        Assert.NotEqual(a, b);
        Assert.Equal(2, host.CreatedImages);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Remembers_A_Picture_That_Would_Not_Decode()
    {
        var host = new TestHost(new BSize(100, 100)) { ImagePixelSize = null };
        var cache = new RichEditImageCache(() => host);
        InlineImage picture = Picture();

        Assert.False(cache.Resolve(picture).IsValid);
        Assert.False(cache.Resolve(picture).IsValid);

        Assert.Equal(1, host.CreatedImages);
    }

    [Fact]
    public void Without_An_Image_Host_No_Picture_Gets_A_Handle()
    {
        var cache = new RichEditImageCache(() => null);

        Assert.False(cache.Resolve(Picture()).IsValid);
    }

    [Fact]
    public void A_Picture_Asked_For_Before_There_Was_A_Host_Is_Decoded_Once_There_Is()
    {
        TestHost? host = null;
        var cache = new RichEditImageCache(() => host);
        InlineImage picture = Picture();
        Assert.False(cache.Resolve(picture).IsValid);

        // No host is not a failed decode: nothing was tried, so there is
        // nothing to remember.
        host = new TestHost(new BSize(100, 100));

        Assert.True(cache.Resolve(picture).IsValid);
        Assert.Equal(1, host.CreatedImages);
    }

    [Fact]
    public void Hands_Decoded_Samples_To_The_Host_As_They_Are()
    {
        var host = new TestHost(new BSize(100, 100));
        var cache = new RichEditImageCache(() => host);
        var samples = new BPixelBuffer(2, 2, new byte[16]);

        BImageHandle handle = cache.Resolve(new InlineImage(BImageResource.FromPixels(samples), width: 20, height: 20));

        Assert.True(handle.IsValid);
        Assert.Same(samples, host.LastSamples);
    }

    [Fact]
    public void Releases_Only_What_The_Document_No_Longer_Holds()
    {
        var host = new TestHost(new BSize(100, 100));
        var cache = new RichEditImageCache(() => host);
        InlineImage kept = Picture();
        InlineImage dropped = Picture();
        InlineImage floating = Picture();
        cache.Resolve(kept);
        cache.Resolve(dropped);
        cache.Resolve(floating);

        // A floating picture is in the document without being in a paragraph.
        RichTextDocument document = RichTextDocument.FromParagraphs([Holding(kept)])
            .WithShapes([new DocumentShape(0, 0, 0, 20, 20, image: floating)]);
        cache.ReleaseUnused(document);

        Assert.Equal(1, host.ReleasedImages);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Releases_Every_Handle_It_Made_When_Emptied()
    {
        var host = new TestHost(new BSize(100, 100));
        var cache = new RichEditImageCache(() => host);
        cache.Resolve(Picture());
        cache.Resolve(Picture());
        host.ImagePixelSize = null;
        cache.Resolve(Picture());

        cache.ReleaseAll();

        // The failed decode has nothing to give back.
        Assert.Equal(2, host.ReleasedImages);
        Assert.Equal(0, cache.Count);
    }
}
