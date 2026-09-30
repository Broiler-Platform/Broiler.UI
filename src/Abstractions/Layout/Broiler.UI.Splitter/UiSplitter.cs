using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.Splitter;

/// <summary>A reusable normalized splitter grip; hosts apply <see cref="Value"/> to pane layout.</summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: a pointer drag or key press leaves Value non-finite or outside [Minimum, Maximum], so the host lays out its panes with a NaN or negative size
// Broiler-Human:        PENDING
public abstract class UiSplitter : UiElement
{
    private bool _isEnabled = true;
    private UiSplitterOrientation _orientation;
    private double _value = 0.5;
    private double _minimum = 0.1;
    private double _maximum = 0.9;
    private double _smallChange = 0.02;
    private double _largeChange = 0.1;
    private double _dragExtent = 400;
    private BSize _preferredSize = new(8, 8);

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiSplitterValueChangedEventArgs>? ValueChanged;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: changing IsEnabled does not invalidate Render and Semantic, so the grip keeps its old enabled look and accessible state
    // Broiler-Human:        PENDING
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value, UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: changing Orientation does not invalidate Measure, so the grip keeps the previous axis size until another layout pass runs
    // Broiler-Human:        PENDING
    public UiSplitterOrientation Orientation
    {
        get => _orientation;
        set => SetField(ref _orientation, value, UiInvalidationKind.Measure | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a NaN or infinite value is stored instead of throwing, or a finite value outside [Minimum, Maximum] is stored without being clamped
    // Broiler-Human:        PENDING
    public double Value
    {
        get => _value;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value));
            double next = Math.Clamp(value, Minimum, Maximum);
            if (_value.Equals(next))
                return;
            double old = _value;
            _value = next;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            ValueChanged?.Invoke(this, new UiSplitterValueChangedEventArgs(old, next));
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a Minimum below 0 or above Maximum is accepted, so the next Value assignment throws from Math.Clamp or leaves the normalized range
    // Broiler-Human:        PENDING
    public double Minimum
    {
        get => _minimum;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < 0 || value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value));
            _minimum = value;
            Value = _value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a Maximum above 1 or below Minimum is accepted, so Value can leave the normalized [0, 1] range the host lays panes out with
    // Broiler-Human:        PENDING
    public double Maximum
    {
        get => _maximum;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < Minimum || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            _maximum = value;
            Value = _value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a zero, negative or NaN SmallChange is stored, so the arrow keys stop moving the splitter, move it the wrong way, or throw from the Value setter
    // Broiler-Human:        PENDING
    public double SmallChange
    {
        get => _smallChange;
        set => _smallChange = Positive(value, nameof(value));
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a zero, negative or NaN LargeChange is stored, so Page Up and Page Down stop moving the splitter, move it the wrong way, or throw from the Value setter
    // Broiler-Human:        PENDING
    public double LargeChange
    {
        get => _largeChange;
        set => _largeChange = Positive(value, nameof(value));
    }

    /// <summary>Available drag distance in layout units, supplied by the composing host.</summary>
    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a DragExtent of 0 is stored, so a pointer drag divides its delta by zero and assigns an infinite Value
    // Broiler-Human:        PENDING
    public double DragExtent
    {
        get => _dragExtent;
        set => _dragExtent = Positive(value, nameof(value));
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a negative width or height is stored and reported to measure as the desired size
    // Broiler-Human:        PENDING
    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an adjustment that would pass Maximum or Minimum leaves Value beyond that bound instead of clamped to it
    // Broiler-Human:        PENDING
    protected void AdjustValue(double delta) => Value += delta;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the semantic node of a disabled splitter carries the Enabled state, or its name reports a percentage other than Value
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        string axis = Orientation == UiSplitterOrientation.Horizontal ? "horizontal" : "vertical";
        return new UiSemanticNode(
            UiSemanticRole.Splitter,
            $"Resize panes, {axis}, {Value:P0}",
            Bounds,
            state,
            []);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: assigning the value the field already holds still invalidates, or assigning a different value stores it without invalidating
    // Broiler-Human:        PENDING
    private void SetField<T>(ref T field, T value, UiInvalidationKind invalidation)
        where T : struct
    {
        ThrowIfDisposed();
        if (field.Equals(value))
            return;
        field = value;
        Invalidate(invalidation);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: zero, NaN or positive infinity is returned instead of throwing, so DragExtent can become a zero divisor for pointer drags
    // Broiler-Human:        PENDING
    private static double Positive(double value, string parameter) =>
        value > 0 && double.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameter);
}
