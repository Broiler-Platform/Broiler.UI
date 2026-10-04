# ADR 0030 - Layout invalidation, hidden tab layout, input direction, keyboard scope, and tree row semantics

**Status:** Proposed  
**Date:** 2026-10-04

## Context

Broiler.Mail's work on narrow windows, larger text, and native input, the review of those fixes, and
the review of Broiler.Hosting.Windows' UI Automation support for tree rows found these problems in
Broiler.UI 0.1.0-preview.17:

- `UiElement.Invalidate(Arrange)` stopped at the first ancestor that was already arrange-invalid, and
  `Measure` cleared only the element's own arrange flag when its desired size changed. An element
  could be left arrange-invalid under an arrange-valid parent: invalidated while that parent was
  arranging it, or measured to a new size under a parent that kept its own size. An offset change
  below it then never reached the root, so a scroll view's offset changed and its content did not
  move until something else laid the window out again. Mail reproduced this headless in its compact
  inbox; a native run of the published app did not show it in the paths tried, so the reach of the
  symptom depends on what else lays the window out in between.
- `StandardTabView` arranged every inactive tab at `BRect.Empty`, also under `CollapseInactive`. A
  hidden form was laid out at no width, and a scroll position inside it was reset by the time the
  user came back. Mail wraps every tab in its own `TabContent` element only to skip that arrange.
- Win32 reports `WM_MOUSEHWHEEL > 0` for a wheel tilted right, and every Broiler input source passes
  that sign through. `StandardCodeEditor` and `StandardTreeView` scrolled right for it;
  `StandardScrollView`, `StandardRichEdit`, and `StandardFormatCodeView` scrolled left.
  `StandardRichEdit` without horizontal scrolling (the default) and a wrapped
  `StandardFormatCodeView` (the default) turned a tilt into a vertical scroll.
- Shift with the wheel reaches the controls in two shapes. Broiler.Input and Broiler.Graphics report
  a vertical notch with Shift. Broiler.Hosting.Windows turns it into a horizontal notch that keeps
  Shift and the vertical sign, so turning the wheel towards the user arrives as a horizontal -1.
  Read as a tilt, that is a scroll to the left: `StandardCodeEditor` and `StandardTreeView` already
  scrolled left for it, and correcting the tilt alone would have done the same in the other three.
- `StandardToolbar`'s arrow, Home, and End keys landed on disabled items and on children that never
  take focus (a status label), all of which Tab passes over.
- A `FormSurface` whose fields' viewport shrinks (feedback shown below the actions) could push the
  control being typed in out of view.
- `StandardTabView` acted on Left, Right, Home, and End wherever they came from. A key that a control
  inside a tab left unhandled (Right on a button) bubbled up to it, switched tabs, and moved focus to
  the strip. An inner tab view's End at its last tab switched the outer tab view. With nothing
  focused, a key that reached the strip only because of where it was dispatched switched tabs too.
- `UiTreeView` describes only the rows in view as its semantic children, but a change of
  `FirstVisibleRow` (the wheel, the scroll bar, a row brought into view) invalidated only Render, and a
  change of `VisibleRowCapacity` (the tree resized) invalidated nothing. A host learned of the new rows
  only at the tree's next state change. When the focus moved, it heard the selection change before the
  scroll, while the old rows were still in view. Broiler.Hosting.Windows' tree row support, not yet
  released, works around this by checking the rows itself after the scrolls it causes.
- `UiTreeView` found each described row's position within its level by walking the row's siblings,
  so describing the tree cost the rows in view times the size of their level: about 1.3 ms for 20 rows
  of a flat tree of 50,000.

## Decision

- **Layout invalidation reaches the root.** `Invalidate(Arrange)` walks every ancestor, as
  `Invalidate(Measure)` already did, and a desired-size change in `Measure` marks every ancestor for
  arrange, so the parent places the element at its new size in the same frame. An invalidation raised
  while an ancestor is still arranging is overwritten when that ancestor finishes, as before; a
  control that changes a child's arrangement inside its own `ArrangeCore` arranges that child again
  itself (`FormViewport` does).
