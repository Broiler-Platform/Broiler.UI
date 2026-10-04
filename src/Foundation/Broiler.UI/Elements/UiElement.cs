using System;
using System.Collections.Generic;
using System.Threading;
using Broiler.Graphics;
using Broiler.Graphics.Geometry;

namespace Broiler.UI;

public abstract class UiElement : IDisposable, IUiFocusable
{
    private static long _nextSemanticId;
    // Above zero while a related element's name is read (see RelatedName).
    [ThreadStatic]
    private static int _relationDepth;
    public long SemanticId { get; } = Interlocked.Increment(ref _nextSemanticId);
    private readonly List<UiElement> _children = [];
    private UiVisibility _visibility = UiVisibility.Visible;
    private bool _isDisposed;
    private bool _isDisposing;
    private string? _accessibleName;
    private UiElement? _labeledBy;
    private IUiExpandable? _discloses;
    private UiElement? _controls;
    private UiElement? _describedBy;
    private UiElement? _errorMessage;
    private bool _isRequired;
    private bool _hiddenFromAccessibility;
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
            // Showing or collapsing an element adds it to or removes it from its parent's children as
            // assistive technology sees them.
            (Parent ?? this).RaiseStructureChanged();
        }
    }

    /// <summary>
    /// An explicit name for assistive technology. When set, it replaces both a <see cref="LabeledBy"/>
    /// label and the name the control derives itself (for example from its text or placeholder).
    /// </summary>
    public string? AccessibleName
    {
        get => _accessibleName;
        set
        {
            ThrowIfDisposed();
            if (_accessibleName == value)
                return;

            _accessibleName = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// The element whose text names this one, usually a visible label. Its own semantic name is used
    /// when <see cref="AccessibleName"/> is empty. Assigning a label's <c>Target</c> sets this
    /// automatically, so a field keeps its label as its name and its text as its value.
    /// </summary>
    public UiElement? LabeledBy
    {
        get => _labeledBy;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_labeledBy, value))
                return;

            _labeledBy = ReferenceEquals(value, this) ? null : value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// What this element shows and hides when it is operated, such as the section a "Show details"
    /// button opens. The element then reports the target's <see cref="UiSemanticState.Expanded"/> or
    /// <see cref="UiSemanticState.Collapsed"/> state as its own, so a disclosure button carries the
    /// state where the focus is. A host offers its expand/collapse pattern on this element and acts
    /// through the target.
    /// </summary>
    /// <remarks>
    /// The target does not know who discloses it. When its state changes it must invalidate the
    /// semantics of the disclosing element, as <c>FormSection</c> does for its toggle. The target is
    /// what acts, and can be a container the element sits in; the content a reader should be sent to
    /// is named separately by <see cref="Controls"/>.
    /// </remarks>
    public IUiExpandable? Discloses
    {
        get => _discloses;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_discloses, value))
                return;

            _discloses = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// The element whose content this one shows, hides or changes, such as the fields a "Show
    /// details" button opens. A host exposes it as the element this one controls (UI Automation's
    /// ControllerFor, ARIA's <c>aria-controls</c>) while it is shown, so a screen reader can move
    /// from the button to what it opened.
    /// </summary>
    /// <remarks>
    /// Only the relation: <see cref="Discloses"/> carries the expand and collapse state and action.
    /// An element cannot control itself, and an ancestor, which already contains the element, is no
    /// place to send a reader; a host leaves it out.
    /// </remarks>
    public UiElement? Controls
    {
        get => _controls;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_controls, value))
                return;

            _controls = ReferenceEquals(value, this) ? null : value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// The element whose text describes this one beyond its name, usually a hint below a field. Its
    /// text becomes part of <see cref="UiSemanticNode.Description"/> while it is shown, and a host
    /// exposes the element itself as a described-by relation.
    /// </summary>
    /// <remarks>
    /// An ancestor of this element is ignored: it contains this element and cannot describe it.
    /// </remarks>
    public UiElement? DescribedBy
    {
        get => _describedBy;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_describedBy, value))
                return;

            _describedBy = ReferenceEquals(value, this) ? null : value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// The element that says what is wrong with this one's value, such as a field's error text. While
    /// it is shown (it and every ancestor visible, none hidden from accessibility, not disposed) and
    /// has text, this element reports <see cref="UiSemanticState.Invalid"/> and its
    /// <see cref="UiSemanticNode.Description"/> begins with that text, so a client that never follows
    /// relations still reads the error. A host lists it first among the described-by relations. Hide
    /// the message, or set this to null, when the value is valid again.
    /// </summary>
    /// <remarks>
    /// Nothing is announced when it is set: the form that validated usually announces its own status,
    /// and a second announcement would repeat it. The message text is read from the element's own
    /// node, as for <see cref="LabeledBy"/>, so a message element that changes its text, or is shown
    /// or hidden, invalidates only itself; whoever changes it should invalidate this element's
    /// semantics as well. An ancestor of this element is ignored, as for <see cref="DescribedBy"/>.
    /// </remarks>
    public UiElement? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_errorMessage, value))
                return;

            _errorMessage = ReferenceEquals(value, this) ? null : value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// Whether a value must be given before the form this element belongs to can be submitted. The
    /// element reports <see cref="UiSemanticState.Required"/>; nothing is enforced here.
    /// </summary>
    /// <remarks>
    /// A composite may override this to forward the state to the control that carries it, as
    /// <c>FormField</c> does; the composite itself then does not report the state.
    /// </remarks>
    public virtual bool IsRequired
    {
        get => _isRequired;
        set
        {
            ThrowIfDisposed();
            if (_isRequired == value)
                return;

            _isRequired = value;
            Invalidate(UiInvalidationKind.Semantic);
        }
    }

    /// <summary>
    /// True when a container has hidden this element, or one of its ancestors, from assistive
    /// technology while keeping it alive, such as the content of an inactive tab. Accessibility
    /// providers that walk the element tree should skip such elements.
    /// </summary>
    public bool IsHiddenFromAccessibility
    {
        get
        {
            for (UiElement? current = this; current is not null; current = current.Parent)
            {
                if (current._hiddenFromAccessibility)
                    return true;
            }

            return false;
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
        RaiseStructureChanged();
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
        RaiseStructureChanged();
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
        RaiseStructureChanged();
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
            // Every ancestor, not only up to the first already-invalid one: an element can be left
            // invalid under a valid parent (for example, invalidated while that parent was measuring
            // it), and stopping there kept a later change from ever reaching the root.
            for (UiElement? current = Parent; current is not null; current = current.Parent)
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
        if (node.Id == 0)
            node = node with { Id = SemanticId };
        if (ResolveAccessibleName() is { Length: > 0 } name)
            node = node with { Name = name };
        UiSemanticState state = node.State;
        if (_discloses is { } target && target is not UiElement { IsDisposed: true })
        {
            state = (state & ~(UiSemanticState.Expanded | UiSemanticState.Collapsed)) |
                (target.IsExpanded ? UiSemanticState.Expanded : UiSemanticState.Collapsed);
        }
        string? error = RelationText(_errorMessage);
        if (error is not null)
            state |= UiSemanticState.Invalid;
        if (_isRequired)
            state |= UiSemanticState.Required;
        if (JoinSentences(error, RelationText(_describedBy), node.Description) is { } description)
            node = node with { Description = description };
        if (_hiddenFromAccessibility)
            state = (state | UiSemanticState.Offscreen) & ~UiSemanticState.Visible;
        else if (!Bounds.IsEmpty && Visibility == UiVisibility.Visible && GetVisibleBounds().IsEmpty)
            state |= UiSemanticState.Offscreen; // Laid out, but scrolled or clipped entirely out of view.
        if (state != node.State)
            node = node with { State = state };
        return node;
    }

    /// <summary>
    /// The part of <see cref="Bounds"/> that can be seen: the bounds intersected with the clip that
    /// every ancestor applies to its children, such as a scroll view's viewport. Empty when this
    /// element or an ancestor is not visible, or when the element is scrolled or clipped entirely out
    /// of view; <see cref="GetSemanticNode"/> then reports <see cref="UiSemanticState.Offscreen"/>.
    /// </summary>
    /// <remarks>
    /// A host uses this as the element's on-screen rectangle for assistive technology, so a field
    /// scrolled half out of a form is not reported over the buttons below it. The host window itself
    /// is not a clip here; the host clips to its own surface. An element drawn as an overlay outside
    /// its parent (see <see cref="OverlayBounds"/>) answers for that area separately.
    /// </remarks>
    public BRect GetVisibleBounds()
    {
        if (_isDisposed || Visibility != UiVisibility.Visible)
            return BRect.Empty;

        BRect visible = Bounds;
        UiElement child = this;
        for (UiElement? ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (visible.IsEmpty || ancestor.Visibility != UiVisibility.Visible)
                return BRect.Empty;
            if (ancestor.GetClipBoundsForChild(child) is { } clip)
                visible = visible.Intersect(clip);
            child = ancestor;
        }

        return visible.IsEmpty ? BRect.Empty : visible;
    }

    /// <summary>
    /// Lets a container hide a child it keeps alive (for example inactive tab content) from
    /// assistive technology. The child is left out of the parent's default semantic children and
    /// reports the Offscreen state; <see cref="IsHiddenFromAccessibility"/> exposes it to providers.
    /// </summary>
    protected static void SetHiddenFromAccessibility(UiElement element, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element._hiddenFromAccessibility == hidden)
            return;

        element._hiddenFromAccessibility = hidden;
        element.Invalidate(UiInvalidationKind.Semantic);
        element.Parent?.Invalidate(UiInvalidationKind.Semantic);
        (element.Parent ?? element).RaiseStructureChanged();
    }

    /// <summary>
    /// Tells assistive technology that the children of this element changed in a way the element tree
    /// does not show, such as the items of a list or the tabs of a tab view. Adding, removing or moving
    /// a child element, changing a child's <see cref="Visibility"/> and
    /// <see cref="SetHiddenFromAccessibility"/> already raise it.
    /// </summary>
    /// <remarks>
    /// Raises <see cref="UiSemanticChangeKind.StructureChanged"/> for this element on its session, once
    /// per call. A host that rebuilds its view of the children on this should coalesce, since a batch
    /// of changes raises one event per change.
    /// </remarks>
    protected void NotifyStructureChanged() => RaiseStructureChanged();

    // A disposing element's own children are removed one by one; its parent is told once, when the
    // element itself is removed.
    private void RaiseStructureChanged()
    {
        if (!_isDisposing && !_isDisposed)
            Session?.NotifyStructureChanged(this);
    }

    private string? ResolveAccessibleName()
    {
        if (!string.IsNullOrWhiteSpace(_accessibleName))
            return _accessibleName;
        // The label's own core node gives its text; going through GetSemanticNode could recurse
        // when two elements label each other.
        if (_labeledBy is { IsDisposed: false } label)
            return RelatedName(label);
        return null;
    }

    // The text of a related element while the user can see it: a hidden error message, or one in a
    // collapsed panel, no longer says anything about the value. An ancestor is not a description of
    // what it contains, and reading its node would build this element's node again.
    private string? RelationText(UiElement? related)
    {
        if (related is null || related.IsDisposed || IsDescendantOf(related))
            return null;

        for (UiElement? current = related; current is not null; current = current.Parent)
        {
            if (current.Visibility != UiVisibility.Visible || current._hiddenFromAccessibility)
                return null;
        }

        string? text = RelatedName(related);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    // A related element's name comes from its core node. That node can contain the asking element
    // again, through a container or a relation that leads back, so the nodes built meanwhile resolve
    // no relations of their own and a cycle ends after one step. Their relation text would be
    // discarded anyway: only the related element's own name is used.
    private static string? RelatedName(UiElement related)
    {
        if (_relationDepth > 0)
            return null;

        _relationDepth++;
        try
        {
            return related.GetSemanticNodeCore().Name.Trim();
        }
        finally
        {
            _relationDepth--;
        }
    }

    private static string? JoinSentences(params string?[] parts)
    {
        string? joined = null;
        foreach (string? part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;

            if (joined is null)
                joined = part;
            else
                joined += (joined[^1] is '.' or '!' or '?' or ':' ? " " : ". ") + part;
        }

        return joined;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposing = true;
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

    /// <summary>
    /// The rectangle this element clips <paramref name="child"/> to when it draws it, or null when
    /// it does not clip it. <see cref="GetVisibleBounds"/> intersects an element's bounds with the
    /// clip of every ancestor. A container that scrolls or crops its children, such as a scroll
    /// view's viewport, overrides this so what it hides is not reported as on screen.
    /// </summary>
    protected virtual BRect? GetClipBoundsForChild(UiElement child) => null;

    // Layout-only elements have no name of their own; the type name is available to providers as a
    // class name and must not be read out as if it were content.
    protected virtual UiSemanticNode GetSemanticNodeCore() =>
        new(
            UiSemanticRole.Generic,
            string.Empty,
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
            if (child.Visibility != UiVisibility.Collapsed && !child._hiddenFromAccessibility)
                nodes.Add(child.GetSemanticNode());
        }

        return nodes;
    }
}
