using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.Button;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: Click on a button whose IsEnabled is false runs OnClicking or raises Clicked
// Broiler-Human:        PENDING
public abstract class UiButton : UiElement
{
    private string _text = string.Empty;
    private string? _commandName;
    private bool _isEnabled = true;
    private bool _isDefault;
    private bool _isCancel;
    private BSize _preferredSize = new(96, 32);
    private IUiCommand? _command;
    private object? _commandParameter;

    protected UiButton()
    {
        Focusable = true;
    }

    public override bool CanFocus => base.CanFocus && IsEnabled;

    public IUiCommand? Command
    {
        get => _command;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_command, value))
                return;

            if (_command is not null)
                _command.CanExecuteChanged -= OnCommandCanExecuteChanged;

            _command = value;

            if (_command is not null)
            {
                _command.CanExecuteChanged += OnCommandCanExecuteChanged;
                if (!string.IsNullOrEmpty(_command.Label) && string.IsNullOrEmpty(Text))
                    Text = _command.Label;
                if (!string.IsNullOrEmpty(_command.TooltipText) && string.IsNullOrEmpty(ToolTipText))
                    ToolTipText = _command.TooltipText;
                IsEnabled = _command.CanExecute(_commandParameter);
            }

            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public object? CommandParameter
    {
        get => _commandParameter;
        set
        {
            ThrowIfDisposed();
            if (Equals(_commandParameter, value))
                return;

            _commandParameter = value;
            if (_command is not null)
                IsEnabled = _command.CanExecute(_commandParameter);
        }
    }

    private void OnCommandCanExecuteChanged(object? sender, EventArgs e)
    {
        if (_command is not null)
            IsEnabled = _command.CanExecute(_commandParameter);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiButtonClickEventArgs>? Clicked;

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

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public string? CommandName
    {
        get => _commandName;
        set
        {
            ThrowIfDisposed();
            if (StringComparer.Ordinal.Equals(_commandName, value))
                return;

            _commandName = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after IsEnabled is set to false, a later Click still raises Clicked
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
    // Broiler-Human:        PENDING
    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            ThrowIfDisposed();
            if (_isDefault == value)
                return;

            _isDefault = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool IsCancel
    {
        get => _isCancel;
        set
        {
            ThrowIfDisposed();
            if (_isCancel == value)
                return;

            _isCancel = value;
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
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
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred button size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: Click on a button whose IsEnabled is false runs OnClicking or raises Clicked
    // Broiler-Human:        PENDING
    public void Click(UiButtonActivationReason reason = UiButtonActivationReason.Programmatic)
    {
        ThrowIfDisposed();
        if (!IsEnabled)
            return;

        if (!OnClicking(reason))
            return;

        _command?.Execute(_commandParameter);
        Clicked?.Invoke(this, new UiButtonClickEventArgs(reason));
        Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected virtual bool OnClicking(UiButtonActivationReason reason)
    {
        return true;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the Button node's name is something other than the button's current Text
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Button,
            Text,
            Bounds,
            CreateSemanticState(),
            []);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a button whose IsEnabled is false reports the Enabled semantic state
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

    protected override void Dispose(bool disposing)
    {
        if (disposing && _command is not null)
        {
            _command.CanExecuteChanged -= OnCommandCanExecuteChanged;
            _command = null;
        }
        base.Dispose(disposing);
    }
}
