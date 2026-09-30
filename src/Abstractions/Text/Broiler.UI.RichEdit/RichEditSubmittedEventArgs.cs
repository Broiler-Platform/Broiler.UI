using System;

namespace Broiler.UI.RichEdit;

/// <summary>
/// Raised when the control is submitted (for example Enter while
/// <see cref="UiRichEdit.AcceptsReturn"/> is false).
/// </summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0015; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class RichEditSubmittedEventArgs : EventArgs
{
    public RichEditSubmittedEventArgs(string plainText)
    {
        PlainText = plainText;
    }

    public string PlainText { get; }
}
