using System;

namespace Broiler.UI.TabView;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiTabSelectionChangedEventArgs : EventArgs
{
    public UiTabSelectionChangedEventArgs(int oldIndex, int newIndex)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    public int OldIndex { get; }

    public int NewIndex { get; }
}
