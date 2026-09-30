using System;

namespace Broiler.UI.Button;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiButtonClickEventArgs : EventArgs
{
    public UiButtonClickEventArgs(UiButtonActivationReason reason)
    {
        Reason = reason;
    }

    public UiButtonActivationReason Reason { get; }
}
