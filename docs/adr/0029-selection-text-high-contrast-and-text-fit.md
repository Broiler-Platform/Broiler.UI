# ADR 0029 - Selection text, the high-contrast flag, and controls that fit larger text

**Status:** Proposed  
**Date:** 2026-10-04

## Context

Broiler.Hosting builds a high-contrast palette from the user's Windows system colors
(`WindowsTheme.CreateHighContrastTheme`). It maps the system highlight pair onto the theme as
`AccentSoft = Highlight`, but `StandardThemeTokens` had no role for the text on that fill. Every
control that draws a selection drew `Text` on it: list rows, the highlighted drop-down item, the open
menu item, a selected tree row, and selected text in `StandardEdit` and `StandardRichEdit`. With
Windows text on Windows highlight the contrast is 1.4:1 to 1.8:1 in the Windows 11 contrast themes
(Aquatic, Desert, Dusk, Night sky), so a selected row or selected text could not be read.

High contrast itself was inferred in three places (`StandardListView`, `StandardTreeView`,
`StandardCodeEditorPalette`) from the luminance of `Surface` and `Text` being more than 0.9 apart.
Three of the four Windows contrast themes fail that test, so the cues meant for high contrast (the
selected-row outline, glyph decorations, color-independent code marks) were dropped there.

Three more defects showed up in Broiler.Mail at a 200 % text size and on refresh:

- `StandardComboBox` measured `PreferredSize` (32 DIP) and drew 28 DIP drop-down rows whatever its
  font, so scaled text was clipped. Mail sized its combo boxes from the font as a workaround.
- `UiListView.SetItems` kept the row at the top of the view in place across a refresh, but when that
  row itself was removed it kept the raw offset. The rows below jumped up, and a refresh that pushed
  the row off a newer page showed unrelated rows from the middle of the new page.
