using System;
using Broiler.Graphics;
using Broiler.Graphics.Imaging;
using Broiler.Graphics.Resources;

namespace Broiler.UI.Window;

/// <summary>
/// A window icon. <see cref="Image"/> is what owner-drawn chrome paints in the title bar;
/// <see cref="NativePixels"/> is the optional CPU-side copy a host needs to set the taskbar and
/// Alt+Tab icon, which no drawable handle can be read back for.
/// </summary>
// Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Falsified-If: the pixels-only constructor accepts a null buffer and yields an icon with an invalid image and no native pixels
// Broiler-Human:        PENDING
public sealed class UiWindowIcon
{
    public UiWindowIcon(BImageHandle image, BPixelBuffer? nativePixels = null)
    {
        Image = image;
        NativePixels = nativePixels;
    }

    /// <summary>Creates an icon that only the native window chrome shows.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: passing a null pixel buffer produces an icon instead of throwing ArgumentNullException
    // Broiler-Human:        PENDING
    public UiWindowIcon(BPixelBuffer nativePixels)
        : this(BImageHandle.Invalid, nativePixels ?? throw new ArgumentNullException(nameof(nativePixels)))
    {
    }

    /// <summary>The drawable image owner-drawn chrome paints. May be invalid.</summary>
    public BImageHandle Image { get; }

    /// <summary>Straight-alpha RGBA pixels for the native taskbar icon, or null.</summary>
    public BPixelBuffer? NativePixels { get; }
}
