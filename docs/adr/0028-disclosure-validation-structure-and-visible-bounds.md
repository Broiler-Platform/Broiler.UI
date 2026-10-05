# ADR 0028 - Disclosure state, validation relations, structure events, and visible bounds

**Status:** Proposed  
**Date:** 2026-10-04

## Context

Broiler.Mail's UI Automation acceptance (UI-09) and its acceptance run on 2026-10-02 found gaps
that start in Broiler.UI, not in the host bridge (ADR 0008):

- **Disclosure.** `UiSemanticState` had `Expanded` but no `Collapsed`, so a closed disclosure looked
  like an element that does not expand at all. `FormSection` (the Cc/Bcc fields in Mail's composer,
  "Advanced" in account setup) put `Expanded` on its group, and only while open; its toggle button,
  which is what takes focus, carried no state. Nothing let a host act on a disclosure, so the bridge
  special-cased `UiComboBox`.
- **Validation.** `FormField.SetError` put `Invalid` and the error text into the field group's name.
  The control that has the focus had no `Invalid` state, no description, and no relation to the
  error.
- **Structure events.** `UiSemanticChangeKind.StructureChanged` existed but was never raised, so a
  host had no signal to refresh a subtree or to drop peers of removed elements.
- **Geometry.** Semantic bounds were never clipped. A field scrolled out of a form reported its full
  rectangle, over the action bar below, and nothing set `Offscreen` for scrolled-out content.
  List-item nodes from presenters were always `Visible` with unclipped bounds, tab nodes used the
  whole tab view's bounds, and the real header geometry was private.
- **Scroll view focus.** A focused `StandardScrollView` drew no focus indicator. Making a read-only
  area a keyboard stop while it can scroll was application policy (Mail's keyboard navigation), so
  `CanFocus`, the toolkit's own focus traversal, and a host's "keyboard focusable" disagreed.

## Decision

### Disclosure

- **`UiSemanticState.Collapsed`** (4096) is the counterpart of `Expanded`. An expandable element
  reports exactly one of the two; an element that cannot expand reports neither.
- **`IUiExpandable`** (`IsExpanded`, `Expand()`, `Collapse()`, each returning whether anything
  changed) is the action half. The state flags remain the semantic half.
- **`UiElement.Discloses`** points an element, typically a button, at the `IUiExpandable` it shows and
  hides. `GetSemanticNode()` then reports the target's state as the element's own, so the state sits
  where the focus is. A disposed target is ignored. The target does not know who discloses it and
  must invalidate the discloser's semantics when its state changes.
- **`UiElement.Controls`** names the element whose content this one shows, hides, or changes
  (ARIA's `aria-controls`), which a host exposes as the element this one controls. It is separate
  from `Discloses` because the object that acts is not always where a reader should go: a
  `FormSection` expands as a whole, but the section contains its own toggle, so sending a reader to
  it moves them nowhere. An element cannot control itself.
- **`FormSection`** implements `IUiExpandable`. Its toggle discloses the section and controls the
  section's `Content`. The group reports neither state (the ARIA disclosure pattern puts the state
  on the button only). The toggle keeps its "Show …" / "Hide …" text. `Collapse()` moves focus out
  of the content to the toggle, as setting `IsExpanded` already did.
- **`UiComboBox`** and **`UiMenu`** implement `IUiExpandable` explicitly (drop-down and open state)
  and report `Collapsed` while closed. `Expand()` and `Collapse()` return false when nothing
  changed. Tree rows that can expand report `Collapsed` (they already reported `Expanded`).

### Validation relations

- **`UiElement.DescribedBy`** is the element whose text describes this one, such as a hint.
- **`UiElement.ErrorMessage`** is the element that says what is wrong with the value. While it is
  shown (it and every ancestor visible, none hidden from accessibility, not disposed, non-blank), the
  element reports `Invalid`. ARIA's `aria-errormessage` is the model. It is separate from
  `DescribedBy` so that a field keeps its hint while it is invalid, and so `Invalid` cannot be left
  on with no message.
- **`UiElement.IsRequired`** reports `Required`. It is virtual so a composite can forward it to the
  control that carries it; the composite then reports nothing itself.
- **`UiSemanticNode.Description`** is an `init` property, so the record's constructor and
  deconstruction are unchanged. `GetSemanticNode()` fills it with the shown error message's text,
  then the described-by text, then any description the control gives itself, joined as sentences.
  This is the channel for clients that never follow relations: while a field is invalid, its error
  is in the control's description.
- Relation text is read from the related element's core node, as for `LabeledBy` (ADR 0027). That
  node can contain the asking element again, through an ancestor or a container whose control is
  related back, so while it is built the nodes inside it resolve no relations, and every cycle,
  including one through `LabeledBy`, ends after one step. An ancestor is never a description or an
  error message of what it contains and is ignored for those two relations; it can still label an
  element by its own name.
- **`FormField`** points its control's `DescribedBy` at the description (unless the application set
  one) and its `ErrorMessage` at the error label while `SetError` shows a message. It invalidates the
  control's semantics on every error change, and `FormField.IsRequired` sets the control's state.
  The field group keeps its existing name, which includes the error text, and its `Invalid` state,
  for compatibility. A field error is not announced: the form's status announces the failure, and a
  second announcement would repeat it.

