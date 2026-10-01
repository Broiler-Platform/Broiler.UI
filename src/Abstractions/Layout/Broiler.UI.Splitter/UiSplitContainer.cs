using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI.Splitter;

/// <summary>
/// A responsive container dividing available layout space between two panes separated by an operable splitter grip.
/// </summary>
// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=2; Fingerprint=TBF
// Broiler-Falsified-If: resizing or collapsing panes produces negative bounds, NaNs, or leaves the splitter unconstrained by minima
// Broiler-Human:        PENDING
public abstract class UiSplitContainer : UiElement
{
    private UiElement? _firstPane;
    private UiElement? _secondPane;
    private UiSplitter _splitter;
    private UiSplitterOrientation _orientation = UiSplitterOrientation.Vertical;
    private double _splitterFraction = 0.5;
    private double _firstPaneMinimumSize = 100;
    private double _secondPaneMinimumSize = 100;
    private bool _isFirstPaneCollapsed;
    private bool _isSecondPaneCollapsed;
    private bool _updatingFromContainer;
    private double _lastNetExtent = 400;
    private BSize _preferredSize = BSize.Empty;

    protected UiSplitContainer(UiSplitter splitter)
    {
        ArgumentNullException.ThrowIfNull(splitter);
        _splitter = splitter;
        _splitter.Orientation = _orientation;
        _splitter.ValueChanged += OnSplitterValueChanged;
        AddChild(_splitter);
    }

    /// <summary>Occurs when the split position or fraction changes.</summary>
    public event EventHandler<UiSplitterPositionChangedEventArgs>? SplitterPositionChanged;