- **A hidden tab keeps its last arrangement.** `StandardTabView` no longer arranges inactive content,
  under either lifetime policy. The shown tab is laid out at the rectangle it is given, in both
  dimensions: it is measured again there unless it was measured at the same width and at least that
  height and asks for no more than it gets. That exception keeps a tab view that a stack arranges at
  the height it asked for from measuring its content twice, at two sizes, on every pass.
- **Hidden content is not hit.** Content a container hides with `SetHiddenFromAccessibility` (ADR
  0027) now keeps bounds that can lie under what is shown in its place, so `UiSession.HitTest` skips
  it, as focus traversal and semantics already do. `UiSplitContainer` still arranges a collapsed pane
  at an empty rectangle.
- **Wheel direction.** A positive vertical notch is a wheel turned away from the user and scrolls
  towards the start. A horizontal notch without Shift is a wheel tilted (or a touchpad swiped)
  sideways; positive is to the right and scrolls right. Shift turns the wheel sideways with the
  vertical sign, so the wheel turned towards the user scrolls right, as in other Windows applications.
  A horizontal notch that carries Shift is that same wheel already turned by the host (the
  Broiler.Hosting.Windows shape) and keeps the vertical sign. A tilt with Shift held cannot be told
  apart from it and scrolls the other way; that gesture is rare enough to give up.
- **A tilt is left to whoever can scroll sideways.** A control that cannot scroll sideways leaves a
  tilt unhandled instead of scrolling vertically, so a scroller outside it can take it. Shift with the
  wheel keeps each control's existing fallback.
- **The controls that scroll sideways agree.** `StandardScrollView`, `StandardRichEdit`,
  `StandardFormatCodeView`, `StandardCodeEditor`, and `StandardTreeView` follow these rules, whichever
  shape Shift arrives in. `StandardFormatCodeView` now honors Shift with a vertical notch too; it used
  to scroll sideways only for the Hosting shape. `StandardListView` cannot scroll sideways and reads
  only vertical notches, as before.
- **Hosts that translate DOM wheel events pass `deltaX` unnegated.** The DOM reports a swipe or tilt
  to the right as a positive `deltaX`, which is already the sign the controls expect. The WebAssembly
  gallery used to negate it and no longer does.
- **Composite navigation matches Tab.** Arrowing along a `StandardToolbar` lands only where Tab can:
  it passes over an item that takes focus but cannot right now (disabled), and a child that never
  takes focus and holds nothing that does (text). A child that never takes focus but holds controls
  (a group) lands on the first of them that can. When nothing on the bar can take focus, the bar
  still consumes its navigation keys, as it did before, so they do not reach a container around it
  that reads the same keys.
- **A tab view reads its strip's keys only while it has focus.** `StandardTabView` acts on Left,
  Right, Home, and End only when it is the focused element, which a click on a header, Tab, and
  `UiSession.SetFocus` all make it. A key that bubbles up from its content, or reaches it while
  nothing has focus, is left unhandled and goes on to its ancestors. The tab view has no Ctrl+Tab or
  Ctrl+PageUp/PageDown handling, and none is added. Applications that offer those shortcuts handle
  them themselves, as Broiler.Mail does before it dispatches a key.
- **Scrolling a tree is a semantic change.** `UiTreeView.FirstVisibleRow` invalidates Semantic when it
  changes, and `VisibleRowCapacity` when the number of rows the tree describes changes with it, and
  only then. Hosts therefore hear after every scroll, and every resize that shows more or fewer rows,
  that the tree's children changed. Room the rows do not fill is not a change: a tree whose rows all
  fit says nothing at its first layout or when resized. When the focus moves, the last thing hosts hear
  is the scroll. The change is reported as the tree's state change, as every semantic invalidation is,
  because a tree's rows are virtual nodes. A host that keeps the rows it exposed compares them on that
  state change; Broiler.Hosting.Windows' tree row support (not yet released) does.
- **Describing a tree costs the rows in view.** `UiTreeView` works out every row's position within its
  level once for each set of rows, on first use after the rows are rebuilt, in one pass over them, and
  looks it up when it describes a row. Describing 20 rows of a flat tree of 50,000 takes about 0.01 ms
  instead of 1.3 ms.
