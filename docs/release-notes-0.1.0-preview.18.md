# Broiler.UI 0.1.0-preview.18 release notes — 2026-10-05 (release candidate)

**Status:** release candidate, not published. Written 2026-10-05 for the work of
2026-10-04 and 2026-10-05.

| | |
|---|---|
| Candidate | branch `claude/roadmap-integration` at `fd7657f` (not published) |
| Base | `origin/main` at `cdd54f3` (0.1.0-preview.17, as last fetched); `main` can fast-forward to the candidate |
| Size | 101 commits above the base |
| Decisions | ADRs [0028](adr/0028-disclosure-validation-structure-and-visible-bounds.md) to [0034](adr/0034-unread-dot-on-the-selection-list-frame-and-ring-over-its-bar-and-form-ring-room.md), all still **Proposed** |
| Verified by | the Broiler.UI test suite; through local packs (`0.1.0-preview.18-local.7` = `fd7657f`), Broiler.Hosting's test suite and Broiler.Mail's test suite and native acceptance |
| Pull requests | none; nothing has been published |

Nothing in this file has been released. Package numbers, PR numbers and dates of
publication do not exist yet. Open items found during the round are in the [open
follow-ups of 2026-10-05](open-follow-ups-2026-10-05.md).

## Summary

This release fixes most of the Broiler.UI defects that Broiler.Mail's UI work
(UI-01 to UI-13) and its native acceptance runs found in 0.1.0-preview.17 (the
rest are in the follow-ups), and adds what Broiler.Hosting needs to map them to
UI Automation:

- **Semantics:** a collapsed state and an expand/collapse action,
  controls/described-by/error relations, structure-change events, clip-aware
  visible bounds for elements, list rows and tab headers, and an opt-in keyboard
  stop for read-only scroll views (ADR 0028).
- **High contrast and theme roles:** roles for text on the selection, for state
  fills, for accent-colored text and for scrollbars, and an explicit
  high-contrast flag, so a palette built from the Windows system colors stays
  readable (ADRs 0029, 0031, 0033).
- **Focus visibility:** focus rings picked against the fill they are drawn on, a
  tab view ring on the selected header, and room for a field's ring inside forms
  (ADRs 0031, 0032, 0034).
- **Layout and input:** arrange invalidation reaches the root, hidden tabs keep
  their layout, the tilt wheel and Shift+wheel scroll the right way under
  Broiler.Hosting, Broiler.Input and Broiler.Graphics, and the gallery's web
  host (web hosts must pass `deltaX` unnegated; a tilt with Shift held is a
  recorded limit), and toolbar and tab-strip keys stay where they belong
  (ADR 0030).
- **Text fit:** combo boxes sized from their font, a drop-down arrow that grows
  with it, and two-line rows that never cut a date (ADRs 0029, 0033).
- **Scroll stops:** a read-only scroll stop hands focus on when it stops being
  one, and is a stop exactly while an `Auto` bar shows (ADRs 0032, 0034).

