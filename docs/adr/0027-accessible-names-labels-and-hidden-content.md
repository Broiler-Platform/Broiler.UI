# ADR 0027 - Accessible names, label relations, and hidden content

**Status:** Proposed  
**Date:** 2026-10-02

## Context

Broiler.Mail exposed its control tree to Windows UI Automation for the first time (its bridges had
been constructed before the native window existed). An external client then showed four semantic
problems that originate in Broiler.UI, not in the host bridge (ADR 0008):

- Edits were named by their current text or placeholder ("reader@example.test") rather than by the
  visible label that `FormField` pairs with them. `UiLabel.Target` pointed at the control, but nothing
  used it for naming.
- A read-only `StandardRichEdit` used as a message reader reported its whole document as its name,
  so a screen reader would announce the entire message as the control's name.
- `UiTabView` keeps every tab's content attached. Under the default lifetime policy inactive content
  stays `Visible` (arranged into an empty rectangle), so hosts that walk the element tree exposed the
  content of every tab as on screen.
- Layout-only elements used their type name as their name ("StandardPanel", "TabContent"), and
  scroll views used their scroll offset.

`UiSemanticTextInfo.Value` already separates a text control's value from its name (ADR 0017), and
`UiSemanticState.Offscreen` already exists.

## Decision

- **`UiElement.AccessibleName`** gives any element an explicit name.
- **`UiElement.LabeledBy`** relates an element to the element that names it. Assigning
  `UiLabel.Target` sets it automatically unless the target already has a different label, and
  retargeting removes the label's earlier relation. `FormField` therefore names its control with no
  application changes.
- **Name precedence** is applied centrally in `UiElement.GetSemanticNode()`: a non-blank
  `AccessibleName`, then the label's own name, then the name the control derives itself. The label's
  name is read from its core node, so elements that label each other cannot recurse.
- **Text is a value, not a name.** `UiEdit` falls back to its placeholder (or "Password field") and
  `UiRichEdit` to its placeholder; neither uses its text. The text remains in `TextInfo.Value`, and
  password fields still publish no value.
- **Hidden content.** A container can call the protected `SetHiddenFromAccessibility(child, hidden)`
  for content it keeps alive but does not present. Such a child is left out of the default semantic
  children and reports `Offscreen` without `Visible`; `UiElement.IsHiddenFromAccessibility` (which
  includes ancestors) lets host bridges that walk the element tree skip it. `UiTabView` hides all but
  the selected tab's content and unhides content it removes. `UiSplitContainer` hides a collapsed
  pane, and its splitter while either pane is collapsed: both stay attached and `Visible` but are
  arranged to an empty rectangle. It unhides a pane it replaces.
- **Focus traversal follows the same rules.** `StandardFocusScope.MoveFocus` skips hidden content,
  so Tab never reaches inactive tab content or a collapsed pane, and orders candidates by `TabIndex`
  with a stable sort, so equal indexes keep document order (`List.Sort` is unstable beyond 16
  items). `UiSplitter` is focusable while enabled, since it resizes with the arrow, Page, Home, and
  End keys, and `UiSplitContainer` keeps its children in visual order (first pane, splitter, second
  pane), so traversal and the semantic tree meet the splitter between the panes.
- **Layout elements have no name.** The default `UiElement` node, `UiPanel`, an untitled `UiToolbar`,
  and `UiScrollView` now report an empty name. The type name remains available to hosts as a class
  name.

## Consequences

- Host bridges should map: `Name` to the UIA Name, `TextInfo.Value` to the Value/Text patterns,
  `LabeledBy` to `UIA_LabeledByPropertyId`, `IsHiddenFromAccessibility` and `Offscreen` to
  `IsOffscreen` (or omission from navigation), and Generic/Panel nodes without a name to non-control
  (Raw view) elements.
- A label's text change invalidates only the label's semantics; a host that caches names must
  re-read the target's name, or raise a name-change event for it, when the label changes.
- Applications should name controls that have no visible label, for example a message reader
  (`AccessibleName = "Message text"`).
- Behavior change: the splitter is now a Tab stop, and code that indexes `UiSplitContainer.Children`
  must not assume the splitter comes first.
- Behavior change: code or tests that read an edit's text, or a layout element's type name, from
  `UiSemanticNode.Name` must read `TextInfo.Value` or the element type instead.
