using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.ScrollView;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiScrollOffsetChangedEventArgs : EventArgs
{
    public UiScrollOffsetChangedEventArgs(BPoint oldOffset, BPoint newOffset)
    {
        OldOffset = oldOffset;
        NewOffset = newOffset;
    }

    public BPoint OldOffset { get; }

    public BPoint NewOffset { get; }
}