All new API is additive. The Light and Dark presets render differently in a few
places, listed under [What renders differently in the
presets](#what-renders-differently-in-the-presets).

## Topic branches

`claude/roadmap-integration` contains every topic branch below; no further merge
is outstanding.

| Branch | Tip | ADR | Content |
|---|---|---|---|
| `claude/semantics-disclosure-validation` | `66b6bc4` | 0028 | collapsed state, `IUiExpandable`, `Discloses`/`Controls`, `DescribedBy`/`ErrorMessage`/`Description`, `StructureChanged` batching, `GetVisibleBounds` and clips, `GetTabHeaderBounds`, scroll view focus ring and `FocusWhenScrollable`, relation-cycle crash fix |
| `claude/hc-selection-and-controls` | `cd25a26` | 0029 | `SelectionText`/`SelectionTextMuted`, `IsHighContrast`, `StateFill`/`StateText`, combo box sized from its font, list anchor fallback, two-line date clamp |
| `claude/layout-input-fixes` | `0fdab3b` | 0030 | arrange invalidation, hidden tabs keep their arrangement, tilt and Shift+wheel direction, toolbar keys skip what Tab skips, forms keep the focused field in view, tab-strip keys only while the strip has focus, tree row semantics |
| `claude/tabview-focus-contrast` | `7d5a8ed` | 0031 | tab page clip, ring on the selected header, selected-tab bar, `AccentText`, `FormSection.ShowText`/`HideText` |
| `claude/focus-handoff-and-rings` | `11c5cb7` | 0032 | focus rings on the drawn fill, focus hand-off from a scroll stop, `FindAdjacentStop` |
| `claude/scrollbar-roles-combo-arrow` | `161ec5c` | 0033 | scrollbar track and thumb roles, opaque bars beside the text, a drop-down arrow slot that grows with the font |
| `claude/list-form-polish` | `debcea3` | 0034 | unread dot on the selection, list frame and ring over its bar, ring room and feedback spacing in forms |
| `claude/scroll-stop-tolerance` | `fd7657f` | 0034 (extended) | `ScrollbarGap`, scroll sizes of the last arrange, one half-DIP tolerance for `Auto` bars and scroll stops |

The integration branch starts from `claude/semantics-disclosure-validation`
(`66b6bc4`, on its first-parent line), merges `claude/hc-selection-and-controls`
twice (`fa747cd` at `c4c7a1b`, then `a821db5` at `cd25a26`) and
`claude/layout-input-fixes` once (`8334e69`); the other five were cut from it in
turn, so each contains the ones above it.

## Changes by decision

### ADR 0028 — disclosure, validation relations, structure events, visible bounds

New API:

- `UiSemanticState.Collapsed`.
- `IUiExpandable` (`IsExpanded`, `Expand()`, `Collapse()`, each returning
  whether anything changed), implemented by `FormSection`, and explicitly by
  `UiComboBox` and `UiMenu`.
- On `UiElement`: `Discloses`, `Controls`, `DescribedBy`, `ErrorMessage`, a
  virtual `IsRequired`, `GetVisibleBounds()`, protected virtual
  `GetClipBoundsForChild(child)` and protected `NotifyStructureChanged()`.
- `UiSemanticNode.Description` (init-only; the record's constructor and
  deconstruction are unchanged).
- `UiTabView.GetTabHeaderBounds(index)` (virtual, empty by default), implemented
  by `StandardTabView`.
- `UiListView.IndexOf(string)` is now public; protected
  `UiListView.ClipItemSemanticNode`.
- `StandardScrollView`: `FocusRing`, `FocusWhenScrollable` (off by default), and
  `ApplyTheme` — it is now an `IStandardThemedControl`, so
  `StandardThemeController` themes it.

Behavior changes:

- A `FormSection` toggle reports `Expanded` or `Collapsed`, discloses the
  section and controls its `Content`; the group reports neither. Closed combo
  boxes and menus and collapsible tree rows report `Collapsed`.
- A control inside a `FormField` reports `Invalid` and `Required`, and its
  description starts with the shown error. Nothing new is announced.
- Relations that lead back to the element (`LabeledBy`, `DescribedBy`,
  `ErrorMessage`) no longer crash with a stack overflow. The `LabeledBy` case
  predates this release.
- `StructureChanged` is raised for the first time. Changes made during
  `UiSession.DispatchInput` or `RenderFrame` are raised once per element when
  the call returns; others as they happen.
- Content clipped entirely out of a scroll view reports `Offscreen` (still
  `Visible`). List rows out of view are `Offscreen` and not `Visible`; partly
  visible rows and tab nodes carry clipped bounds.
- A focused `StandardScrollView` that is a keyboard stop draws a theme focus
  ring.

### ADR 0029 — selection text, state fills, the high-contrast flag, text fit

New API:

- `StandardThemeTokens`: `SelectionText`, `SelectionTextMuted`,
  `SelectionTextContrast`, `StateFill`, `StateText`, `StateTextContrast`,
  `IsHighContrast`. None is `required`; the presets leave the roles unset, so
  they follow `Text`, `TextMuted` and `AccentSoft` and render as before.
- `StandardControlPaint`: `SelectionText`, `SelectionTextMuted`, `StateFill`,
  `StateText`.
- Lists: `UiListItemRenderContext.SelectedForeground`,
  `SelectedSecondaryForeground` and `WithItem`;
  `StandardListView.SelectedForeground` and `SelectedSecondaryForeground`.
- `StandardComboBox.SelectedForeground`, `StandardMenu.SelectedForeground`.
- Editors: `StandardEdit.SelectionForeground`,
  `StandardRichEdit.SelectionForeground` (nullable),
  `ContextMenuHighlightForeground` on both;
  `StandardFormatCodeView.SelectionForeground` and
  `CodeEditorPalette.SelectionForeground` (both nullable).
- State labels: `StandardButton.SecondaryHoverForeground`,
  `StandardToggleButton.CheckedForeground`, `StandardSpinBox.ArrowHoverColor`,
  `StandardToolbar.OverflowOpenBackground` and `OverflowOpenForeground`.
- Protected `UiComboBox.IsPreferredSizeSet`.

Behavior changes:

- Controls draw text on the selection fill in `SelectionText` when a theme sets
  it: list rows, the highlighted drop-down item, menus, a focused tree's
  selected rows (including decorations), edit and rich edit selections (also
  when disabled), both editors' context menus, the format code view and the code
  editor.
- A hovered secondary button, a checked/indeterminate/pressed toggle button, a
  hovered spin box arrow and the open toolbar overflow chevron are drawn on
  `StateFill` with `StateText`; the code editor's bracket match is filled with
  `StateFill` (drawn over the bracket, see the follow-ups, V-9).
- `IsHighContrast` turns on the high-contrast cues (list outline, tree glyphs,
  color-independent code marks) in palettes built from a high-contrast preset,
  such as Hosting's system palette. In high contrast the focus ring of a
  focused, selected list row is drawn 2 DIP inside the selection outline.
- `StandardComboBox` sizes the box and its drop-down rows from its font unless
  `PreferredSize` or `ItemHeight` is set (32 and 28 DIP at the default font, as
  before).
- `UiListView.SetItems` anchors on the nearest surviving row when the top row is
  removed.
- `StandardTwoLineListItemPresenter` draws the date whole or not at all; it is
  left out where it does not fit beside the first three characters of the
  sender. The semantic name keeps the full date.

### ADR 0030 — layout invalidation, hidden tabs, input direction, keyboard scope, tree rows

No public API changes. Behavior changes:

- `Invalidate(Arrange)` and a desired-size change in `Measure` mark every
  ancestor, so an offset change below an element left arrange-invalid moves the
  content in the same frame.
- `StandardTabView` no longer arranges inactive content at an empty rectangle,
  under either lifetime policy: hidden tabs keep their layout and scroll
  positions, and report their last bounds. The shown tab is measured once per
  pass when a stack arranges it at the height it asked for.
- `UiSession.HitTest` skips content hidden with `SetHiddenFromAccessibility`.
- A wheel tilted right scrolls right in `StandardScrollView`, `StandardRichEdit`
  and `StandardFormatCodeView` (it scrolled left). Shift+wheel keeps its
  direction in both shapes hosts send; under Broiler.Hosting.Windows
  `StandardCodeEditor` and `StandardTreeView` now scroll right for Shift+wheel
  turned towards the user. A control that cannot scroll sideways leaves a tilt
  unhandled. The WebAssembly gallery passes `deltaX` unnegated.
- `StandardToolbar` arrow, Home and End keys land only where Tab can, and the
  bar still consumes them when nothing on it can take focus.
- `StandardTabView` acts on Left, Right, Home and End only while it is the
  focused element.
- `FormViewport` keeps the focused control in view when its viewport shrinks.
- `UiTreeView` invalidates Semantic when `FirstVisibleRow` changes and when the
  number of described rows changes, and describes a row in constant time after
  one pass per row set.

### ADR 0031 — tab view page clip, header marks, accent text, disclosure toggle text

New API:

- `StandardThemeTokens.AccentText` and `AccentTextContrast`;
  `StandardControlPaint.AccentText`. `AccentText` follows `Accent` unless set.
  Light sets #0A61BE and Dark #7AB7FF; a preset's value is tied to its accent,
  so `Dark with { Accent = brand }` draws the brand accent as text.
- `StandardTabView.SelectedHeaderForeground`, `SelectedIndicatorThickness`
  (2 DIP, 0 = none), `SelectedIndicatorColor`.
- `FormSection.ShowText` and `HideText` (null or blank keeps "Show {Title}" /
  "Hide {Title}").

Behavior changes:

- Selected tab content is drawn, hit-tested and reported inside the page's 1 DIP
  frame. Layout is unchanged.
- A focused tab view strokes its ring around the selected header (only the part
  inside the view; around the visible strip when less than 48 DIP of the header
  shows), at least 2 DIP thick in high-contrast palettes, and keeps it out of
  the label's line at any header height.
- The selected tab has a 2 DIP bar under its label, drawn inside the view.
- The selected tab label, `StandardLabel`'s Accent role, themed toggle button
  labels and icons, and the format code view's inline codes use `AccentText`.
- An unthemed tab view captures its selected label color when it is built, as it
  captures its fills.
- A themed toggle button uses `StateText` on its state fills whenever its label
  or the accent is the state fill.

### ADR 0032 — focus rings on the drawn fill, focus hand-off from a scroll stop

New API: `StandardControlPaint.FocusRingColor(ring, fill, label)` and
`StandardFocusScope.FindAdjacentStop(from, direction, scopeRoot = null)`.

Behavior changes:

- `StandardButton` and `StandardToggleButton` keep the theme's ring where it
  reaches 3:1 on the fill drawn in the current state, and draw it in that
  state's label color otherwise. Default buttons now ring in `OnAccent` at rest
  and hovered in every preset, and pressed in Light and both high-contrast
  presets.
- `StandardSpinBox` applies the same rule to its frame, draws an arrow fill the
  ring does not stand out from inside the ring, and, themed through
  `StandardThemeController`, no longer draws its edit's border or ring inside
  its frame.
- A focused `FocusWhenScrollable` view that stops being a stop hands focus to
  the next tab stop (or the previous one) through the session dispatcher when it
  is next drawn, and brings that stop into view only when none of it shows.
  Setting `FocusWhenScrollable` invalidates Render as well as Semantic.
- The `FocusRing` properties keep the value set or themed; the color drawn is
  decided at render time.

### ADR 0033 — scrollbar roles, a drop-down arrow that grows with the font

New API: `StandardThemeTokens.ScrollbarTrack`, `ScrollbarThumb` and
`ScrollbarThumbContrast`; `StandardControlPaint.ScrollbarTrack`,
`ScrollbarThumb` and `ScrollbarColors(theme, track, thumb)`. The roles default
to `SurfaceDisabled` and `BorderStrong`, the colors the list, tree and code
editor always drew.

Behavior changes:

- The list, tree and code editor draw the roles. The scroll view, rich edit and
  format code view draw them when the theme sets either role, is high contrast,
  or has surface and text at the extremes of lightness; otherwise they keep
  their translucent bars. A scrollbar color the application set is kept across
  themes.
- When a bar color is fully opaque, `StandardRichEdit` keeps a
  `ScrollbarThickness` strip beside (and below) the text for each bar its policy
  allows, and `StandardFormatCodeView` ends its text where the bar starts.
- In the high-contrast presets a focused scroll view redraws its ring where it
  crosses the thumb.
- `StandardComboBox`'s arrow slot and text clip grow with the font (18 and
  22 DIP at the default font, 36 and 44 at 200 %), also when `PreferredSize` is
  set.

