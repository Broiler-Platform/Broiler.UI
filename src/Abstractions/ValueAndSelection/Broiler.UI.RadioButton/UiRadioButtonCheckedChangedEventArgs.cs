using System;

namespace Broiler.UI.RadioButton;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiRadioButtonCheckedChangedEventArgs : EventArgs
{
    public UiRadioButtonCheckedChangedEventArgs(bool oldValue, bool newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public bool OldValue { get; }

    public bool NewValue { get; }
}
