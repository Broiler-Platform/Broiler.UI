using System;
using System.Globalization;
using Broiler.Graphics;

namespace Broiler.UI.SpinBox;

/// <summary>
/// A number the user can type or step: a text field with an up and a down arrow.
/// </summary>
/// <remarks>
/// <para>
/// The value model lives here and the text field lives in the implementation, because a spin box is
/// a number that happens to be edited as text rather than text that happens to parse as a number.
/// Everything that decides what the number may be — its range, its step, how many decimals it keeps
/// — is therefore answerable without a control tree, and a caller reading <see cref="Value"/> never
/// has to parse anything.
/// </para>
/// <para>
/// Values are coerced rather than rejected: a number outside the range clamps to it and a number
/// between two steps rounds to the nearest decimal the box keeps. Rejecting would leave the control
/// showing something that is not its value, and the user with nothing to do about it.
/// </para>
/// </remarks>
public abstract class UiSpinBox : UiElement
{
    private double _minimum;
    private double _maximum = 100;
    private double _value;
    private double _smallChange = 1;
    private double _largeChange = 10;
    private int _decimalPlaces;
    private bool _isEnabled = true;
    private BSize _preferredSize = new(120, 32);

    public event EventHandler<UiSpinBoxValueChangedEventArgs>? ValueChanged;

    public double Minimum
    {
        get => _minimum;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value > _maximum)
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum cannot exceed Maximum.");
            if (_minimum.Equals(value))
                return;

            _minimum = value;
            SetValue(_value);
        }
    }

    public double Maximum
    {
        get => _maximum;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value < _minimum)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum cannot be less than Minimum.");
            if (_maximum.Equals(value))
                return;

            _maximum = value;
            SetValue(_value);
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            ThrowIfDisposed();
            SetValue(value);
        }
    }

    /// <summary>What one press of an arrow, or of Up/Down, is worth.</summary>
    public double SmallChange
    {
        get => _smallChange;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Small change must be positive.");
            _smallChange = value;
        }
    }

    /// <summary>What one press of Page Up/Page Down is worth.</summary>
    public double LargeChange
    {
        get => _largeChange;
        set
        {
            ThrowIfDisposed();
            ValidateFinite(value, nameof(value));
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Large change must be positive.");
            _largeChange = value;
        }
    }

    /// <summary>
    /// The most decimals the value keeps. Zero — the default — makes the box a whole-number one,
    /// which is what a count or a page number wants. Trailing zeros are not shown: a box that keeps
    /// one decimal reads "16" and "10.5", never "16.0", because a font size box that showed the
    /// second would be reporting a precision the user did not ask for.
    /// </summary>
    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set
        {
            ThrowIfDisposed();
            if (value is < 0 or > 6)
                throw new ArgumentOutOfRangeException(nameof(value), "Decimal places must be between 0 and 6.");
            if (_decimalPlaces == value)
                return;

            _decimalPlaces = value;
            SetValue(_value);
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            ThrowIfDisposed();
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            OnEnabledChanged();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred spin box size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>The value as the box shows it.</summary>
    public string ValueText => FormatValue(_value);

    /// <summary>Steps up by <see cref="SmallChange"/>. False when the value was already at the top.</summary>
    public bool StepUp() => StepBy(1, SmallChange);

    /// <summary>Steps down by <see cref="SmallChange"/>. False when the value was already at the bottom.</summary>
    public bool StepDown() => StepBy(-1, SmallChange);

    /// <summary>Steps up by <see cref="LargeChange"/>.</summary>
    public bool PageUp() => StepBy(1, LargeChange);

    /// <summary>Steps down by <see cref="LargeChange"/>.</summary>
    public bool PageDown() => StepBy(-1, LargeChange);

    /// <summary>
    /// Takes a value the user typed. False when the text is not a number at all, which leaves the
    /// value alone — half-typed text is not a reason to move the number under the caret.
    /// </summary>
    public bool TryCommitText(string? text)
    {
        ThrowIfDisposed();
        if (!TryParseValue(text, out double parsed))
            return false;

        SetValue(parsed);
        return true;
    }

    /// <summary>
    /// Formats a value the way the box shows it: invariant, so a number a control writes is one the
    /// same control reads back whatever the machine's locale is.
    /// </summary>
    public string FormatValue(double value) =>
        Coerce(value).ToString(
            _decimalPlaces == 0 ? "0" : "0." + new string('#', _decimalPlaces),
            CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a value out of typed text. The invariant separator is tried first and the current
    /// culture's second, so a box that writes "12.5" still accepts the "12,5" a German keyboard
    /// produces.
    /// </summary>
    public static bool TryParseValue(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    /// <summary>Called when <see cref="IsEnabled"/> changes, for implementations with children to disable.</summary>
    protected virtual void OnEnabledChanged()
    {
    }

    /// <summary>Called after the value changed, before <see cref="ValueChanged"/> is raised.</summary>
    protected virtual void OnValueChanged()
    {
    }

    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.SpinBox,
            ValueText,
            Bounds,
            CreateSemanticState(),
            []);

    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this || (Session?.FocusedElement?.IsDescendantOf(this) ?? false))
            state |= UiSemanticState.Focused;
        return state;
    }

    private bool StepBy(int direction, double amount)
    {
        double stepped = Coerce(_value + (direction * amount));
        if (stepped.Equals(_value))
            return false;

        SetValue(stepped);
        return true;
    }

    private void SetValue(double value)
    {
        double coerced = Coerce(value);
        if (coerced.Equals(_value))
            return;

        double previous = _value;
        _value = coerced;
        OnValueChanged();
        ValueChanged?.Invoke(this, new UiSpinBoxValueChangedEventArgs(previous, coerced));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    /// <summary>
    /// Brings a value into range and onto the grid the box keeps. Rounding before clamping would let
    /// a value just outside the range round back inside it.
    /// </summary>
    private double Coerce(double value)
    {
        if (double.IsNaN(value))
            return _value;

        double clamped = Math.Clamp(value, _minimum, _maximum);
        return Math.Round(clamped, _decimalPlaces, MidpointRounding.AwayFromZero);
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Value must be a finite number.");
    }
}
