using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.RichEdit.Standard;

/// <summary>
/// Tells a double-click from two clicks: the second press has to come soon after
/// the first and land close to it.
/// </summary>
internal sealed class RichEditClickTracker
{
    /// <summary>How soon the second press has to follow the first.</summary>
    public static readonly TimeSpan DoubleClickTime = TimeSpan.FromMilliseconds(400);

    /// <summary>How far, on either axis, the second press may land from the first.</summary>
    public const double DoubleClickDistance = 4;

    private UiTimestamp _lastTime;
    private BPoint _lastPosition;
    private bool _hasClicked;

    /// <summary>Whether a press at <paramref name="point"/> now is the second of a double-click.</summary>
    public bool IsDoubleClick(UiTimestamp now, BPoint point)
    {
        if (!_hasClicked)
            return false;

        TimeSpan delta = now.Elapsed - _lastTime.Elapsed;
        bool quick = delta >= TimeSpan.Zero && delta <= DoubleClickTime;
        bool near = Math.Abs(point.X - _lastPosition.X) <= DoubleClickDistance &&
                    Math.Abs(point.Y - _lastPosition.Y) <= DoubleClickDistance;
        return quick && near;
    }

    /// <summary>Remembers a press, for the next one to be measured against.</summary>
    public void Record(UiTimestamp time, BPoint point)
    {
        _lastTime = time;
        _lastPosition = point;
        _hasClicked = true;
    }
}