### Structure events

- `UiElement` raises `StructureChanged` for the parent when a child is inserted, removed, or moved,
  when a child's `Visibility` changes, and from `SetHiddenFromAccessibility`. A root has no parent, so
  when a root itself is shown, collapsed, or hidden from assistive technology, the root is the
  subject: everything it exposes changed. A disposing element does not raise it for each of its own
  children; its parent is told once.
- The protected **`NotifyStructureChanged()`** is for containers whose children assistive
  technology sees as virtual nodes. `UiListView.SetItems` and `UiTabView`'s add, remove, and move
  call it.
- Changes made while `UiSession.DispatchInput` or `UiSession.RenderFrame` runs are held and raised
  once per element, in the order they first changed, when the call returns. An element that has left
  the session by then is not named; the parent it left is. A tab switch by click, which hides one
  page, shows another, and changes the tab nodes, is one event for the tab view, and feedback a form
  surface shows or hides while it is measured is reported after the frame rather than during
  layout. Changes made outside those calls, such as an application updating views from a dispatcher
  callback, are raised as they happen, one per change. The dispatcher is not used, so the
  application's queue and its drain counts are unchanged.
- State-only changes raise none, and neither do the session's root changes (`AddRoot`,
  `RemoveRoot`, `MoveRoot`), which hosts track themselves. A host should still coalesce per frame.

### Visible geometry

- **`UiElement.GetVisibleBounds()`** intersects `Bounds` with the clip that every ancestor applies
  to its children, through the new protected virtual **`GetClipBoundsForChild(child)`** (null means
  no clip). The result is empty when the element or an ancestor is not visible, or when the element
  is clipped away entirely. The host window is not a clip here; the host clips to its own surface.
  An overlay drawn outside its parent (`OverlayBounds`) answers for that area separately.
- `UiScrollView` clips to its viewport at its origin, and `StandardScrollView` to its content bounds,
  the same area its children are drawn and hit-tested in. `FormViewport` and `FormSurface` inherit
  this through their scroll view. `StandardRichEdit` hosts no child elements and needs no clip.
- `GetSemanticNode()` adds `Offscreen` for an element that is laid out but clipped entirely out of
  view. It keeps `Visible`, which still means "not hidden"; only hidden content clears it. Node
  `Bounds` remain the element's `Bounds`, and a host asks the element for its visible bounds.
- **List items** have no element to ask, so `UiListView.GetItemSemanticNode` and the list's own
  semantic children fit the presenter's node with the protected `ClipItemSemanticNode`. The
  presenter's name and states are kept. A partly visible row is `Visible`, and its bounds are cut to
  the content area and the ancestors' clips. A row entirely out of view is `Offscreen`, not
  `Visible`, and keeps its full bounds, which say where scrolling would bring it (the existing
  realization contract). `UiListView.IndexOf(string)` is public, so a host can key item peers by id.
- **Tabs.** `UiTabView.GetTabHeaderBounds(index)` (virtual; empty by default) gives a header's
  rectangle. `StandardTabView` implements it from the measured header widths and
  `EffectiveHeaderHeight`, the same geometry it paints and hit-tests. Tab semantic nodes use the
  header clipped to the view's visible bounds, or the full header and `Offscreen` (still `Visible`)
  when none of it shows. Without a header they keep the view's bounds.
- `Offscreen` therefore keeps `Visible` for elements and tabs but not for list items, whose earlier
  contract already cleared `Visible` for rows out of view. The `UiSemanticState.Offscreen`
  documentation says so; hosts read `Offscreen`, not a missing `Visible`, to tell what is on screen.

### Scroll view focus

- `StandardScrollView` implements `IStandardThemedControl`. `ApplyTheme` sets its new `FocusRing`
  color and captures the theme's ring offset and thickness. The scrollbar track and thumb colors are
  deliberately unchanged, so themed sessions draw scrollbars as before. That includes the
  high-contrast themes, where the semi-transparent default thumb has not been checked for contrast.
  The theme tokens have no scrollbar roles yet; mapping them, at least under a high-contrast theme
  (ADR 0029 adds the flag), is left to the high-contrast pass. ADR 0033 adds the roles and maps them.
