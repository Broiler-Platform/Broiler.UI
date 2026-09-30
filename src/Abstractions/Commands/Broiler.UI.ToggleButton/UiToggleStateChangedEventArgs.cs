using System;

namespace Broiler.UI.ToggleButton;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiToggleStateChangedEventArgs : EventArgs
{
    public UiToggleStateChangedEventArgs(UiToggleState oldState, UiToggleState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public UiToggleState OldState { get; }

    public UiToggleState NewState { get; }
}
