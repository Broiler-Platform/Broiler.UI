using System;

namespace Broiler.UI.CheckBox;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiCheckStateChangedEventArgs : EventArgs
{
    public UiCheckStateChangedEventArgs(UiCheckState oldState, UiCheckState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public UiCheckState OldState { get; }

    public UiCheckState NewState { get; }
}
