using System;

namespace Broiler.UI.RichEdit;

/// <summary>Raised after a command runs, whether or not it changed state.</summary>
// Broiler-AI:           Origin=AI; Spec=ADR-0015; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class RichEditCommandExecutedEventArgs : EventArgs
{
    public RichEditCommandExecutedEventArgs(RichEditCommand command, bool changed)
    {
        Command = command;
        Changed = changed;
    }

    public RichEditCommand Command { get; }

    /// <summary>True when the command had an effect (edited, moved selection, or wrote the clipboard).</summary>
    public bool Changed { get; }
}
