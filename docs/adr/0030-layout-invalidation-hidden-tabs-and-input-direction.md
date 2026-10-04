# ADR 0030 - Layout invalidation, hidden tab layout, and input direction

**Status:** Proposed  
**Date:** 2026-10-04

## Context

Broiler.Mail's work on narrow windows, larger text, and native input, and the review of the fixes,
found these problems in Broiler.UI 0.1.0-preview.17:

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
  that reads the same keys (a `StandardTabView` would switch tabs).
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
