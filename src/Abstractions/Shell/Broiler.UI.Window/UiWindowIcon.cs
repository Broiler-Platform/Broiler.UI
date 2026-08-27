using System;
using Broiler.Graphics;

namespace Broiler.UI.Window;

/// <summary>
/// A window icon. <see cref="Image"/> is what owner-drawn chrome paints in the title bar;
/// <see cref="NativePixels"/> is the optional CPU-side copy a host needs to set the taskbar and
/// Alt+Tab icon, which no drawable handle can be read back for.
/// </summary>
public sealed class UiWindowIcon
{
    public UiWindowIcon(BImageHandle image, BPixelBuffer? nativePixels = null)
    {
        Image = image;
        NativePixels = nativePixels;
    }

    /// <summary>Creates an icon that only the native window chrome shows.</summary>
    public UiWindowIcon(BPixelBuffer nativePixels)
        : this(BImageHandle.Invalid, nativePixels ?? throw new ArgumentNullException(nameof(nativePixels)))
    {
    }

    /// <summary>The drawable image owner-drawn chrome paints. May be invalid.</summary>
    public BImageHandle Image { get; }

    /// <summary>Straight-alpha RGBA pixels for the native taskbar icon, or null.</summary>
    public BPixelBuffer? NativePixels { get; }
}
