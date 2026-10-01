using System;

namespace Broiler.UI.Splitter;

/// <summary>Information about a change in a split container's division position.</summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiSplitterPositionChangedEventArgs : EventArgs
{
    public UiSplitterPositionChangedEventArgs(double oldFraction, double newFraction, double oldDistance, double newDistance)
    {
        OldFraction = oldFraction;
        NewFraction = newFraction;
        OldDistance = oldDistance;
        NewDistance = newDistance;
    }

    /// <summary>The previous normalized fraction allocated to the first pane.</summary>
    public double OldFraction { get; }

    /// <summary>The new normalized fraction allocated to the first pane.</summary>
    public double NewFraction { get; }

    /// <summary>The previous pixel/unit distance allocated to the first pane.</summary>
    public double OldDistance { get; }

    /// <summary>The new pixel/unit distance allocated to the first pane.</summary>
    public double NewDistance { get; }
}
