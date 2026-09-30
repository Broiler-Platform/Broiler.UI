using System;

namespace Broiler.UI.Edit;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiEditTextChangedEventArgs : EventArgs
{
    public UiEditTextChangedEventArgs(string oldText, string newText)
    {
        OldText = oldText;
        NewText = newText;
    }

    public string OldText { get; }

    public string NewText { get; }
}