- **A shrinking form keeps focus in view.** When a `FormViewport`'s viewport becomes smaller, the part
  of the focused control that was on screen is shown again, scrolling no further than needed and
  never past its top. Focus does not move, and nothing scrolls when nothing inside has focus or the
  focused control had already been scrolled away.

## Consequences

- Behavior change: inactive tab content reports the bounds it was last shown with instead of an empty
  rectangle (it is still `Offscreen` and hidden from accessibility). Code that tested hidden content
  for empty bounds should test `IsHiddenFromAccessibility` or `Visibility` instead.
- Behavior change: a tilt wheel or sideways touchpad swipe scrolls the other way in
  `StandardScrollView`, `StandardRichEdit`, and `StandardFormatCodeView`. In a `StandardRichEdit`
  without horizontal scrolling or a wrapped `StandardFormatCodeView` it no longer scrolls the lines.
- Shift with the wheel keeps its direction in both shapes. Under Broiler.Hosting.Windows (Broiler.Mail)
  it now also scrolls right in `StandardCodeEditor` and `StandardTreeView`, which read the Hosting
  shape as a tilt before. With a vertical notch it now scrolls `StandardFormatCodeView` sideways when
  its lines are wider than the view.
- Hosts that convert DOM wheel events and negate `deltaX`, as the gallery did, must stop negating it
  when they adopt this release, or sideways scrolling runs backwards. Broiler.Writer's WebAssembly
  host does this today (`wwwroot/main.js`).
- No change is required in Broiler.Hosting. A future Hosting release may report Shift with the wheel
  as a vertical notch with Shift; both shapes behave the same.
- Broiler.Mail can drop `TabContent` once it consumes this, and its native horizontal-wheel test can
  assert the direction.
- An arrange invalidation now costs a walk to the root instead of stopping early, the same cost a
  measure invalidation already had.
- Behavior change: Left, Right, Home, and End no longer switch tabs unless the `StandardTabView` has
  focus. They reach its ancestors unhandled instead. Code that dispatched them to a tab view without
  focusing it must focus it first. `StandardToolbar` still keeps its own keys when nothing on it can
  take focus. A tab view no longer needs that, but other containers that read the same keys still do.
- Behavior change: a tree raises a semantic invalidation (`SemanticChanged` with `StateChanged`) each
  time a scroll moves its first row, and each time a resize changes how many rows it describes, which
  includes the first layout of a tree with more rows than fit. A handler that reacts to every event
  sees more of them. A host that describes the tree for each one pays for the rows in view, not for
  the tree's size, after one pass over the rows each time they are rebuilt.
- `StandardTreeView` works out how many rows fit inside its arrange, so a resize that changes the rows
  in view is reported from inside the layout, as `StandardListView` already reports a scroll offset it
  clamps there. A host that compares semantics as soon as it is told sees the tree arranged and the
  rest of the window not yet. Applications with a tree should build their session with
  `StandardQueuedUiDispatcher`, so work a host posts (Broiler.Hosting.Windows' bridge posts its
  comparison) runs after the frame; Hosting's structure coalescing already needs one. With
  `ImmediateUiDispatcher` the comparison runs mid-layout. The invalidation stays pending after that
  frame, so a host that repaints for every invalidation, of any kind, paints one more frame.
- Not verified against Broiler.Hosting.Windows. As released, Hosting exposes no tree rows, so it only
  sees more state changes. Its tree row support (branch `claude/uia-semantics-mapping`, not yet
  released) needs no change: a wheel scroll or a resize that does not move the focus reaches its
  tree-rows comparison without waiting for the tree's next state change. Its
  `AutomationStructureTests` must be re-run once Hosting consumes a Broiler.UI with this change. Two
  of its comments (the `_treeRows` note in `WindowsAutomationBridge` and `ScrollTreeRowIntoView` in
  `WindowsElementAutomationPeer.TreeRows`) say Broiler.UI reports a scroll only as a render change and
  need updating then. The explicit `QueueTreeCheck` in `ScrollTreeRowIntoView` becomes redundant; it
  is harmless, because the flush dedupes it. While a client listens, Hosting still describes the tree
  synchronously for every state change of a tree the client has reached; folding that into its
  per-flush tree check would coalesce a burst of wheel notches or a thumb drag into one description.
