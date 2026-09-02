using System;

namespace Broiler.UI.SpinBox;

public sealed class UiSpinBoxValueChangedEventArgs : EventArgs
{
    public UiSpinBoxValueChangedEventArgs(double oldValue, double newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public double OldValue { get; }

    public double NewValue { get; }
}