    /// <summary>The first (left or top) pane.</summary>
    public UiElement? FirstPane
    {
        get => _firstPane;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_firstPane, value))
                return;

            if (_firstPane is not null)
                RemoveChild(_firstPane);

            _firstPane = value;
            if (_firstPane is not null)
                AddChild(_firstPane);

            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>The second (right or bottom) pane.</summary>
    public UiElement? SecondPane
    {
        get => _secondPane;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_secondPane, value))
                return;

            if (_secondPane is not null)
                RemoveChild(_secondPane);

            _secondPane = value;
            if (_secondPane is not null)
                AddChild(_secondPane);

            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>The splitter grip between the two panes.</summary>
    public UiSplitter Splitter
    {
        get => _splitter;
        [MemberNotNull(nameof(_splitter))]
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_splitter, value))
                return;

            if (_splitter is not null)
            {
                _splitter.ValueChanged -= OnSplitterValueChanged;
                RemoveChild(_splitter);
            }

            _splitter = value;
            _splitter.Orientation = _orientation;
            _splitter.ValueChanged += OnSplitterValueChanged;
            AddChild(_splitter);

            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>The orientation of the split: Vertical divides left/right; Horizontal divides top/bottom.</summary>
    public UiSplitterOrientation Orientation
    {
        get => _orientation;
        set
        {
            ThrowIfDisposed();
            if (_orientation == value)
                return;

            _orientation = value;
            _splitter.Orientation = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// The normalized split fraction [0..1] of net available space allocated to the first pane.
    /// </summary>
    public double SplitterFraction
    {
        get => _splitterFraction;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            double next = Math.Clamp(value, _splitter.Minimum, _splitter.Maximum);
            if (_splitterFraction.Equals(next))
                return;

            double oldFraction = _splitterFraction;
            _splitterFraction = next;

            _updatingFromContainer = true;
            try
            {
                _splitter.Value = next;
            }
            finally
            {
                _updatingFromContainer = false;
            }

            Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
            double oldDistance = oldFraction * _lastNetExtent;
            double newDistance = next * _lastNetExtent;
            SplitterPositionChanged?.Invoke(this, new UiSplitterPositionChangedEventArgs(oldFraction, next, oldDistance, newDistance));
        }
    }

    /// <summary>
    /// The distance in layout units allocated to the first pane along the split axis.
    /// </summary>
    public double SplitterDistance
    {
        get => _splitterFraction * _lastNetExtent;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_lastNetExtent > 0)
                SplitterFraction = value / _lastNetExtent;
        }
    }

    /// <summary>Minimum size of the first pane along the split axis.</summary>
    public double FirstPaneMinimumSize
    {
        get => _firstPaneMinimumSize;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_firstPaneMinimumSize.Equals(value))
                return;

            _firstPaneMinimumSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>Minimum size of the second pane along the split axis.</summary>
    public double SecondPaneMinimumSize
    {
        get => _secondPaneMinimumSize;
        set
        {
            ThrowIfDisposed();
            if (!double.IsFinite(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_secondPaneMinimumSize.Equals(value))
                return;

            _secondPaneMinimumSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>The preferred size used as a baseline when available size is unconstrained.</summary>
    public BSize PreferredSize
    {
        get => _preferredSize;
        set
        {
            ThrowIfDisposed();
            if (value.Width < 0 || value.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_preferredSize == value)
                return;

            _preferredSize = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    /// <summary>Whether the first pane is collapsed.</summary>
    public bool IsFirstPaneCollapsed
    {
        get => _isFirstPaneCollapsed;
        set
        {
            ThrowIfDisposed();
            if (_isFirstPaneCollapsed == value)
                return;

            _isFirstPaneCollapsed = value;
            if (value)
                _isSecondPaneCollapsed = false;

            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    /// <summary>Whether the second pane is collapsed.</summary>
    public bool IsSecondPaneCollapsed
    {
        get => _isSecondPaneCollapsed;
        set
        {
            ThrowIfDisposed();
            if (_isSecondPaneCollapsed == value)
                return;

            _isSecondPaneCollapsed = value;
            if (value)
                _isFirstPaneCollapsed = false;

            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public void CollapseFirstPane() => IsFirstPaneCollapsed = true;

    public void CollapseSecondPane() => IsSecondPaneCollapsed = true;

    public void RestorePanes()
    {
        ThrowIfDisposed();
        if (!_isFirstPaneCollapsed && !_isSecondPaneCollapsed)
            return;

        _isFirstPaneCollapsed = false;
        _isSecondPaneCollapsed = false;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public void ToggleFirstPane() => IsFirstPaneCollapsed = !IsFirstPaneCollapsed;

    public void ToggleSecondPane() => IsSecondPaneCollapsed = !IsSecondPaneCollapsed;

    protected override BSize MeasureCore(BSize availableSize)
    {
        if (_isFirstPaneCollapsed)
        {
            _firstPane?.Measure(BSize.Empty);
            _splitter.Measure(BSize.Empty);
            return _secondPane?.Measure(availableSize) ?? BSize.Empty;
        }

        if (_isSecondPaneCollapsed)
        {
            _secondPane?.Measure(BSize.Empty);
            _splitter.Measure(BSize.Empty);
            return _firstPane?.Measure(availableSize) ?? BSize.Empty;
        }

        bool vertical = Orientation == UiSplitterOrientation.Vertical;
        double availableExtent = vertical ? availableSize.Width : availableSize.Height;
        double cross = vertical ? availableSize.Height : availableSize.Width;
        double thickness = vertical ? _splitter.PreferredSize.Width : _splitter.PreferredSize.Height;

        double netExtent = double.IsFinite(availableExtent)
            ? Math.Max(0, availableExtent - thickness)
            : (_firstPaneMinimumSize + _secondPaneMinimumSize);

        UpdateSplitterBounds(netExtent);

        double firstExtent = Math.Round(netExtent * _splitterFraction);
        double secondExtent = Math.Max(0, netExtent - firstExtent);

        if (vertical)
        {
            BSize firstDesired = _firstPane?.Measure(new BSize(firstExtent, cross)) ?? BSize.Empty;
            BSize splitDesired = _splitter.Measure(new BSize(thickness, cross));
            BSize secondDesired = _secondPane?.Measure(new BSize(secondExtent, cross)) ?? BSize.Empty;

            double width = Math.Max(firstExtent + thickness + secondExtent, PreferredSize.Width);
            double height = Math.Max(Math.Max(firstDesired.Height, Math.Max(splitDesired.Height, secondDesired.Height)), PreferredSize.Height);
            return new BSize(Clamp(width, availableSize.Width), Clamp(height, availableSize.Height));
        }
        else
        {
            BSize firstDesired = _firstPane?.Measure(new BSize(cross, firstExtent)) ?? BSize.Empty;
            BSize splitDesired = _splitter.Measure(new BSize(cross, thickness));
            BSize secondDesired = _secondPane?.Measure(new BSize(cross, secondExtent)) ?? BSize.Empty;

            double width = Math.Max(Math.Max(firstDesired.Width, Math.Max(splitDesired.Width, secondDesired.Width)), PreferredSize.Width);
            double height = Math.Max(firstExtent + thickness + secondExtent, PreferredSize.Height);
            return new BSize(Clamp(width, availableSize.Width), Clamp(height, availableSize.Height));
        }
    }

    protected override void ArrangeCore(BRect finalRect)
    {
        if (_isFirstPaneCollapsed)
        {
            _firstPane?.Arrange(BRect.Empty);
            _splitter.Arrange(BRect.Empty);
            _secondPane?.Arrange(finalRect);
            return;
        }

        if (_isSecondPaneCollapsed)
        {
            _secondPane?.Arrange(BRect.Empty);
            _splitter.Arrange(BRect.Empty);
            _firstPane?.Arrange(finalRect);
            return;
        }

        bool vertical = Orientation == UiSplitterOrientation.Vertical;
        double extent = vertical ? finalRect.Width : finalRect.Height;
        double thickness = vertical ? _splitter.PreferredSize.Width : _splitter.PreferredSize.Height;
        double netExtent = Math.Max(0, extent - thickness);

        UpdateSplitterBounds(netExtent);

        double firstExtent = Math.Round(netExtent * _splitterFraction);
        double secondExtent = Math.Max(0, netExtent - firstExtent);

        if (vertical)
        {
            _firstPane?.Arrange(new BRect(finalRect.Left, finalRect.Top, firstExtent, finalRect.Height));
            _splitter.Arrange(new BRect(finalRect.Left + firstExtent, finalRect.Top, thickness, finalRect.Height));
            _secondPane?.Arrange(new BRect(finalRect.Left + firstExtent + thickness, finalRect.Top, secondExtent, finalRect.Height));
        }
        else
        {
            _firstPane?.Arrange(new BRect(finalRect.Left, finalRect.Top, finalRect.Width, firstExtent));
            _splitter.Arrange(new BRect(finalRect.Left, finalRect.Top + firstExtent, finalRect.Width, thickness));
            _secondPane?.Arrange(new BRect(finalRect.Left, finalRect.Top + firstExtent + thickness, finalRect.Width, secondExtent));
        }
    }

    protected override UiSemanticNode GetSemanticNodeCore()
    {
        UiSemanticState state = Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None;
        string axis = Orientation == UiSplitterOrientation.Horizontal ? "horizontal" : "vertical";
        return new UiSemanticNode(
            UiSemanticRole.Panel,
            $"Split container, {axis}, {SplitterFraction:P0}",
            Bounds,
            state,
            CreateChildSemanticNodes());
    }

    private void UpdateSplitterBounds(double netExtent)
    {
        _lastNetExtent = netExtent;
        if (netExtent <= 0)
        {
            _splitter.DragExtent = 1;
            return;
        }

        _splitter.DragExtent = netExtent;
        double totalMin = _firstPaneMinimumSize + _secondPaneMinimumSize;
        double minFraction;
        double maxFraction;

        if (totalMin > netExtent)
        {
            if (totalMin > 0)
                minFraction = maxFraction = Math.Clamp(_firstPaneMinimumSize / totalMin, 0, 1);
            else
                minFraction = maxFraction = 0.5;
        }
        else
        {
            minFraction = Math.Clamp(_firstPaneMinimumSize / netExtent, 0, 1);
            maxFraction = Math.Clamp(1.0 - (_secondPaneMinimumSize / netExtent), 0, 1);
            if (minFraction > maxFraction)
                minFraction = maxFraction = (minFraction + maxFraction) / 2;
        }

        _splitter.Minimum = minFraction;
        _splitter.Maximum = maxFraction;
        double clampedFraction = Math.Clamp(_splitterFraction, minFraction, maxFraction);
        if (!_splitterFraction.Equals(clampedFraction))
            _splitterFraction = clampedFraction;

        _updatingFromContainer = true;
        try
        {
            _splitter.Value = _splitterFraction;
        }
        finally
        {
            _updatingFromContainer = false;
        }
    }

    private void OnSplitterValueChanged(object? sender, UiSplitterValueChangedEventArgs e)
    {
        if (_updatingFromContainer)
            return;

        double oldFraction = _splitterFraction;
        double newFraction = e.NewValue;
        if (oldFraction.Equals(newFraction))
            return;

        _splitterFraction = newFraction;
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);

        double oldDistance = oldFraction * _lastNetExtent;
        double newDistance = newFraction * _lastNetExtent;
        SplitterPositionChanged?.Invoke(this, new UiSplitterPositionChangedEventArgs(oldFraction, newFraction, oldDistance, newDistance));
    }

    private IReadOnlyList<UiSemanticNode> CreateChildSemanticNodes()
    {
        if (Children.Count == 0)
            return [];

        var nodes = new System.Collections.Generic.List<UiSemanticNode>(Children.Count);
        foreach (UiElement child in Children)
        {
            if (child.Visibility != UiVisibility.Collapsed)
                nodes.Add(child.GetSemanticNode());
        }

        return nodes;
    }

    private static double Clamp(double desired, double available) =>
        double.IsInfinity(available) ? desired : Math.Min(desired, Math.Max(0, available));
}