### ADR 0034 — unread dot, list frame and ring, form ring room, scroll stop as arranged

New API: `UiListItemRenderContext.AccentText` (copied by `WithItem`),
`StandardListView.AccentText`, `StandardScrollView.HorizontalContentInset`,
`VerticalContentInset` and `ScrollbarGap`, and
`StandardControlPaint.PillAreasUnderRing`.

Behavior changes:

- The two-line presenter picks the unread dot against the fill under it
  (selection text color, then `Accent`, then `AccentText`, then the row's text
  color). Only Dark's selected row changes: #7AB7FF instead of #2673CE.
- `StandardListView` strokes its frame after the rows and the bar, so the right
  edge is continuous; a focused list redraws its ring over an opaque thumb it is
  lost on, clipped to the thumb's pill. The scroll view's redraw (ADR 0033) is
  clipped to the pill too.
- `FormViewport` sets both content insets to 1 DIP and `ScrollbarGap` to 2 DIP;
  `FormSurface` reaches its viewports 1 DIP into the margins and leaves 4 DIP
  between the action strip and the feedback. Fields keep their bounds while no
  bar shows; beside a shown bar they are 2 DIP narrower.
- Once arranged, a `StandardScrollView` keeps the `ViewportSize` and
  `ExtentSize` of its last arrange through a measure at another size, and
  decides `FocusWhenScrollable` on the view as arranged.
