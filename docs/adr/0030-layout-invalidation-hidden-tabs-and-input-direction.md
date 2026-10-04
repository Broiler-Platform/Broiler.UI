# ADR 0030 - Layout invalidation, hidden tab layout, and input direction

**Status:** Proposed  
**Date:** 2026-10-04

## Context

Broiler.Mail's work on narrow windows, larger text, and native input found five problems that
originate in Broiler.UI 0.1.0-preview.17:

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
- `StandardToolbar`'s arrow, Home, and End keys landed on disabled items that Tab passes over.
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
  under either lifetime policy. The shown tab is measured at the rectangle it is given, in both
  dimensions; the measure cache makes that free when the sizes already agree.
- **Hidden content is not hit.** Content a container hides with `SetHiddenFromAccessibility` (ADR
  0027) now keeps bounds that can lie under what is shown in its place, so `UiSession.HitTest` skips
  it, as focus traversal and semantics already do. `UiSplitContainer` still arranges a collapsed pane
  at an empty rectangle.
- **Wheel direction.** A positive vertical notch is a wheel turned away from the user and scrolls
  towards the start; a positive horizontal notch is a wheel tilted right and scrolls right. Shift turns
  a vertical wheel sideways with the vertical sign, so the wheel turned towards the user scrolls right,
  as in other Windows applications. Every standard control follows this, and an input source converts
  to it (the WebAssembly gallery passes the DOM's `deltaX` unnegated).
- **Composite navigation matches Tab.** Arrowing along a `StandardToolbar` passes over an item that
  takes focus but cannot right now (disabled), as Tab does. A child that never takes focus itself is
  still reached, as before.
- **A shrinking form keeps focus in view.** When a `FormViewport`'s viewport becomes smaller, the part
  of the focused control that was on screen is shown again, scrolling no further than needed and
  never past its top. Focus does not move, and nothing scrolls when nothing inside has focus or the
  focused control had already been scrolled away.

## Consequences

- Behavior change: inactive tab content reports the bounds it was last shown with instead of an empty
  rectangle (it is still `Offscreen` and hidden from accessibility). Code that tested hidden content
  for empty bounds should test `IsHiddenFromAccessibility` or `Visibility` instead.
- Behavior change: a tilt wheel or sideways touchpad swipe scrolls the other way in
  `StandardScrollView`, `StandardRichEdit`, and `StandardFormatCodeView`. Shift with the wheel is
  unchanged. `StandardFormatCodeView` still ignores Shift.
- Broiler.Mail can drop `TabContent` once it consumes this, and its native horizontal-wheel test can
  assert the direction.
- An arrange invalidation now costs a walk to the root instead of stopping early, the same cost a
  measure invalidation already had.