- `StandardTwoLineListItemPresenter` never shortened its tertiary text (a mail row's date). In a
  narrow row the date ran past the right edge and the list's clip cut it off mid-character.

## Decision

### Selection text roles

- `StandardThemeTokens.SelectionText` is the color of text on the `AccentSoft` selection fill, and
  `SelectionTextMuted` the color of secondary text on it (a row's second line or date). Neither is
  `required`, so existing initializers keep compiling, and the presets leave both unset:
  - `SelectionText` is `Text` until a theme sets it.
  - `SelectionTextMuted` is `TextMuted` while `SelectionText` is `Text`, and `SelectionText` once a
    theme gives selected text a color of its own. A system highlight pair has no muted variant, and
    muted text in the unselected color is not guaranteed to be readable on the highlight.
  - Because they are unset in the presets, `preset with { Text = ... }` carries the new text color onto
    the selection. A host that builds a palette from a preset therefore only sets `SelectionText`.
- `StandardThemeTokens.SelectionTextContrast` reports the ratio of `SelectionText` on `AccentSoft`, and
  `StandardControlPaint.SelectionText` / `SelectionTextMuted` read the shared palette like the other roles.
- Every preset meets WCAG AA (4.5:1) for both roles on `AccentSoft`. The tests check this.

### Controls use the roles

A control draws text on its selection fill in a selected-text color that follows its ordinary text
color until a theme gives the selection a color of its own. `ApplyTheme` writes the theme's value only
when `SelectionText` differs from `Text` (and `SelectionTextMuted` from `TextMuted`). Otherwise it
clears the value so it follows again. The presets therefore draw exactly what they drew before, and a
`Foreground` an application sets after the theme still reaches selected text.

- **Lists.** `UiListItemRenderContext` gains `SelectedForeground` and `SelectedSecondaryForeground`.
  Both are optional and default to `Foreground` and `SecondaryForeground`. `StandardListView` supplies
  them from its new `SelectedForeground` and `SelectedSecondaryForeground` properties. The default and
  two-line presenters draw selected rows with them. On such a selection the unread dot takes the
  selected text color, because the accent can be the selection fill itself (Hosting maps both to
  Highlight). `UiListItemRenderContext.WithItem` copies a context for another item with every member
  kept. A presenter that adapts an item and delegates, as Mail's message presenter does, should use it
  so it does not drop members added later.
- **Drop-downs and menus.** `StandardComboBox.SelectedForeground` colors the highlighted drop-down item.
  `StandardMenu.SelectedForeground` colors the open top-level item and the highlighted popup row;
  disabled items keep `DisabledForeground`.
- **Trees.** `StandardTreeView` draws the selected rows of a focused tree in the selection roles,
  including the secondary label and the expander. An unfocused tree fills its selection with
  `SurfaceDisabled`, which ordinary text reads on, so it is unchanged.
- **Editors.** `StandardEdit.SelectionForeground` (following `Foreground`) and
  `StandardRichEdit.SelectionForeground` (nullable) color selected text. Neither editor splits runs at
  the selection. The line is drawn once per strip (before, on, and after the selection), each pass
  clipped to its strip. Every glyph is drawn in exactly one color, and shaping and kerning are not
  broken at the selection edges. In the rich edit a set color replaces the run colors on the selection,
  because document colors are not chosen to be read on the selection fill. The default `null` keeps
  each run's own color, as before. A disabled rich edit is unchanged.
- **Context menus.** `ContextMenuHighlightForeground` on both editors colors the highlighted row's text
  and, when it has its own color, its shortcut. The dimmed shortcut color is chosen for the menu
  background, not for the highlight.

### An explicit high-contrast flag

`StandardThemeTokens.IsHighContrast` is true in `HighContrastLight` and `HighContrastDark`, and in every
copy of them (`with`, `WithTextScale`, `Select`). It is false otherwise. The three luminance tests now
read `tokens.IsHighContrast ||` the old test. A palette built from a preset, such as Hosting's system
palette, is recognized whatever its colors, and a custom palette at the extremes is still recognized
without the flag.

### Controls fit larger text

- **Combo boxes.** `StandardComboBox` is as tall as its font needs until the application sets
  `PreferredSize`: 32 DIP at the default font, and at a larger font a line of it with the margin 32 DIP
  leaves around a default line. `ItemHeight` follows the font the same way (28 DIP at the default font)
  until the application sets it. This is the margin rule list rows and tab headers already use.
  `UiComboBox.IsPreferredSizeSet` (protected) tells an implementation whether the size is the
  application's. A size the application sets is kept at any font, so a dialog that lays a combo box
  out at a fixed height is unchanged.
- **List anchoring.** When `UiListView.SetItems` finds that the row at the top of the view is gone, the
  nearest row of the old order that is still there keeps its place on screen instead. The rows after the
  anchor are searched first, because they were on screen, then the rows before it. Only when no row
  survives is the offset kept. The usual clamp to the scroll range still applies, so a survivor that
  can no longer sit where it was ends up as close to it as the list allows.
- **Tertiary text.** `StandardTwoLineListItemPresenter` gives the tertiary text the room between 20 DIP
  after the primary text and 8 DIP before the edge. Text that fits is drawn as before. Wider text is
  shortened with an ellipsis and still ends 8 DIP from the edge. When not one character fits before
  the ellipsis, the text is left out and the primary text gets the line. The semantic node keeps the
  full text.

## Consequences

- Hosting should map its system palette's `SelectionText` to `HighlightText` in
  `WindowsTheme.CreateHighContrastTheme`. The palette is built from a preset, so `IsHighContrast` is
  already true. Until Hosting does this, behavior there is unchanged except that the high-contrast cues
  are now drawn.
- Mail's `MailMessageItemPresenter` rebuilds the render context field by field. It must copy
  `SelectedForeground` and `SelectedSecondaryForeground`, or use `WithItem`, or selected message rows
  lose the selection colors. Mail can drop its combo box sizing workaround in `AppearanceController`.
- At the default font and with every preset, all sizes and colors are unchanged. The only new draw
  commands are the per-strip passes, and they appear only when a theme sets a distinct `SelectionText`.
- Not covered:
  - `StandardTabView` draws its selected header in the accent on `Surface`, not on the selection fill.
  - `StandardFormatCodeView` and `StandardCodeEditor` keep their format and syntax colors on the
    selection. Recoloring selected code is a decision for those surfaces and their palettes.
  - `AccentSoft` is also a state fill: a secondary button's hover, a checked or pressed toggle button
    (whose label is drawn in `Accent`), spin box arrows, and the toolbar overflow button. A palette that
    sets `AccentSoft` and `Accent` to the same system highlight makes a checked toggle's label invisible.
    That needs its own role or mapping and is left as a follow-up.
