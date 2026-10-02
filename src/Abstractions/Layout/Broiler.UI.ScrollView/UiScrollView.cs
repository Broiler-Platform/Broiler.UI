using System;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.ScrollView;

// Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
// Broiler-Falsified-If: with finite extent and viewport sizes, an offset below zero or beyond ExtentSize minus ViewportSize on either axis survives SetOffset, ScrollBy or a size change
// Broiler-Human:        PENDING
public abstract class UiScrollView : UiElement, IUiScrollable
{
    private BPoint _offset;
    private BSize _extentSize;
    private BSize _viewportSize;
    private BSize _preferredSize = new(160, 120);
    private double _lineScrollAmount = 32;
    private double _pageScrollFraction = 0.85;
    private UiScrollBarVisibility _horizontalScrollBarVisibility = UiScrollBarVisibility.Auto;
    private UiScrollBarVisibility _verticalScrollBarVisibility = UiScrollBarVisibility.Auto;
    private UiScrollConstraint _constraint = UiScrollConstraint.None;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public event EventHandler<UiScrollOffsetChangedEventArgs>? OffsetChanged;

    public BPoint Offset => _offset;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public double HorizontalOffset => _offset.X;

    // Broiler-AI:           Origin=AI; IP=None; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public double VerticalOffset => _offset.Y;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: shrinking the extent so the current offset exceeds the new maximum leaves Offset past that maximum
    // Broiler-Human:        PENDING
    public BSize ExtentSize
    {
        get => _extentSize;
        protected set
        {
            if (_extentSize == value)
                return;

            _extentSize = value;
            CoerceOffset();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: growing the viewport so the current offset exceeds the new maximum leaves Offset past that maximum
    // Broiler-Human:        PENDING
    public BSize ViewportSize
    {
        get => _viewportSize;
        protected set
        {
            if (_viewportSize == value)
                return;

            _viewportSize = value;
            CoerceOffset();
            Invalidate(UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a NaN width or height passes the non-negative test and is returned from measure as the scroll view's desired size
    // Broiler-Human:        PENDING
    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Preferred scroll view size must be non-negative.");
            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a negative, NaN or infinite amount is stored without an exception
    // Broiler-Human:        PENDING
    public double LineScrollAmount
    {
        get => _lineScrollAmount;
        set
        {
            ThrowIfDisposed();
            if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Line scroll amount must be a finite non-negative value.");
            _lineScrollAmount = value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: zero, a negative, NaN or infinite fraction is stored without an exception
    // Broiler-Human:        PENDING
    public double PageScrollFraction
    {
        get => _pageScrollFraction;
        set
        {
            ThrowIfDisposed();
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), "Page scroll fraction must be a finite positive value.");
            _pageScrollFraction = value;
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => _horizontalScrollBarVisibility;
        set
        {
            ThrowIfDisposed();
            ValidateScrollBarVisibility(value, nameof(value));
            if (_horizontalScrollBarVisibility == value)
                return;

            _horizontalScrollBarVisibility = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiScrollBarVisibility VerticalScrollBarVisibility
    {
        get => _verticalScrollBarVisibility;
        set
        {
            ThrowIfDisposed();
            ValidateScrollBarVisibility(value, nameof(value));
            if (_verticalScrollBarVisibility == value)
                return;

            _verticalScrollBarVisibility = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public UiScrollConstraint Constraint
    {
        get => _constraint;
        set
        {
            ThrowIfDisposed();
            if (_constraint == value)
                return;

            _constraint = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    public UiScrollConstraint ScrollConstraint
    {
        get => Constraint;
        set => Constraint = value;
    }

    public bool ConstrainContentWidth
    {
        get => Constraint == UiScrollConstraint.ConstrainWidth;
        set => Constraint = value ? UiScrollConstraint.ConstrainWidth : UiScrollConstraint.None;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: with finite sizes, a requested offset with a NaN component or one past the maximum is stored as given rather than mapped into 0 to the maximum
    // Broiler-Human:        PENDING
    public bool SetOffset(BPoint offset)
    {
        ThrowIfDisposed();
        BPoint coerced = Coerce(offset);
        if (_offset == coerced)
            return false;

        BPoint oldOffset = _offset;
        _offset = coerced;
        OffsetChanged?.Invoke(this, new UiScrollOffsetChangedEventArgs(oldOffset, _offset));
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a delta that would move past the end leaves VerticalOffset beyond MaxVerticalOffset instead of at it
    // Broiler-Human:        PENDING
    public bool ScrollBy(double horizontalDelta, double verticalDelta) =>
        SetOffset(new BPoint(HorizontalOffset + horizontalDelta, VerticalOffset + verticalDelta));

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public bool ScrollToStart() => SetOffset(BPoint.Zero);

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: after ScrollToEnd the offset differs from ExtentSize minus ViewportSize on an axis whose extent exceeds its viewport
    // Broiler-Human:        PENDING
    public bool ScrollToEnd() => SetOffset(new BPoint(MaxHorizontalOffset, MaxVerticalOffset));

    public virtual bool MakeVisible(BRect targetRect)
    {
        ThrowIfDisposed();
        if (targetRect.IsEmpty || ViewportSize.IsEmpty)
            return false;

        BRect viewport = new(Bounds.Left, Bounds.Top, ViewportSize.Width, ViewportSize.Height);

        double deltaX = 0;
        if (targetRect.Left < viewport.Left)
            deltaX = targetRect.Left - viewport.Left;
        else if (targetRect.Right > viewport.Right)
            deltaX = targetRect.Right - viewport.Right;

        double deltaY = 0;
        if (targetRect.Top < viewport.Top)
            deltaY = targetRect.Top - viewport.Top;
        else if (targetRect.Bottom > viewport.Bottom)
            deltaY = targetRect.Bottom - viewport.Bottom;

        if (deltaX != 0 || deltaY != 0)
        {
            return ScrollBy(deltaX, deltaY);
        }

        return false;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an extent narrower than the viewport yields a negative maximum horizontal offset
    // Broiler-Human:        PENDING
    protected double MaxHorizontalOffset => Math.Max(0, ExtentSize.Width - ViewportSize.Width);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: an extent shorter than the viewport yields a negative maximum vertical offset
    // Broiler-Human:        PENDING
    protected double MaxVerticalOffset => Math.Max(0, ExtentSize.Height - ViewportSize.Height);

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: under a culture with a comma decimal separator the value for offset (12.5, 3) reads '12,5,3', which cannot be split back into two coordinates
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.ScrollView,
            // Offsets are scroll state, not a name; a provider exposes them through a scroll pattern.
            string.Empty,
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible | UiSemanticState.Enabled : UiSemanticState.None,
            []);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: when the viewport and the extent grow in the same call, an offset valid for the final sizes is clamped lower because the new viewport is coerced against the previous extent first
    // Broiler-Human:        PENDING
    protected void SetViewportAndExtent(BSize viewportSize, BSize extentSize)
    {
        ViewportSize = new BSize(Math.Max(0, viewportSize.Width), Math.Max(0, viewportSize.Height));
        ExtentSize = new BSize(Math.Max(0, extentSize.Width), Math.Max(0, extentSize.Height));
    }

    // Broiler-AI:           Origin=AI; IP=None; Security=Medium; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    private void CoerceOffset() => SetOffset(_offset);

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: with finite sizes, a component beyond its axis maximum or below zero is returned unclamped
    // Broiler-Human:        PENDING
    private BPoint Coerce(BPoint offset) =>
        new(
            Math.Clamp(Normalize(offset.X), 0, MaxHorizontalOffset),
            Math.Clamp(Normalize(offset.Y), 0, MaxVerticalOffset));

    // Broiler-AI:           Origin=AI; IP=Low; Security=Medium; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: NaN, negative infinity or positive infinity is returned as a non-finite value
    // Broiler-Human:        PENDING
    private static double Normalize(double value)
    {
        if (double.IsNaN(value))
            return 0;
        if (double.IsNegativeInfinity(value))
            return 0;
        if (double.IsPositiveInfinity(value))
            return double.MaxValue;
        return value;
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a value outside Auto, Visible and Hidden, such as (UiScrollBarVisibility)3, is accepted without an exception
    // Broiler-Human:        PENDING
    private static void ValidateScrollBarVisibility(UiScrollBarVisibility value, string parameterName)
    {
        if (value is not UiScrollBarVisibility.Auto and not UiScrollBarVisibility.Visible and not UiScrollBarVisibility.Hidden)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
