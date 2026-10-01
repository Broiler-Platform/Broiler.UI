using System;
using System.Collections.Generic;
using System.Threading;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI;

public abstract class UiElement : IDisposable, IUiFocusable
{
    private static long _nextSemanticId;
    public long SemanticId { get; } = Interlocked.Increment(ref _nextSemanticId);
    private readonly List<UiElement> _children = [];
    private UiVisibility _visibility = UiVisibility.Visible;
    private bool _isDisposed;
    private bool _isMeasureValid;
    private bool _isArrangeValid;
    private BSize? _previousAvailableSize;
    private BRect? _previousFinalRect;

    public bool IsMeasureValid => _isMeasureValid;
    public bool IsArrangeValid => _isArrangeValid;

    public UiElement? Parent { get; private set; }

    public UiSession? Session { get; private set; }

    public IReadOnlyList<UiElement> Children => _children;

    public BSize DesiredSize { get; private set; }

    public BRect Bounds { get; private set; }

    public UiVisibility Visibility
    {
        get => _visibility;
        set
        {
            ThrowIfDisposed();
            if (_visibility == value)
                return;

            _visibility = value;
            Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        }
    }

    public virtual bool Focusable { get; set; }
    public virtual bool IsTabStop { get; set; } = true;
    public virtual int TabIndex { get; set; }
    public bool IsFocused => Session?.FocusedElement == this;

    public virtual bool CanFocus
    {
        get
        {
            if (_isDisposed || Visibility != UiVisibility.Visible || Session is null || !Focusable)
                return false;

            for (UiElement? current = Parent; current is not null; current = current.Parent)
            {
                if (current.Visibility != UiVisibility.Visible)
                    return false;
            }

            return true;
        }
    }

    public bool Focus()
    {
        if (Session is null || !CanFocus)
            return false;

        Session.SetFocus(this);
        return Session.FocusedElement == this;
    }

    public void BringIntoView(BRect? targetRect = null)
    {
        ThrowIfDisposed();
        BRect rect = targetRect ?? Bounds;
        for (UiElement? current = Parent; current is not null; current = current.Parent)
        {
            if (current is IUiScrollable scrollable)
            {
                scrollable.MakeVisible(rect);
                rect = Bounds;
            }
        }
    }

    /// <summary>
    /// What a tooltip should say about this element, or empty for none.
    /// </summary>
    /// <remarks>
    /// Data only. Nothing here shows a tooltip - deciding when one appears is a session-level job
    /// and belongs to whatever is driving hover, so the element only has to carry the text. It
    /// matters most where a control has no visible caption: an icon-only toolbar loses its
    /// discoverability entirely without this, which is the whole reason the text of an icon button
    /// stays set even when the icon is what gets drawn.
    /// </remarks>
    public string ToolTipText { get; set; } = string.Empty;

    public bool IsAttached => Session is not null;

    public bool IsDisposed => _isDisposed;

    public void AddChild(UiElement child) => InsertChild(_children.Count, child);

    public void InsertChild(int index, UiElement child)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        child.ThrowIfDisposed();

        if ((uint)index > (uint)_children.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (ReferenceEquals(child, this) || IsDescendantOf(child))
            throw new InvalidOperationException("Adding the element would create a cycle.");
        if (child.Parent is not null || child.Session is not null)
            throw new InvalidOperationException("A UI element can belong to only one tree.");

        _children.Insert(index, child);
        child.Parent = this;
        child._isMeasureValid = false;
        child._isArrangeValid = false;
        if (Session is not null)
            child.AttachToSession(Session);

        OnChildAdded(child);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
    }

    public bool MoveChildToFront(UiElement child) => MoveChild(child, _children.Count - 1);

    public bool MoveChildToBack(UiElement child) => MoveChild(child, 0);

    public bool MoveChild(UiElement child, int newIndex)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        if ((uint)newIndex >= (uint)_children.Count)
            throw new ArgumentOutOfRangeException(nameof(newIndex));

        int oldIndex = _children.IndexOf(child);
        if (oldIndex < 0)
            return false;
        if (oldIndex == newIndex)
            return false;

        _children.RemoveAt(oldIndex);
        _children.Insert(newIndex, child);
        Invalidate(UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public bool RemoveChild(UiElement child)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);

        int index = _children.IndexOf(child);
        if (index < 0)
            return false;

        _children.RemoveAt(index);
        child.Session?.DetachSubtree(child);

