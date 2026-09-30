using System;
using Broiler.Documents.Model;

namespace Broiler.UI.RichEdit;

/// <summary>Raised after the document content changes.</summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0015; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class RichEditDocumentChangedEventArgs : EventArgs
{
    public RichEditDocumentChangedEventArgs(RichTextDocument document)
    {
        Document = document;
    }

    public RichTextDocument Document { get; }
}
