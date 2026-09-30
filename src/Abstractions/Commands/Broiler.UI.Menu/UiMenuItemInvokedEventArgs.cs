using System;
using System.Collections.Generic;

namespace Broiler.UI.Menu;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiMenuItemInvokedEventArgs : EventArgs
{
    public UiMenuItemInvokedEventArgs(UiMenuItem item, IReadOnlyList<int> path)
    {
        Item = item;
        Path = path;
    }

    public UiMenuItem Item { get; }

    public IReadOnlyList<int> Path { get; }
}