        child.Parent = null;
        child._isMeasureValid = false;
        child._isArrangeValid = false;
        OnChildRemoved(child);
        Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange | UiInvalidationKind.Render | UiInvalidationKind.Semantic);
        return true;
    }

    public BSize Measure(BSize availableSize)
    {
        ThrowIfDisposed();
        if (_isMeasureValid && _previousAvailableSize.HasValue && availableSize == _previousAvailableSize.Value)
            return DesiredSize;

        _previousAvailableSize = availableSize;
        BSize oldDesiredSize = DesiredSize;
        DesiredSize = Visibility == UiVisibility.Collapsed ? BSize.Empty : MeasureCore(availableSize);
        _isMeasureValid = true;

        if (DesiredSize != oldDesiredSize)
        {
            _isArrangeValid = false;
        }

        return DesiredSize;
    }

    public void Arrange(BRect finalRect)
    {
        ThrowIfDisposed();
        if (_isArrangeValid && _previousFinalRect.HasValue && finalRect == _previousFinalRect.Value)
            return;

        _previousFinalRect = finalRect;
        Bounds = Visibility == UiVisibility.Collapsed ? BRect.Empty : finalRect;
        ArrangeCore(Bounds);
        _isArrangeValid = true;
    }

    public void Render(UiRenderContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);
        if (Visibility != UiVisibility.Visible)
            return;

        RenderCore(context);
    }

    /// <summary>
    /// Ground the element answers for beyond its own box, while it is showing
    /// something outside itself - a drop-down, a popup, a flyout. Empty for the
    /// elements that show nothing, which is nearly all of them.
    /// </summary>
    /// <remarks>
    /// An element's box is where it is; this is where it currently reaches. A
    /// parent routing input to its children has to be able to tell a press meant
    /// for a child's open list from a press meant to dismiss, and the two are
    /// told apart by which of them the point falls in - not by what kind of
    /// control the child happens to be, which the parent has no business
    /// knowing.
    /// </remarks>
    public virtual BRect OverlayBounds => BRect.Empty;

    public bool DispatchInput(UiInputEvent input)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(input);
        if (Visibility != UiVisibility.Visible)
            return false;

        return OnInput(input);
    }

    public void Invalidate(UiInvalidationKind kind)
    {
        if (kind == UiInvalidationKind.None || _isDisposed)
            return;

        if (kind.HasFlag(UiInvalidationKind.Measure))
        {
            _isMeasureValid = false;
            _isArrangeValid = false;
            for (UiElement? current = Parent; current is not null && current._isMeasureValid; current = current.Parent)
            {
                current._isMeasureValid = false;
                current._isArrangeValid = false;
            }
        }
        else if (kind.HasFlag(UiInvalidationKind.Arrange))
        {
            _isArrangeValid = false;
            for (UiElement? current = Parent; current is not null && current._isArrangeValid; current = current.Parent)
            {
                current._isArrangeValid = false;
            }
        }

        Session?.Invalidate(this, kind);
    }

    public void InvalidateMeasure() => Invalidate(UiInvalidationKind.Measure | UiInvalidationKind.Arrange);
    public void InvalidateArrange() => Invalidate(UiInvalidationKind.Arrange);
    public void InvalidateRender() => Invalidate(UiInvalidationKind.Render);

    public UiSemanticNode GetSemanticNode()
    {
        UiSemanticNode node = GetSemanticNodeCore();
        return node.Id == 0 ? node with { Id = SemanticId } : node;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        Dispose(disposing: true);
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    protected virtual BSize MeasureCore(BSize availableSize)
    {
        foreach (UiElement child in _children)
            child.Measure(availableSize);
        return BSize.Empty;
    }

    protected virtual void ArrangeCore(BRect finalRect)
    {
        foreach (UiElement child in _children)
            child.Arrange(finalRect);
    }

    protected virtual void RenderCore(UiRenderContext context)
    {
        foreach (UiElement child in _children)
            child.Render(context);
    }

    protected virtual bool OnInput(UiInputEvent input)
    {
        return false;
    }

    protected internal virtual bool ShouldHitTestChildren(BPoint point)
    {
        return true;
    }

    protected virtual UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Generic,
            GetType().Name,
            Bounds,
            Visibility == UiVisibility.Visible ? UiSemanticState.Visible : UiSemanticState.None,
            CreateChildSemanticNodes(),
            null,
            SemanticId);

    protected virtual void OnAttached()
    {
    }

    protected virtual void OnDetached()
    {
    }

    protected virtual void OnChildAdded(UiElement child)
    {
    }

    protected virtual void OnChildRemoved(UiElement child)
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (UiElement child in _children.ToArray())
                child.Dispose();
            _children.Clear();

            if (Parent is not null)
                Parent.RemoveChild(this);
            else
                Session?.RemoveRoot(this);
        }
    }

    internal void AttachToSession(UiSession session)
    {
        if (Session is not null)
        {
            if (Session == session)
                return;
            throw new InvalidOperationException("A UI element can attach to only one session.");
        }

        Session = session;
        OnAttached();
        foreach (UiElement child in _children)
            child.AttachToSession(session);
    }

    internal void DetachFromSession()
    {
        if (Session is null)
            return;

        foreach (UiElement child in _children)
            child.DetachFromSession();
        OnDetached();
        Session = null;
        _isMeasureValid = false;
        _isArrangeValid = false;
        _previousAvailableSize = null;
        _previousFinalRect = null;
    }

    public bool IsDescendantOf(UiElement possibleAncestor)
    {
        ArgumentNullException.ThrowIfNull(possibleAncestor);
        UiElement? current = Parent;
        while (current is not null)
        {
            if (ReferenceEquals(current, possibleAncestor))
                return true;
            current = current.Parent;
        }

        return false;
    }

    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private IReadOnlyList<UiSemanticNode> CreateChildSemanticNodes()
    {
        if (_children.Count == 0)
            return [];

        var nodes = new List<UiSemanticNode>(_children.Count);
        foreach (UiElement child in _children)
        {
            if (child.Visibility != UiVisibility.Collapsed)
                nodes.Add(child.GetSemanticNode());
        }

        return nodes;
    }
}