- While it has focus and is a keyboard stop (`CanFocus`, through `Focusable` or
  `FocusWhenScrollable`), it strokes the ring after the scrollbars, inset by the theme offset,
  whatever the last input was, as an editor does: the ring shows where the arrow keys go. A view
  that is no stop never draws one. A click on blank form space or a label focuses the scroll view
  behind it, and a later key, such as Alt, must not ring the whole form. The stroke is drawn here
  rather than by `StandardControlPaint.DrawFocusRing`, which takes the theme's color and would
  ignore the control's `FocusRing`.
- **`FocusWhenScrollable`** (off by default) makes `CanFocus` true while the extent exceeds the
  viewport (with a half-DIP tolerance) and no visible, unhidden descendant can take focus. Tab
  traversal and `CanFocus` then agree on the stop. `Focusable` stays false, so a host must judge
  keyboard focusability, and whether an unnamed scroll view is layout only, by `CanFocus`. The
  existing arrow, Page, Home, and End handling scrolls the view once it is focused. An application
  that makes such a stop by its own policy and calls `SetFocus` on a view that is not `CanFocus` gets
  no ring until it opts into `FocusWhenScrollable`. ADR 0034 decides it on the view as last arranged,
  with the tolerance a bar set to Auto now uses, so with Auto bars the stop exists exactly while a bar
  shows.

## Consequences

- Host bridges should map:
  - Expand/collapse: offer the pattern when a node reports `Expanded` or `Collapsed`. Act through
    the element if it is an `IUiExpandable`, otherwise through its `Discloses` target. Tree rows are
    virtual nodes with neither: the row at semantic child `i` is `Rows[FirstVisibleRow + i]`, and
    the host acts through `UiTreeView.Expand(row.Id)` and `Collapse(row.Id)`. Raise the state's
    property change.
  - `ControllerFor` is `[Controls]` while that element is exposed: not disposed, it and its
    ancestors visible, not hidden from accessibility, and not an ancestor of the element. It is not
    derived from `Discloses`.
  - Validation: `IsDataValidForForm` is `!Invalid` and `IsRequiredForForm` is `Required`.
    `DescribedBy` lists the shown `ErrorMessage` first, then the shown `DescribedBy`; the error is
    reached through `ErrorMessage`, since a field's own `DescribedBy` is its hint. `FullDescription`
    is `Description`. Whether the error should also replace the placeholder in `HelpText` is left to
    the host and the screen-reader pass (H-01), since reading it through several properties can
    repeat it.
  - Structure: `StructureChanged` should become a children-invalidated event on the element's peer,
    coalesced per frame, and the trigger for releasing peers of removed elements.
  - Geometry: the bounding rectangle is `GetVisibleBounds()` clipped to the window, and `IsOffscreen`
    is `Offscreen`, hidden content, or empty visible bounds. List items take their bounds and
    visibility from `GetItemSemanticNode` and tabs from `GetTabHeaderBounds`, which also serve hit
    testing. `IsKeyboardFocusable` is `CanFocus`, and so is the "not layout only" test for an unnamed
    scroll view, since a `FocusWhenScrollable` stop keeps `Focusable` false. Such a stop should also
    get an `AccessibleName`, or a reader has nothing to say when it takes focus.
- Release order: no released Broiler.UI raised `StructureChanged` before this, so the host code
  for it has never run. Broiler.Hosting as consumers ship it today (0.1.0-preview.5 in Mail, and
  main at 380e0e8) handles every such event by cleaning its peer tables and raising a
  children-invalidated event on the window root. Batching per input and
  per frame keeps ordinary clicks, tab switches, and layout to one event per container, but changes
  an application makes from dispatcher callbacks still arrive one by one. Consumers should take this
  release together with, or after, the Hosting release that coalesces per frame and targets the
  parent's peer, and pin that pair.
- Behavior changes:
  - The `FormSection` group no longer reports `Expanded`; its toggle does, and controls the content.
  - Closed combo boxes and menus, and collapsed tree rows that can expand, now report `Collapsed`.
  - Elements scrolled entirely out of a scroll view now report `Offscreen` (still `Visible`).
  - A presenter's list-item node is no longer `Visible` when the row is out of view, and a partly
    visible row's bounds are cut.
  - Tab nodes now carry header bounds instead of the whole view's.
  - `SemanticChanged` now also carries `StructureChanged`. A handler that reacts to every event
    should filter by kind.
  - A focused scroll view that is a keyboard stop draws a focus ring.
- Applications opt read-only scroll areas into `FocusWhenScrollable` and can drop their own
  "scroll stop" policy. A subclass that declares a member named like one of the new `UiElement`
  members gets a hiding warning when it recompiles.
- Left for the H-01 screen-reader pass: the field group still names the error and reports
  `Invalid`, and tree row names still end in ", expanded" or ", collapsed". Once hosts map the
  control's state and the row flags, a reader may say the error or the state twice; drop them there
  if it does.
- Not covered: per-line text-range geometry, clipping for other cropping containers (the toolbar's
  overflow strip, for example), scrollbar colors under high contrast (see Scroll view focus), and
  real screen-reader speech.
