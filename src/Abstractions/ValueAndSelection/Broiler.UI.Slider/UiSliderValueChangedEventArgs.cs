using System;

namespace Broiler.UI.Slider;

// Broiler-AI:           Origin=AI; IP=None; Security=None; Resources=0; Fingerprint=TBF
// Broiler-Human:        PENDING
public sealed class UiSliderValueChangedEventArgs : EventArgs
{
    public UiSliderValueChangedEventArgs(double oldValue, double newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public double OldValue { get; }

    public double NewValue { get; }
}
