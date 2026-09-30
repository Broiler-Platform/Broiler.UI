using System;
using System.Collections.Generic;
using System.Linq;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.RadioButton;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
// Broiler-Falsified-If: after Select on one attached button, another attached button in the same GroupScope still reports IsChecked true
// Broiler-Human:        PENDING
public abstract class UiRadioButton : UiElement
{
    private string _text = string.Empty;
    private bool _isEnabled = true;
    private bool _isChecked;
    private bool _isApplyingGroup;
    private BSize _preferredSize = new(120, 32);
    private UiRadioGroupScope? _groupScope;
    private UiFlowDirection _flowDirection;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiRadioButtonCheckedChangedEventArgs>? CheckedChanged;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning null stores null rather than an empty string, giving the semantic node a null name
    // Broiler-Human:        PENDING
    public string Text
    {
        get => _text;
        set
        {
            ThrowIfDisposed();
            value ??= string.Empty;
            if (StringComparer.Ordinal.Equals(_text, value))
                return;

            _text = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after IsEnabled is set to false, Select returns true and the button becomes checked
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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: setting IsChecked to true on an attached button leaves a checked peer in the same GroupScope checked
    // Broiler-Human:        PENDING
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            ThrowIfDisposed();
            SetChecked(value, updateGroup: true);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: moving a checked button into a scope that already holds a checked attached button leaves both checked
    // Broiler-Human:        PENDING
    public UiRadioGroupScope? GroupScope
    {
        get => _groupScope;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_groupScope, value))
                return;

            _groupScope = value;
            if (IsChecked)
                ApplyGroupSelection();
            Invalidate(UiInvalidationKind.Semantic);
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
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred radio button size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: changing FlowDirection requests no Arrange invalidation, so the mark is drawn on the old side
    // Broiler-Human:        PENDING
    public UiFlowDirection FlowDirection
    {
        get => _flowDirection;
        set
        {
            ThrowIfDisposed();
            if (_flowDirection == value)
                return;

            _flowDirection = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: Select on a button whose IsEnabled is false returns true or sets IsChecked
    // Broiler-Human:        PENDING
    public bool Select()
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return false;

        IsChecked = true;
        return true;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the RadioButton node's name is something other than the button's current Text
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.RadioButton,
            Text,
            Bounds,
            CreateSemanticState(),
            []);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a radio button whose IsChecked is true reports a semantic state without Checked
    // Broiler-Human:        PENDING
    protected UiSemanticState CreateSemanticState()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        if (IsEnabled)
            state |= UiSemanticState.Enabled;
        if (Session?.FocusedElement == this)
            state |= UiSemanticState.Focused;
        if (IsChecked)
            state |= UiSemanticState.Checked | UiSemanticState.Selected;
        return state;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: CheckedChanged is raised when IsChecked is assigned the value it already holds
    // Broiler-Human:        PENDING
    private void SetChecked(bool value, bool updateGroup)
    {
        if (_isChecked == value)
            return;

        bool oldValue = _isChecked;
        _isChecked = value;
        CheckedChanged?.Invoke(this, new UiRadioButtonCheckedChangedEventArgs(oldValue, _isChecked));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);

        if (_isChecked && updateGroup)
            ApplyGroupSelection();
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: two buttons in one GroupScope that are both set checked before being attached to a session both stay checked once attached
    // Broiler-Human:        PENDING
    private void ApplyGroupSelection()
    {
        if (_isApplyingGroup || GroupScope is null || Session is null)
            return;

        UiRadioButton[] peers = FindRadioButtons(Session.Roots)
            .Where(peer => !ReferenceEquals(peer, this) && ReferenceEquals(peer.GroupScope, GroupScope) && peer.IsChecked)
            .ToArray();

        _isApplyingGroup = true;
        try
        {
            foreach (UiRadioButton peer in peers)
                peer.SetChecked(false, updateGroup: false);
        }
        finally
        {
            _isApplyingGroup = false;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=5; Fingerprint=TBF
    // Broiler-Falsified-If: a radio button nested inside a non-radio container below a root is not yielded
    // Broiler-Human:        PENDING
    private static IEnumerable<UiRadioButton> FindRadioButtons(IEnumerable<UiElement> roots)
    {
        foreach (UiElement root in roots)
        {
            if (root is UiRadioButton radio)
                yield return radio;

            foreach (UiRadioButton child in FindRadioButtons(root.Children))
                yield return child;
        }
    }
}
