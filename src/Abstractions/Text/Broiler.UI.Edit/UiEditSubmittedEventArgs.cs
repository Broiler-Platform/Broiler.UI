using System;

namespace Broiler.UI.Edit;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiEditSubmittedEventArgs : EventArgs
{
    public UiEditSubmittedEventArgs(string text)
    {
        Text = text;
    }

    public string Text { get; }
}
