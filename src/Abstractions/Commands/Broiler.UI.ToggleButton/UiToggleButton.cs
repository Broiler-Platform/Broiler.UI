using System;
using Broiler.UI.Button;

namespace Broiler.UI.ToggleButton;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: Toggle on a button whose IsEnabled is false changes ToggleState or raises ToggleStateChanged
// Broiler-Human:        PENDING
public abstract class UiToggleButton : UiButton
{
    private bool _isThreeState;
    private UiToggleState _toggleState;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiToggleStateChangedEventArgs>? ToggleStateChanged;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: turning IsThreeState off while ToggleState is Indeterminate leaves ToggleState Indeterminate
    // Broiler-Human:        PENDING
    public bool IsThreeState
    {
        get => _isThreeState;
        set
        {
            ThrowIfDisposed();
            if (_isThreeState == value)
                return;

            _isThreeState = value;
            if (!_isThreeState && _toggleState == UiToggleState.Indeterminate)
                SetToggleState(UiToggleState.Off);
            else
                Invalidate(UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an undefined UiToggleState value is stored instead of being refused
    // Broiler-Human:        PENDING
    public UiToggleState ToggleState
    {
        get => _toggleState;
        set
        {
            ThrowIfDisposed();
            SetToggleState(value);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: setting IsChecked to null on a button whose IsThreeState is false leaves it Indeterminate
    // Broiler-Human:        PENDING
    public bool? IsChecked
    {
        get => ToggleState switch
        {
            UiToggleState.On => true,
            UiToggleState.Indeterminate => null,
            _ => false,
        };
        set => ToggleState = value switch
        {
            true => UiToggleState.On,
            null => UiToggleState.Indeterminate,
            _ => UiToggleState.Off,
        };
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Toggle on a button whose IsEnabled is false changes ToggleState or raises ToggleStateChanged
    // Broiler-Human:        PENDING
    public bool Toggle()
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return false;

        ToggleState = ToggleState switch
        {
            UiToggleState.Off => UiToggleState.On,
            UiToggleState.On when IsThreeState => UiToggleState.Indeterminate,
            UiToggleState.On => UiToggleState.Off,
            _ => UiToggleState.Off,
        };
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a Click on an enabled toggle button leaves ToggleState unchanged
    // Broiler-Human:        PENDING
    protected override bool OnClicking(UiButtonActivationReason reason)
    {
        Toggle();
        return true;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a toggle button whose ToggleState is On produces a node without the Checked state
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ToggleButton,
            Text,
            Bounds,
            CreateToggleSemanticState(),
            []);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an Indeterminate toggle button reports the Checked semantic state
    // Broiler-Human:        PENDING
    protected UiSemanticState CreateToggleSemanticState()
    {
        UiSemanticState state = CreateSemanticState();
        if (ToggleState == UiToggleState.On)
            state |= UiSemanticState.Checked;
        if (ToggleState == UiToggleState.Indeterminate)
            state |= UiSemanticState.Indeterminate;
        return state;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an Indeterminate value is stored on a button whose IsThreeState is false
    // Broiler-Human:        PENDING
    private void SetToggleState(UiToggleState value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));

        if (value == UiToggleState.Indeterminate && !IsThreeState)
            value = UiToggleState.Off;
        if (_toggleState == value)
            return;

        UiToggleState oldState = _toggleState;
        _toggleState = value;
        ToggleStateChanged?.Invoke(this, new UiToggleStateChangedEventArgs(oldState, _toggleState));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }
}
