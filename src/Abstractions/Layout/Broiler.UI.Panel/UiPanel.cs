using System;
using System.Collections.Generic;
using System.Linq;

namespace Broiler.UI.Panel;

// Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=4; Fingerprint=TBF
// Broiler-Falsified-If: a child removed from the panel is still held in the dock table, so re-adding it reports its old dock instead of Fill
// Broiler-Human:        PENDING
public abstract class UiPanel : UiElement
{
    private readonly Dictionary<UiElement, UiDock> _docks = [];
    private UiPanelLayoutMode _layoutMode;
    private UiStackOrientation _stackOrientation;
    private double _spacing;

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiPanelLayoutMode LayoutMode
    {
        get => _layoutMode;
        set
        {
            ThrowIfDisposed();
            if (_layoutMode == value)
                return;

            _layoutMode = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Human:        PENDING
    public UiStackOrientation StackOrientation
    {
        get => _stackOrientation;
        set
        {
            ThrowIfDisposed();
            if (_stackOrientation == value)
                return;

            _stackOrientation = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=1; Fingerprint=TBF
    // Broiler-Falsified-If: a NaN spacing passes the non-negative test and turns the measured stack size into NaN
    // Broiler-Human:        PENDING
    public double Spacing
    {
        get => _spacing;
        set
        {
            ThrowIfDisposed();
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Panel spacing must be non-negative.");
            if (_spacing.Equals(value))
                return;

            _spacing = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
        }
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=3; Fingerprint=TBF
    // Broiler-Falsified-If: dock metadata is stored for an element that is not a child of this panel
    // Broiler-Human:        PENDING
    public void SetDock(UiElement child, UiDock dock)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        if (!Children.Contains(child))
            throw new InvalidOperationException("Dock metadata can only be assigned to a child of this panel.");

        _docks[child] = dock;
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: a child with no dock assigned reports anything other than Fill
    // Broiler-Human:        PENDING
    public UiDock GetDock(UiElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return _docks.TryGetValue(child, out UiDock dock) ? dock : UiDock.Fill;
    }

    // Broiler-AI:           Origin=AI; Spec=ADR-0008; IP=Low; Security=Low; Resources=4; Fingerprint=TBF
    // Broiler-Human:        PENDING
    protected override UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Panel,
            GetType().Name,
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None,
            CreateChildSemanticNodes());

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=0; Fingerprint=TBF
    // Broiler-Falsified-If: GetDock returns a removed child's old dock instead of Fill after RemoveChild
    // Broiler-Human:        PENDING
    protected override void OnChildRemoved(UiElement child)
    {
        _docks.Remove(child);
    }

    // Broiler-AI:           Origin=AI; IP=Low; Security=Low; Resources=4; Fingerprint=TBF
    // Broiler-Falsified-If: a Collapsed child appears among the panel's semantic children
    // Broiler-Human:        PENDING
    private IReadOnlyList<UiSemanticNode> CreateChildSemanticNodes()
    {
        if (Children.Count == 0)
            return [];

        var nodes = new List<UiSemanticNode>(Children.Count);
        foreach (UiElement child in Children)
        {
            if (child.Visibility != UiVisibility.Collapsed)
                nodes.Add(child.GetSemanticNode());
        }

        return nodes;
    }
}
