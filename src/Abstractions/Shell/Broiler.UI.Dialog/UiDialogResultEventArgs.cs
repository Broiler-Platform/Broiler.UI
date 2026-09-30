using System;

namespace Broiler.UI.Dialog;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiDialogResultEventArgs : EventArgs
{
    public UiDialogResultEventArgs(UiDialogResult result)
    {
        Result = result;
    }

    public UiDialogResult Result { get; }
}
