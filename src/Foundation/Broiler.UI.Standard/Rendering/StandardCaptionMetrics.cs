using System;
using System.Collections.Concurrent;
using System.Text;
using Broiler.Graphics.Color;
using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;
using Broiler.Graphics.Rendering;
using Broiler.Graphics.Text;

namespace Broiler.UI.Standard;

/// <summary>Visible vertical bounds for captions; editing and multiline text retain line metrics.</summary>
internal static class StandardCaptionMetrics
{
    private const double SampleSize = 64;
    private const int Baseline = 128;
    private const int Extent = 256;
    private static readonly ConcurrentDictionary<(string Text, BFontStyle Font), BTextVerticalMetrics> Cache = new();
    private static readonly ConcurrentDictionary<(int Rune, BFontStyle Font), (double Area, double Moment)> OpticalCache = new();

    public static double MeasureOpticalCenter(string text, BFontStyle font)
    {
        double area = 0, moment = 0;
        BFontStyle sampleFont = font with { Size = SampleSize };
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
                continue;
            var key = (rune.Value, sampleFont);
            if (!OpticalCache.TryGetValue(key, out var weight))
            {
                if (OpticalCache.Count >= 2048)
                    OpticalCache.Clear();
                weight = OpticalCache.GetOrAdd(key, static key => MeasureOpticalGlyph(new Rune(key.Rune).ToString(), key.Font));
            }
            area += weight.Area;
            moment += weight.Moment;
        }
        if (area > 0)
            return moment / area * font.Size / SampleSize;
        BTextVerticalMetrics metrics = BTextMeasurer.GetVerticalMetrics(font);
        return (metrics.Descent - metrics.Ascent) / 2;
    }

    private static (double Area, double Moment) MeasureOpticalGlyph(string glyph, BFontStyle font)
    {
        // The outermost descender pixels have little visual weight. Center a list
        // caption by its ink coverage so those pixels cannot pull the entire row upward.
        // Cache glyph weights, rather than captions, so changing versions or counters
        // only adds their weights and does not repeatedly rasterize the text.
        var list = new BRenderList();
        list.DrawText(new BTextRun(glyph, font, BColor.Black) { Baseline = Baseline }, new BPoint(64, 0));
        using var renderer = new BImageRenderer();
        using var bitmap = renderer.RenderToImage(list, BSurfaceDescriptor.Default(new BSize(Extent, Extent)), new BFrameContext(BColor.Transparent));
        double area = 0, moment = 0;
        for (int y = 0; y < Extent; y++)
            for (int x = 0; x < Extent; x++)
            {
                double coverage = bitmap.GetPixel(x, y).A / 255.0;
                area += coverage;
                moment += coverage * (y + 0.5 - Baseline);
            }
        return (area, moment);
    }

    public static BTextVerticalMetrics Measure(string text, BFontStyle font)
    {
        if (string.IsNullOrWhiteSpace(text) || !(font.Size > 0))
            return BTextMeasurer.GetVerticalMetrics(font);

        // Normalize the font size so zoomed captions share the same measurement. Only
        // metrics are retained, never bitmaps; changing status labels cannot grow this cache forever.
        var key = (text, font with { Size = SampleSize });
        if (!Cache.TryGetValue(key, out BTextVerticalMetrics metrics))
        {
            if (Cache.Count >= 256)
                Cache.Clear();
            metrics = Cache.GetOrAdd(key, static key => MeasureCore(key.Text, key.Font));
        }
        double scale = font.Size / SampleSize;
        return new(metrics.Ascent * scale, metrics.Descent * scale);
    }

    private static BTextVerticalMetrics MeasureCore(string text, BFontStyle font)
    {
        // A carried font exposes its outlines directly, including shaped and combining glyphs.
        if (font.Face is { } face)
        {
            double top = double.PositiveInfinity, bottom = double.NegativeInfinity;
            foreach (var contour in face.GetRunOutline(text, SampleSize, BPoint.Zero, 0))
                foreach (var point in contour)
                {
                    top = Math.Min(top, point.Y);
                    bottom = Math.Max(bottom, point.Y);
                }
            return top < bottom ? new(-top, bottom) : BTextMeasurer.GetVerticalMetrics(font);
        }

        // Graphics exposes only fixed "Hg" metrics for installed fonts. Its software
        // renderer resolves the same host font files as the native backend. Sample those
        // outlines at a 64-unit font size, rather than guessing which letters have descenders.
        // Overlay glyphs so even a long caption requires only one small temporary mask.
        var list = new BRenderList();
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (var rune in text.EnumerateRunes())
            if (!Rune.IsWhiteSpace(rune) && seen.Add(rune.Value))
                list.DrawText(new BTextRun(rune.ToString(), font, BColor.Black) { Baseline = Baseline }, new BPoint(64, 0));

        using var renderer = new BImageRenderer();
        using var bitmap = renderer.RenderToImage(list, BSurfaceDescriptor.Default(new BSize(Extent, Extent)), new BFrameContext(BColor.Transparent));
        int first = Extent, last = -1;
        for (int y = 0; y < Extent; y++)
            for (int x = 0; x < Extent; x++)
                if (bitmap.GetPixel(x, y).A > 0)
                {
                    first = Math.Min(first, y);
                    last = y;
                    break;
                }
        return first <= last ? new(Baseline - first, last + 1 - Baseline) : BTextMeasurer.GetVerticalMetrics(font);
    }
}
