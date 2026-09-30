using System;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.Slider;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: after any setter or step method returns, Value lies outside the range from Minimum to Maximum
// Broiler-Human:        PENDING
public abstract class UiSlider : UiElement
{
    private double _minimum;
    private double _maximum = 100;
    private double _value;
    private double _stepFrequency = 1;
    private double _smallChange = 1;
    private double _largeChange = 10;
    private bool _isEnabled = true;
    private bool _isDirectionReversed;
    private UiSliderOrientation _orientation;
    private BSize _preferredSize = new(160, 32);

    protected UiSlider()
    {
        Focusable = true;
    }

    public override bool CanFocus => base.CanFocus && IsEnabled;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiSliderValueChangedEventArgs>? ValueChanged;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: changing Minimum while Value stays the same requests no Render invalidation, so the thumb is drawn at the old proportion of the range
    // Broiler-Human:        PENDING
    public double Minimum
    {
        get => _minimum;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum cannot exceed Maximum.");
            if (_minimum.Equals(value))
                return;

            _minimum = value;
            CoerceCurrentValue();
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: changing Maximum while Value stays the same requests no Render invalidation, so the thumb is drawn at the old proportion of the range
    // Broiler-Human:        PENDING
    public double Maximum
    {
        get => _maximum;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < Minimum)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum cannot be less than Minimum.");
            if (_maximum.Equals(value))
                return;

            _maximum = value;
            CoerceCurrentValue();
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning a value above Maximum or below Minimum stores it unclamped
    // Broiler-Human:        PENDING
    public double Value
    {
        get => _value;
        set
        {
            ThrowIfDisposed();
            SetValue(value);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a StepFrequency change that moves the current Value onto the new step grid raises no ValueChanged
    // Broiler-Human:        PENDING
    public double StepFrequency
    {
        get => _stepFrequency;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Step frequency must be non-negative.");
            if (_stepFrequency.Equals(value))
                return;

            _stepFrequency = value;
            CoerceCurrentValue();
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a negative or NaN SmallChange is stored
    // Broiler-Human:        PENDING
    public double SmallChange
    {
        get => _smallChange;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "SmallChange must be non-negative.");
            _smallChange = value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a negative or NaN LargeChange is stored
    // Broiler-Human:        PENDING
    public double LargeChange
    {
        get => _largeChange;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "LargeChange must be non-negative.");
            _largeChange = value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: setting IsEnabled to false requests no Render or Semantic invalidation, so the slider keeps drawing and reporting itself enabled
    // Broiler-Human:        PENDING
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            ThrowIfDisposed();
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: toggling IsDirectionReversed requests no Render invalidation, so the fill stays on the old side
    // Broiler-Human:        PENDING
    public bool IsDirectionReversed
    {
        get => _isDirectionReversed;
        set
        {
            ThrowIfDisposed();
            if (_isDirectionReversed == value)
                return;

            _isDirectionReversed = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an undefined UiSliderOrientation value is stored instead of being refused
    // Broiler-Human:        PENDING
    public UiSliderOrientation Orientation
    {
        get => _orientation;
        set
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_orientation == value)
                return;

            _orientation = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a size whose width or height is NaN passes the non-negative check and is stored as the preferred size
    // Broiler-Human:        PENDING
    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred slider size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: ChangeBySmallStep with a positive direction, on a slider below Maximum, leaves Value unchanged
    // Broiler-Human:        PENDING
    public void ChangeBySmallStep(int direction) => Value += Math.Sign(direction) * GetEffectiveChange(SmallChange);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: ChangeByLargeStep with a positive direction, on a slider below Maximum, leaves Value unchanged
    // Broiler-Human:        PENDING
    public void ChangeByLargeStep(int direction) => Value += Math.Sign(direction) * GetEffectiveChange(LargeChange);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a slider whose Minimum equals Maximum reports a NaN normalized value
    // Broiler-Human:        PENDING
    protected double NormalizedValue =>
        Maximum.Equals(Minimum) ? 0 : (Value - Minimum) / (Maximum - Minimum);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: with IsDirectionReversed true, a normalized position of 0 sets Value to Minimum rather than Maximum
    // Broiler-Human:        PENDING
    protected void SetValueFromNormalized(double normalized)
    {
        if (IsDirectionReversed)
            normalized = 1 - normalized;

        Value = Minimum + ((Maximum - Minimum) * Math.Clamp(normalized, 0, 1));
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the Slider node formats Value with the current culture, so 0.5 reads as 0,5 under a German culture
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Slider,
            Value.ToString(CultureInfo.InvariantCulture),
            Bounds,
            CreateSemanticState(),
            []);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a slider whose IsEnabled is false reports the Enabled semantic state
    // Broiler-Human:        PENDING
    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        return state;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: narrowing the range so the current Value falls outside it leaves Value unchanged
    // Broiler-Human:        PENDING
    private void CoerceCurrentValue() => SetValue(_value);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: ValueChanged is raised when the coerced value equals the current Value
    // Broiler-Human:        PENDING
    private void SetValue(double value)
    {
        double coerced = CoerceValue(value);
        if (_value.Equals(coerced))
            return;

        double oldValue = _value;
        _value = coerced;
        ValueChanged?.Invoke(this, new UiSliderValueChangedEventArgs(oldValue, _value));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: with a positive StepFrequency, a finite value is returned outside the range from Minimum to Maximum after step snapping
    // Broiler-Human:        PENDING
    private double CoerceValue(double value)
    {
        ValidateFinite(value, nameof(value));
        double clamped = Math.Clamp(value, Minimum, Maximum);
        if (StepFrequency > 0)
            clamped = Minimum + (Math.Round((clamped - Minimum) / StepFrequency) * StepFrequency);

        return Math.Clamp(clamped, Minimum, Maximum);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a requested change smaller than a positive StepFrequency is returned unchanged, so a keyboard step rounds back to the current Value
    // Broiler-Human:        PENDING
    private double GetEffectiveChange(double requestedChange) =>
        StepFrequency > 0 && requestedChange < StepFrequency ? StepFrequency : requestedChange;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a positive or negative infinity passes and is stored as Minimum, Maximum or Value
    // Broiler-Human:        PENDING
    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Range values must be finite.");
    }
}
