# ADR 0029 - Selection text, the high-contrast flag, and controls that fit larger text

**Status:** Proposed  
**Date:** 2026-10-04

## Context

Broiler.Hosting builds a high-contrast palette from the user's Windows system colors
(`WindowsTheme.CreateHighContrastTheme`). It maps the system highlight pair onto the theme as
`AccentSoft = Highlight`, but `StandardThemeTokens` had no role for the text on that fill. Every
control that draws a selection drew `Text` on it: list rows, the highlighted drop-down item, the open
menu item, a selected tree row, and selected text in `StandardEdit`, `StandardRichEdit`,
`StandardFormatCodeView` and `StandardCodeEditor`. With
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
  narrow row the date ran past the right edge and the list's clip cut it off mid-character, and the
  primary text (the sender) was cut to a bare ellipsis to make room for it.

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
- **The focus ring in high contrast.** The selected-row outline and the focus ring of a focused, selected
  row used the same rectangle, and the ring was drawn last, so the outline never showed on that row. In
  high contrast the ring is now drawn 2 DIP inside the outline. It is drawn in `FocusRing` unless that has
  less than 3:1 against the selection fill, as when a system palette uses the highlight for both; then it
  takes the selected text color. Outside high contrast the ring is unchanged.
- **Drop-downs and menus.** `StandardComboBox.SelectedForeground` colors the highlighted drop-down item.
  `StandardMenu.SelectedForeground` colors the open top-level item and the highlighted popup row;
  disabled items keep `DisabledForeground`.
- **Trees.** `StandardTreeView` draws the selected rows of a focused tree in the selection roles,
  including the secondary label, the expander, and the row's decoration (its glyph in high contrast,
  its shape otherwise; the error mark's hole then shows the selection fill). The status colors are chosen
  for the surface, and the glyph or shape still tells the decorations apart. An unfocused tree fills its
  selection with `SurfaceDisabled`, which ordinary text reads on, so it is unchanged.
- **Editors.** `StandardEdit.SelectionForeground` (following `Foreground`) and
  `StandardRichEdit.SelectionForeground` (nullable) color selected text. Neither editor splits runs at
  the selection. The line is drawn once per strip (before, on, and after the selection), each pass
  clipped to its strip, so shaping and kerning are not broken at the selection edges. Each strip draws
  its glyphs in one color; ink that overhangs a selection edge, such as an italic glyph's, is cut between
  the two colors there. A strip that holds none of the line is not drawn: where the selection reaches
  the start or the end of the line the selected strip runs on to the edge, and a line selected from end
  to end is drawn once, as it is without a selection. In the rich edit a set color replaces the run
  colors on the selection, because document colors are not chosen to be read on the selection fill. The
  default `null` keeps each run's own color, as before. A disabled editor still fills its selection, so
  its selected text takes the selection color as well; its dimmed text is no more readable on the fill.
- **Code views.** `StandardFormatCodeView.SelectionForeground` and `CodeEditorPalette.SelectionForeground`
  (both nullable, set from the theme by `ApplyTheme` and `StandardCodeEditorPalette.FromTokens` in the
  same way) replace the token and classification colors on the selection; tokens keep their weight and
  slant, so codes and keywords still stand apart. Both views lay text out on a character grid, already
  split at token or classification edges, so a token is split at the selection's edges too and the
  pieces line up with the fill exactly. The code editor applies it only while it has focus, since its
  unfocused selection is `InactiveSelection`, which the classification colors read on.
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
  nearest row of the old order that is still there keeps its place on screen instead. The old order is
  searched outward from the anchor; at equal distance the row after the anchor wins, because it was on
  screen. Only when no row survives is the offset kept. The usual clamp to the scroll range still applies, so a survivor that
  can no longer sit where it was ends up as close to it as the list allows.
- **Tertiary text.** `StandardTwoLineListItemPresenter` draws the tertiary text whole or not at all,
  right-aligned 8 DIP from the edge. A shortened date or time reads as a different one ("10:..." for
  "10:42"), and the primary text has priority: the tertiary text is drawn only when it fits beside the
  10 DIP gap and the least of the primary text that is still readable - its first three characters and
  an ellipsis, or all of it when that is narrower, and never less than 10 DIP, the least a date that
  fitted left it before. Otherwise it is left out and the primary text gets the line. The semantic node
  keeps the full text. A row wide enough for the date and the start of the primary text is drawn as
  before.

## Consequences

- Hosting should map its system palette's `SelectionText` to `HighlightText` in
  `WindowsTheme.CreateHighContrastTheme`. The palette is built from a preset, so `IsHighContrast` is
  already true. Until Hosting does this, behavior there is unchanged except that the high-contrast cues
  are now drawn.
- Mail's `MailMessageItemPresenter` rebuilds the render context field by field. It must copy
  `SelectedForeground` and `SelectedSecondaryForeground`, or use `WithItem`, or selected message rows
  lose the selection colors. Mail can drop its combo box sizing workaround in `AppearanceController`.
- At the default font and with every preset, all sizes and colors are unchanged, with two exceptions:
  in the high-contrast presets the focus ring of a focused, selected list row now sits inside the
  selection outline, and a two-line row too narrow for its date beside the start of its primary text
  leaves the date out instead of reducing the primary text to an ellipsis. The only new draw commands
  are the per-strip passes and the split code tokens, and they appear only when a theme sets a distinct
  `SelectionText`.
- The built-in file and font dialogs give their combo boxes explicit sizes, like their edits and
  buttons, so they keep their fixed geometry at a larger text size. Fitting those dialogs to larger text
  is a layout change of its own. `PreferredSize` sets a width and a height together, so an application
  that sets only a width also fixes the height; a width-only preference would be a separate addition.
- Not covered:
  - `StandardTabView` draws its selected header in the accent on `Surface`, not on the selection fill.
  - `AccentSoft` is also a state fill: a secondary button's hover, a checked or pressed toggle button
    (whose label is drawn in `Accent`), spin box arrows, and the toolbar overflow button. A palette that
    sets `AccentSoft` and `Accent` to the same system highlight makes a checked toggle's label invisible.
    That needs its own role or mapping and is left as a follow-up.
