using System;
using System.Globalization;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.ProgressBar;

// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: a sequence of Minimum, Maximum and Value assignments leaves Value outside the Minimum to Maximum range
// Broiler-Human:        PENDING
public abstract class UiProgressBar : UiElement
{
    private double _minimum;
    private double _maximum = 100;
    private double _value;
    private bool _isIndeterminate;
    private bool _isReducedMotion;
    private bool _isDirectionReversed;
    private UiProgressBarOrientation _orientation;
    private BSize _preferredSize = new(160, 16);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiProgressBarValueChangedEventArgs>? ValueChanged;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: raising Minimum from 0 to 10 with Value at 50 raises no render invalidation, so the bar keeps drawing the old fraction
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: raising Maximum from 100 to 200 with Value at 50 raises no render invalidation, so the bar keeps drawing half full
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            ThrowIfDisposed();
            if (_isIndeterminate == value)
                return;

            _isIndeterminate = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool IsReducedMotion
    {
        get => _isReducedMotion;
        set
        {
            ThrowIfDisposed();
            if (_isReducedMotion == value)
                return;

            _isReducedMotion = value;
            Invalidate(UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
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
    // Broiler-Human:        PENDING
    public UiProgressBarOrientation Orientation
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
    // Broiler-Falsified-If: a NaN width or height passes the non-negative test and is returned from measure as the progress bar's desired size
    // Broiler-Human:        PENDING
    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred progress bar size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: with Minimum at -double.MaxValue and Maximum at double.MaxValue the fraction for Value at double.MaxValue is NaN instead of 1
    // Broiler-Human:        PENDING
    protected double NormalizedValue =>
        Maximum.Equals(Minimum) ? 0 : (Value - Minimum) / (Maximum - Minimum);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the value text for 12.5 reads '12,5' under a culture with a comma decimal separator
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ProgressBar,
            IsIndeterminate ? "Indeterminate" : Value.ToString(CultureInfo.InvariantCulture),
            Bounds,
            CreateSemanticState(),
            []);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        state |= UiSemanticState.Enabled;
        if (IsIndeterminate)
            state |= UiSemanticState.Indeterminate;
        return state;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private void CoerceCurrentValue() => SetValue(_value);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a value outside the Minimum to Maximum range is stored without being clamped to the nearer bound
    // Broiler-Human:        PENDING
    private void SetValue(double value)
    {
        ValidateFinite(value, nameof(value));
        double coerced = Math.Clamp(value, Minimum, Maximum);
        if (_value.Equals(coerced))
            return;

        double oldValue = _value;
        _value = coerced;
        ValueChanged?.Invoke(this, new UiProgressBarValueChangedEventArgs(oldValue, _value));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: NaN or an infinity passes without an ArgumentOutOfRangeException
    // Broiler-Human:        PENDING
    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Range values must be finite.");
    }
}