- An overflow of no more than 0.5 DIP shows no `Auto` bar and makes no stop, but
  can still be scrolled by that fraction. With `Auto` bars a stop exists exactly
  while a bar shows. This tolerance needs the owner's sign-off.

## What renders differently in the presets

At the default font and with no scrollbar shown, layouts are unchanged except
where listed.

| Area | Light and Dark | High-contrast presets |
|---|---|---|
| Tab view | ring on the selected header instead of the whole view; 2 DIP bar under the selected label; page content clipped 1 DIP inside the frame | the same, ring 2 DIP thick |
| Accent-colored text (selected tab, Accent labels, themed toggles, inline format codes) | Dark #2673CE → #7AB7FF; Light #0B6FD8 → #0A61BE | unchanged |
| Default button ring | `OnAccent` at rest and hovered (and pressed in Light) | `OnAccent` at rest, hovered and pressed |
| Spin box themed through the controller | no inner edit border or ring | the same |
| Focused scroll view that is a keyboard stop (`Focusable`, or `FocusWhenScrollable` while it can scroll) | theme focus ring (new; preview.17 drew none) | the same, ring redrawn across the thumb |
| Scroll view, rich edit, format code view bars | unchanged (translucent) | text color thumb on a surface track (21:1); rich edit text 12 DIP narrower and format code view text 4 DIP narrower where a bar can show; ring redrawn across the scroll view's thumb |
| List | frame continuous along the bar (no stray corner arc); Dark selected unread dot #7AB7FF | frame as in Light/Dark; ring redrawn across the thumb in the surface color; a focused selected row's ring inside the outline |
| Two-line rows | date drawn whole, or left out where it does not fit | the same |
| Forms (`FormSurface`/`FormViewport`) | viewport clip and vertical bar 1 DIP further out at each side and end; whole field rings; feedback 4 DIP below the action strip; content 2 DIP short of a shown bar | the same; 2 DIP of surface between a focused field's ring and the thumb |
| Scroll views with `Auto` bars | no bar for an overflow of 0.5 DIP or less | the same |
| Combo box at a larger font | taller box and rows unless sized by the app; arrow slot and text clip grow | the same |

