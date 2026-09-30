using System;

namespace Broiler.UI.Window;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiWindowClosingEventArgs : EventArgs
{
    public UiWindowClosingEventArgs(UiWindowCloseReason reason)
    {
        Reason = reason;
    }

    public UiWindowCloseReason Reason { get; }

    public bool Cancel { get; set; }
}
