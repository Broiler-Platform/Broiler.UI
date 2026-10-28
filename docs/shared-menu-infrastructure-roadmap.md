# Shared menu infrastructure roadmap

Date: 2026-10-27. Status: proposed; separator sharing is implemented in the
current working tree, and the phases below are planned.

## Outcome

StandardMenu, StandardEdit, and StandardRichEdit should share popup geometry,
row painting, and navigation behavior. A correction to separator layout,
viewport placement, or keyboard navigation should reach all three consumers.
Editors continue to own their command lists, selection, clipboard rules, and
undo behavior. Existing public APIs remain usable.

## Current baseline

- `StandardMenuSeparator` in `Broiler.UI.Standard` supplies the seven-pixel
  separator row and inset line painting to all three controls.
- Edit and RichEdit independently implement nearly identical measurement,
  hit testing, clipping, highlighting, shortcut rendering, navigation, and
  open/close state in their `Standard*.ContextMenu.cs` files.
- StandardMenu has its own popup geometry and input handling, plus hierarchical
  selection paths and MenuBar, ContextMenu, Overflow, Drawer, and TabBar modes,
  as well as BottomSheet presentation.
- Editor popups have horizontal/vertical padding, measured shortcut columns,
  hover highlighting, Home/End navigation, and input suppression while open.
  StandardMenu's current popup implementation differs in these areas.
- Editor command enums and public item records differ from `UiMenuItem`.
  Edit's enablement includes password and single-line paste rules; RichEdit
  delegates to its document command state.

## Architecture and boundaries

Put shared helpers in a `Menus` folder of `src/Foundation/Broiler.UI.Standard`.
This assembly is already referenced by all three implementations. It must not
gain dependencies on Menu, Edit, RichEdit, or the document model.

Use a small presentation row containing label, shortcut, separator/enabled
state, and any check/submenu presentation metadata. Adapt existing item types
to this view; keep command identity and execution with the owning control.
Treat proposed type names below as design guidance, not committed public APIs.
Helpers needed across assemblies require deliberate visibility and API review.

The shared layer supplies geometry, painting, navigation decisions, and common
popup state. The owner performs session focus/capture operations, invalidation,
command execution, and document changes. Menu-bar traversal, cascading submenu
paths, drawer/tab triggers, and bottom-sheet backdrop/handle behavior remain
presentation policies layered on the shared popup primitives.

## Delivery sequence

### 1. Record behavior and settle parity decisions

- Add a short ADR describing the dependency boundary and row adapter contract.
- Extend behavioral coverage for separator/disabled rows, all-disabled menus,
  navigation from no highlight, repeated access letters, and tiny viewports.
- Record intentional changes separately from extraction: initial highlight,
  activation on left versus other buttons, hover behavior, wheel/touch input
  consumption, padding clicks, and focus restoration.
- Specifically resolve the current Edit/RichEdit difference where Edit's
  popup handler accepts button-down activation without RichEdit's left-button
  guard, and their different wheel/touch handling.

Exit: an agreed behavior matrix covers all three consumers and distinguishes
editor-specific rules from common popup rules. Existing editor command tests
remain the regression baseline.

### 2. Share layout and hit testing

- Introduce a popup layout result with measured bounds and row rectangles.
  Rendering and hit testing must consume the same rectangles.
- Centralize separator heights, row offsets, padding, label/shortcut sizing,
  and viewport clamping. Measure once per relevant layout update rather than
  repeatedly summing preceding rows while rendering.
- Parameterize minimum versus explicit width and anchor placement. Editor
  caret popups can flip above their anchor; cascading menus can flip sideways.
- Migrate Edit first, RichEdit second, then StandardMenu's popup levels.
  Feed submenu placement from the selected row's actual rectangle.
- Keep bottom-sheet and horizontal-trigger layouts explicit, while reusing
  row metrics where applicable.

Exit: separator positions, submenu anchors, viewport edges, and hit regions
agree; existing popup placement tests pass. An oversized popup clips both
painting and hit testing consistently, including negative anchors.

### 3. Share popup and row painting

- Introduce a style value and painter for popup surface/border, highlighted
  rows, separator rules, text, and right-aligned shortcut columns.
- Use the editor popup presentation as the baseline: ten-pixel horizontal
  padding, four-pixel vertical padding, shortcut spacing, and clipping.
- Preserve each control's public color, font, item-height, and width settings
  through adapters. Include checkmarks and submenu indicators for StandardMenu.
- Preserve selected-text contrast rules, including shortcut color when a
  highlight supplies its own foreground.

Exit: equivalent rows render consistently in the three consumers under light,
dark, custom selection colors, and scaled fonts. Review render output for
long labels/shortcuts, narrow viewports, disabled items, and nested popups.

### 4. Share navigation and popup interaction state

- Extract bounded next/previous selectable-row search, Home/End behavior,
  access-letter cycling, and hover selection. Separator/disabled rows never
  become actionable; empty/all-disabled menus terminate safely.
- Extract common open/highlight/close transitions and input decisions such as
  highlight, activate, dismiss, or consume. Owners apply the resulting actions
  through their existing session and command APIs.
- Preserve editor input suppression, selection on right-click, caret anchors,
  and close-before-command behavior. Preserve menu-bar second-click dismissal
  and nested path handling.
- Ensure capture/focus cleanup on dismissal, disable, detach, and disposal.
  Restore pre-open focus according to the agreed owner policy, including after
  switching menu-bar entries.

Exit: integration tests exercise mouse, keyboard, access letters, outside
clicks, and lifecycle cleanup in each consumer. Open editor menus cannot leak
typing, composition, or editing shortcuts into the underlying document.

### 5. Share semantic construction and finish migration

- Supply common semantic construction for popup rows using the repository's
  supported roles/states: label, enabled state, selection, check state, and
  submenu expansion. Decide and document separator exposure to accessibility.
- Let owners supply names, command identity, bounds, and hierarchy; preserve
  their existing public semantic entry points.
- Remove replaced private implementations after all consumers use the shared
  path. Document adapters and extension points in the developer guide.
- Run component dependency checks to verify editors still require no menu
  implementation dependency, and foundation has no upward references.

Exit: semantic bounds match visible rows, public APIs remain compatible, and
no duplicate general popup geometry, row painting, or navigation loops remain
in the three implementations.

## Validation and completion

Deliver each phase in reviewable changes, migrating one consumer at a time.
Use focused helper tests for geometry/navigation edge cases and integration
tests for commands, capture, focus, and input routing.

Relevant suites are `MenuPresentationModeTests`, `StandardEditContextMenuTests`,
`StandardRichEditContextMenuTests`, selection-color tests for all three controls,
and architecture/dependency tests. Finish with the affected projects' complete
test suites and a manual menu-bar/nested/context-menu check in a sample host.

Completion means all three consumers share the primitives above, existing
editor selection/clipboard/undo behavior passes, separators work in each menu
presentation, and any deliberate behavior changes are documented. Replacing
the editor public item records or introducing a new standalone context-menu
control is a separate API decision, not required for this consolidation.