Text on selection and state fills changes only in palettes that set
`SelectionText`, `StateFill` or `StateText` (Hosting's system palette does); the
presets leave them unset.

## Compatibility notes for consumers

### All consumers

- **API.** Every change is additive: new members, interfaces now implemented
  (`IUiExpandable` on `UiComboBox`, `UiMenu` and `FormSection`;
  `IStandardThemedControl` on `StandardScrollView`), `UiListView.IndexOf`
  widened from protected to public, new defaults that equal the old values in
  the presets (`StandardScrollbar.Track` and `Thumb` read the scrollbar roles),
  and one new default that changes in Light and Dark
  (`StandardFormatCodeView.InlineCodeForeground` reads `AccentText` instead of
  `Accent`; see the accent-colored text row of the table above). No package was
  added or removed, and no production project reference changed. Package
  validation is not enabled in this repository, so no API-compatibility tool
  checked this; it rests on the slice reviews. A subclass that declares a member
  named like one of the new `UiElement` members gets a hiding warning when it
  recompiles.
- **Pair it with Broiler.Hosting 0.1.0-preview.7.** Hosting preview.7
  (`claude/roadmap-integration` at `c388a66`, not published) maps ADR 0028 to UI
  Automation, coalesces `StructureChanged` per frame on the parent's peer, and
  sets the state, accent and scrollbar roles in its system contrast palette. It
  needs this release's API, so it is published after it. Hosting as released
  (preview.5 in Mail, and preview.6 = `main` at `380e0e8`) handles every
  `StructureChanged` by cleaning its peer tables and raising a
  children-invalidated event on the window root, and on `main` its contrast
  palette leaves `StateText` at the window text, so the hovered-secondary and
  toggle-state rings stay as faint as that label (ADR 0032). Take the two
  releases together. This UI release was not tested with an older Hosting.
- **Visual baselines change** as listed in the table above. Tests or pixel
  baselines that look for the tab view's ring at `Inset(Bounds, 2)`, compare a
  tab page's `GetVisibleBounds()` with its `Bounds`, hit-test at a page edge,
  expect Light's or Dark's accent as label color, pin form viewport bounds, or
  pin a field's width beside a form's bar need new values.
- **Behavior a test can see:** `SemanticChanged` also carries `StructureChanged`
  (filter by kind); trees raise a semantic invalidation on every scroll;
  inactive tab content reports its last bounds instead of an empty rectangle
  (test `IsHiddenFromAccessibility` or `Visibility`); Left/Right/Home/End reach
  a tab view's ancestors unless the tab view has focus; `ViewportSize` and
  `ExtentSize` of an arranged scroll view are those of its last arrange.
- **Adopt:**
  - `UiListItemRenderContext.WithItem` in any presenter that adapts an item and
    delegates. A presenter that rebuilds the context member by member drops
    `SelectedForeground`, `SelectedSecondaryForeground` and `AccentText`.
  - `StandardScrollView.FocusWhenScrollable`, with an `AccessibleName`, on
    read-only scroll areas, in place of an application's own scroll-stop rule.
    The toolkit then rings the area and hands focus on.
  - `FormSection.ShowText` / `HideText` for sentence-case or translated toggle
    text.
  - `StandardQueuedUiDispatcher` in applications that have a tree view or host a
    Hosting bridge. With `ImmediateUiDispatcher` a host's tree comparison runs
    mid-layout, and the scroll-stop hand-off happens while the view is drawn.
  - `StandardThemeController.ApplyToSubtree` for content built after a theme is
    applied; selection roles and session themes reach controls only through
    `ApplyTheme`.
  - Set `AccentText` (and `StateFill` with `StateText`) together with a brand
    `Accent` in a copied preset, to keep accent text readable.
  - Theme toggle buttons: an unthemed `StandardToggleButton` keeps `Accent` on
    fixed light fills.
  - Drop workarounds this release makes unnecessary: combo box sizing from the
    font (where no fixed width is needed), a wrapper that keeps hidden tabs from
    being arranged, application-side default-button ring colors,
    application-side scroll-stop rules.
  - Hosts that convert DOM wheel events pass `deltaX` unnegated.
  - Hosts with their own Tab order may use
    `StandardFocusScope.FindAdjacentStop`.

### Broiler.Mail

Mail's adoption branch `claude/ui-09-upstream-adoption` already makes every
change this release needs, against `0.1.0-preview.18-local.7` and Hosting
`0.1.0-preview.7-local.5`. Its native runs and test suite were verified at
`e870135`; the branch has since been rebased onto `claude/ui-13-acceptance` at
`70b7017` (code tip `12ceded` plus documentation), is now at `c1ee282` (39
commits above `70b7017`), and passed its suite again there, according to Mail's
adoption record. The branch: `MailMessageItemPresenter` uses `WithItem`; 'Inbox
notice', 'Message header' and 'Status and errors' use `FocusWhenScrollable`; the
reader's editors draw focus rings; the combo box sizing workaround, `TabContent`
and `DefaultButtonFocus` are removed; the collapsible sections set sentence-case
`ShowText`/`HideText`; `FocusNavigation.Reveal` keeps `VerticalContentInset`;
and the layout and unread-dot tests expect the new geometry and colors.

- Mail needs a Broiler.UI cut at or after `fd7657f`: ADRs 0031 and 0034 for the
  adoption branch to compile, and ADRs 0028–0030, 0032 and 0034 for its tests.
  Built against a release without ADR 0034, `Broiler.Mail.Application` and
  `Broiler.Mail.Tests` do not compile.
- Mail `main` and the published stack (`claude/ui-13-acceptance`) stay on
  preview.17 and Hosting preview.5 and do not depend on this release. Moved to
  preview.18 without the adoption branch, their presenter, which copies the
  context member by member, would draw Dark's selected unread dot in the row's
  text color (#F2F4F8) instead of the accent text, and they would need the test
  and baseline updates ADR 0034 lists. This was not tried.
- After the release, Mail refreshes the screenshot baselines ADRs 0031 and 0034
  change.

### Broiler.Code

Not built against this release this round. From the ADRs:

- `CodeShellFactory`'s content-less `StandardTabView` can be wider than its
  pane. The ring and the bar now stay inside the view, but header labels past
  its edge still draw over the next pane; clip the strip to its pane
  (`StandardTabView` ignores `VisibleTabCapacity` in layout).
- Under Broiler.Hosting.Windows, Shift+wheel turned towards the user now scrolls
  `StandardCodeEditor` and `StandardTreeView` right.
- A tree raises a semantic invalidation on every scroll and on resizes that
  change its rows in view; use `StandardQueuedUiDispatcher` before relying on
  Hosting's tree rows.

### Broiler.Writer

Not built against this release this round. From the ADRs:

- Stop negating `event.deltaX` in
  `src/Broiler.Writer.WebAssembly/wwwroot/main.js` (keep `-event.deltaY`) in the
  same change that adopts preview.18, or sideways scrolling runs backwards in
  `StandardRichEdit` and `StandardScrollView`. (ADR 0030 records this; the file
  was not reopened this round.)
- In high contrast, or with an opaque scrollbar color the app sets,
  `StandardRichEdit` text wraps 12 DIP short of the bar and
  `StandardFormatCodeView` text 4 DIP short; inline format codes use
  `AccentText`.
- Writer uses `ImmediateUiDispatcher` by default; move to
  `StandardQueuedUiDispatcher` before relying on Hosting's tree rows or
  structure coalescing.

### Broiler.Browser

Not built against this release this round. No Browser-specific follow-up was
recorded. The general notes apply, in particular the visual baseline changes and
the move from `ImmediateUiDispatcher` (its default) to
`StandardQueuedUiDispatcher` where it hosts a tree or a Hosting bridge.

## How it was verified

### Broiler.UI

Each slice was implemented, reviewed, and its confirmed review findings fixed.
The reports of the first seven slices state that every new or changed behavior
test was checked to fail with its fix reverted; the last slice's tests are
listed in ADR 0034. Full suite (`dotnet test Broiler.UI.slnx -c Release`) at
each topic branch tip, two full runs each unless noted:

| Revision | Tests passed |
|---|---:|
| `cdd54f3` (base, preview.17) | 862 |
| `66b6bc4` semantics-disclosure-validation | 905 |
| `cd25a26` hc-selection-and-controls | 986 |
| `0fdab3b` layout-input-fixes | 907 |
| `7d5a8ed` tabview-focus-contrast (includes the three above) | 1124 |
| `11c5cb7` focus-handoff-and-rings | 1175 |
| `161ec5c` scrollbar-roles-combo-arrow | 1250 |
| `debcea3` list-form-polish | 1312 |
| `fd7657f` release candidate (coordinator's run; number of runs not recorded) | **1333** |

Clean Release builds reported 0 errors and the same 27 pre-existing warnings
(xUnit2031, xUnit2029, CS8625 in existing test files). The changed projects of
the state-role slice also built with 0 warnings under `-p:IsAotCompatible=true`.

The checks are render-list and session tests (colors, clip and stroke
rectangles, sizes, focus and semantics). The Win32 demo fails at startup on
`main` and on this branch (see the follow-ups, C-1). The only native run in this
repository was the ADR 0032 slice's temporary, uncommitted probe of the demo,
which moved the animation registration into `OnCreated` and captured the button
rings and spin box in Light and in Hosting-shaped Aquatic and Dusk palettes, and
the scroll-stop hand-off. All other native evidence comes from Broiler.Mail.

### Local packs

| Pack | Commit | Used for |
|---|---|---|
| `0.1.0-preview.18-local.1` | `fa747cd` | Hosting UIA mapping |
| `local.2` | `8334e69` | Mail adoption, first round |
| `local.3` | `7d5a8ed` | Hosting accent text; Mail suite check |
| `local.4` | `11c5cb7` | Mail adoption after ADR 0032 |
| `local.5` | `161ec5c` | Hosting scrollbar roles |
| `local.6` | `debcea3` | Mail adoption after ADR 0034; performance comparison |
| `local.7` | `fd7657f` | Hosting release candidate (`0.1.0-preview.7-local.5`); Mail final runs |

The packs are in the local feed `D:\local-packages\roadmap-2026-10-04`. The
commit of each was read from its nuspec.

### Broiler.Hosting

`claude/roadmap-integration` at `c388a66` was packed as
`0.1.0-preview.7-local.5` against `local.7`, and its tests pass. Earlier slices
also ran an external UIA2 client against a probe window (23 of 23 checks, JIT
and NativeAOT).

### Broiler.Mail (adoption branch at `e870135`, `local.7` with Hosting `local.5`)

These results are from `e870135`, before the branch was rebased onto `70b7017`.
After the rebase (tip `c1ee282`) the suite passed again, according to Mail's
adoption record; the native runs below were taken at `e870135`.

- Test suite: **934 passed** (640 shared, 291 Windows, 3 Linux), twice; each of
  the branch's 38 commits above the acceptance tip also passes.
- Native acceptance (`final3`; NativeAOT win-x64, one monitor 3840x2160 at
  150 %, Windows 11 Enterprise 26200): full matrix 132/132; compact reader
  20/20; 200 % text 44/44; simulated Windows 11 contrast palettes aquatic,
  desert, dusk, night-sky and the high preset 10/10 each; Accept-Refresh 4/4;
  Probe-Uia 6/6; a Record-Uia transcript of focus, selection, notification and
  validity events.
- Visual review 5 (226 screenshots of the `final3` captures, both Mail stacks)
  confirmed the toolkit fixes of the earlier reviews and found one open
  Broiler.UI item: the tab ring flush against the window frame in contrast
  palettes.
- Performance (Mail's performance record of 2026-10-05, measured with `local.6`,
  not `local.7`): the adoption build was 0.31 to 0.37 ms slower at p50 in the
  scroll, theme, select and splitter workloads than the published build, and
  allocated 5–7 KB more per frame. The cause was not profiled; the adoption
  build also differs from the published one in Hosting and Mail code.

### Not verified

- No real screen reader, real Windows contrast theme, OS text size, real DPI
  change, physical tilt wheel or touchpad, or real CJK IME. Contrast palettes
  were simulated from the Windows 11 themes' colors.
- Broiler.Code, Broiler.Writer and Broiler.Browser were not built against this
  release.
- No hosted CI run: CI runs on pull requests and on `main`, and no pull request
  exists yet.

## Release steps

1. **Decisions.** The owner signs off the items in [open
   follow-ups](open-follow-ups-2026-10-05.md#design-decisions-for-the-owner), at
   least the 0.5 DIP `Auto` bar tolerance, which this release ships, and accepts
   ADRs 0028–0034 (status Proposed → Accepted). Before accepting them, correct
   the Consequences of ADR 0030 and ADR 0032 as the follow-ups list them
   ([C-8](open-follow-ups-2026-10-05.md#c-8-adr-text-to-correct-before-acceptance)).
2. **Merge.** Push what is not yet on GitHub and merge into `main`, either
   `claude/roadmap-integration` as one pull request or the topic branches in the
   order of the table above. `main` is an ancestor of `fd7657f`, so no conflict
   is expected if `main` has not moved since it was last fetched.
3. **CI.** Let CI build and test the merge commit (`ci.yml` builds Release and
   runs the suite; 1333 tests at `fd7657f`). Build `-c Release-Windows` locally
   as well, which includes the samples and is not part of CI.
4. **Publish.** Run the Publish workflow (`.github/workflows/publish.yml`) on
   `main` at or after `fd7657f`, first as a dry run, then for real, with version
   suffix `preview.18` (or let it choose the next unused preview number, which
   is preview.18 after preview.17; a `v0.1.0-preview.18` tag also publishes).
   Check that the published API matches `local.7`.
5. **Hosting preview.7 next.** Rebuild `Broiler.Hosting`
   `claude/roadmap-integration` (`c388a66` or later) against the published
   preview.18, run its tests, merge it, and publish 0.1.0-preview.7 (it also
   needs Broiler.Native preview.7, already on NuGet). Publish it soon after this
   release, since consumers should take the two together.
6. **Mail.** Rebase the adoption branch onto the then-current
   `claude/ui-13-acceptance` (or `main`, once that is merged), build it without
   the `-p` overrides so the published packages restore, then run the full suite
   twice, Accept-UI (default, dusk, aquatic), Accept-Refresh and Probe-Uia
   before merging it.
7. **Afterwards.** Update `docs/roadmap.md` and the follow-ups file.
   `HUMAN_REVIEW.md` stays `PENDING`; this release does not change it. The
   round's new declarations carry no `Broiler-AI`/`Broiler-Human` lines yet
   ([C-10](open-follow-ups-2026-10-05.md#c-10-review-annotations-for-the-rounds-declarations)).
