using System;

namespace Broiler.UI.Window;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiWindowClosedEventArgs : EventArgs
{
    public UiWindowClosedEventArgs(UiWindowCloseReason reason)
    {
        Reason = reason;
    }

    public UiWindowCloseReason Reason { get; }
}
