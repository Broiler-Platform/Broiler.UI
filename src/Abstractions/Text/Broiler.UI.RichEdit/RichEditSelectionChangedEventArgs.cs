using System;
using Broiler.Documents.Model;

namespace Broiler.UI.RichEdit;

/// <summary>Raised after the selection changes.</summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0015; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class RichEditSelectionChangedEventArgs : EventArgs
{
    public RichEditSelectionChangedEventArgs(RichTextRange selection)
    {
        Selection = selection;
    }

    public RichTextRange Selection { get; }
}
