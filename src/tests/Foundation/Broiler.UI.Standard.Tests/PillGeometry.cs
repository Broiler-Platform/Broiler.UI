using Broiler.Graphics.Geometry;
using Broiler.Graphics.RenderList;

namespace Broiler.UI.Standard.Tests;

/// <summary>
/// The geometry of a pill, a rectangle filled with a radius of half its shorter side as a scrollbar thumb is, and of
/// the path a ring is stroked along, for checking a stretch of ring drawn again over a thumb.
/// </summary>
internal static class PillGeometry
{
    /// <summary>How far <paramref name="point"/> lies inside the pill's outline; negative outside it.</summary>
    public static double Depth(BRect pill, BPoint point)
    {
        // A pill is every point within half its shorter side of the segment along its middle.
        double radius = Math.Min(pill.Width, pill.Height) / 2;
        double x = Math.Clamp(point.X, pill.Left + radius, pill.Right - radius);
        double y = Math.Clamp(point.Y, pill.Top + radius, pill.Bottom - radius);
        return radius - Math.Sqrt(((point.X - x) * (point.X - x)) + ((point.Y - y) * (point.Y - y)));
    }

    /// <summary>Points along the middle of the stroke of a rectangle with rounded corners, about 0.05 DIP apart.</summary>
    public static IEnumerable<BPoint> Path(BRect rect, double radius)
    {
        const double step = 0.05;
        for (double x = rect.Left + radius; x <= rect.Right - radius; x += step)
        {
            yield return new BPoint(x, rect.Top);
            yield return new BPoint(x, rect.Bottom);
        }

        for (double y = rect.Top + radius; y <= rect.Bottom - radius; y += step)
        {
            yield return new BPoint(rect.Left, y);
            yield return new BPoint(rect.Right, y);
        }

        for (double angle = 0; angle <= Math.PI / 2; angle += Math.PI / 720)
        {
            double dx = radius * Math.Cos(angle);
            double dy = radius * Math.Sin(angle);
            yield return new BPoint(rect.Right - radius + dx, rect.Top + radius - dy);
            yield return new BPoint(rect.Left + radius - dx, rect.Top + radius - dy);
            yield return new BPoint(rect.Right - radius + dx, rect.Bottom - radius + dy);
            yield return new BPoint(rect.Left + radius - dx, rect.Bottom - radius + dy);
        }
    }

    /// <summary>
    /// The clips of the strokes drawn straight after <paramref name="first"/> that match <paramref name="stroke"/>,
    /// each alone between a push and a pop of a clip.
    /// </summary>
    public static BRect[] ClipsOfRedraws<TStroke>(BRenderCommand[] commands, BRenderCommand first, Func<TStroke, bool> stroke)
        where TStroke : BRenderCommand
    {
        var clips = new List<BRect>();
        for (int index = Array.IndexOf(commands, first) + 1; index + 2 < commands.Length; index += 3)
        {
            if (commands[index] is not BRenderCommand.PushClip push || commands[index + 1] is not TStroke redraw || !stroke(redraw) || commands[index + 2] is not BRenderCommand.PopClip)
                break;

            clips.Add(push.Rect);
        }

        return clips.ToArray();
    }

    /// <summary>
    /// Every clip lies on the pill, so nothing beside its rounded ends is drawn over, and together they take in the
    /// middle of the ring's stroke wherever it lies on the pill.
    /// </summary>
    public static void AssertRedrawnOnTheThumbAlone(BRect pill, IReadOnlyList<BRect> clips, BRect ring, double ringRadius)
    {
        Assert.NotEmpty(clips);
        foreach (BRect clip in clips)
        {
            foreach (BPoint corner in new[] { new BPoint(clip.Left, clip.Top), new BPoint(clip.Right, clip.Top), new BPoint(clip.Left, clip.Bottom), new BPoint(clip.Right, clip.Bottom) })
                Assert.True(Depth(pill, corner) >= -1e-6, $"The clip {clip} reaches outside the thumb {pill} at ({corner.X}, {corner.Y}).");
        }

        int onThePill = 0;
        foreach (BPoint point in Path(ring, ringRadius))
        {
            if (Depth(pill, point) < 0.01)
                continue;

            onThePill++;
            Assert.True(
                clips.Any(clip => point.X >= clip.Left - 1e-9 && point.X <= clip.Right + 1e-9 && point.Y >= clip.Top - 1e-9 && point.Y <= clip.Bottom + 1e-9),
                $"The ring at ({point.X}, {point.Y}) lies on the thumb {pill} but is not drawn again there.");
        }

        Assert.True(onThePill > 0, "The ring does not cross the thumb.");
    }
}
