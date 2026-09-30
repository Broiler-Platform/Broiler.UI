using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;
using Broiler.UI.Window;

namespace Broiler.UI.Tooltip;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: an open tooltip stays open after an input event reaches it or after InitialDelay plus DismissAfter has elapsed
// Broiler-Human:        PENDING
public abstract class UiTooltip : UiWindow
{
    private string _text = string.Empty;
    private BRect _targetBounds = BRect.Empty;
    private TimeSpan _initialDelay = TimeSpan.FromMilliseconds(500);
    private TimeSpan? _dismissAfter = TimeSpan.FromSeconds(8);
    private TimeSpan? _requestedAt;
    private bool _isTooltipOpen;
    private BRect _tooltipBounds = BRect.Empty;

    // Broiler-AI:           Origin=AI; Spec=ADR-0026; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a new tooltip reports a BreakOutMode other than Manual or a Chrome other than None
    // Broiler-Human:        PENDING
    protected UiTooltip()
    {
        // A tooltip is a transient overlay positioned against its target, not a window the user
        // manages: it never breaks out into an OS window and never draws a title bar.
        BreakOutMode = UiWindowBreakOutMode.Manual;
        Chrome = UiWindowChrome.None;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: assigning null makes Text return null instead of an empty string
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
    // Broiler-Falsified-If: a change of target bounds schedules no arrange pass, so the tooltip stays placed against the previous target
    // Broiler-Human:        PENDING
    public BRect TargetBounds
    {
        get => _targetBounds;
        private set
        {
            if (_targetBounds == value)
                return;

            _targetBounds = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a negative delay is stored instead of throwing ArgumentOutOfRangeException
    // Broiler-Human:        PENDING
    public TimeSpan InitialDelay
    {
        get => _initialDelay;
        set
        {
            ThrowIfDisposed();
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "Tooltip delay must be non-negative.");
            _initialDelay = value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a negative timeout is stored instead of throwing ArgumentOutOfRangeException
    // Broiler-Human:        PENDING
    public TimeSpan? DismissAfter
    {
        get => _dismissAfter;
        set
        {
            ThrowIfDisposed();
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "Tooltip timeout must be non-negative.");
            _dismissAfter = value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a change of the open state schedules no render pass, so a closed tooltip stays painted
    // Broiler-Human:        PENDING
    public bool IsTooltipOpen
    {
        get => _isTooltipOpen;
        private set
        {
            if (_isTooltipOpen == value)
                return;

            _isTooltipOpen = value;
            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public BRect TooltipBounds
    {
        get => _tooltipBounds;
        protected set => _tooltipBounds = value;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: calling Start while the tooltip is open leaves IsTooltipOpen true before the new delay has elapsed
    // Broiler-Human:        PENDING
    public void Start(BRect targetBounds)
    {
        ThrowIfDisposed();
        TargetBounds = targetBounds;
        _requestedAt = Session?.Clock.Now.Elapsed ?? TimeSpan.Zero;
        IsTooltipOpen = false;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after Hide, a later UpdateVisibility call opens the tooltip again without a new Start
    // Broiler-Human:        PENDING
    public void Hide()
    {
        ThrowIfDisposed();
        _requestedAt = null;
        IsTooltipOpen = false;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: with DismissAfter set to TimeSpan.MaxValue and a non-zero InitialDelay, a call after Start throws OverflowException instead of keeping the tooltip open
    // Broiler-Human:        PENDING
    public bool UpdateVisibility()
    {
        ThrowIfDisposed();
        if (_requestedAt is null)
            return false;

        TimeSpan elapsed = (Session?.Clock.Now.Elapsed ?? TimeSpan.Zero) - _requestedAt.Value;
        if (DismissAfter is not null && elapsed >= InitialDelay + DismissAfter.Value)
        {
            Hide();
            return true;
        }

        if (!IsTooltipOpen && elapsed >= InitialDelay)
        {
            IsTooltipOpen = true;
            return true;
        }

        return false;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: an input event delivered to an open tooltip leaves IsTooltipOpen true
    // Broiler-Human:        PENDING
    protected override bool OnInput(UiInputEvent input)
    {
        Hide();
        return false;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: the semantic node carries the tooltip Text while IsTooltipOpen is false
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Tooltip,
            IsTooltipOpen ? Text : string.Empty,
            TooltipBounds,
            IsTooltipOpen && Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None,
            []);
}
