# Broiler.UI open follow-ups — 2026-10-05

Every Broiler.UI item left open by the UI round of 2026-10-04 and 2026-10-05,
which produced the [0.1.0-preview.18 release
candidate](release-notes-0.1.0-preview.18.md) (`claude/roadmap-integration` at
`fd7657f`, unpublished).

Sources: the slice reports of the round (Broiler.UI, Broiler.Hosting and
Broiler.Mail), ADRs 0028–0034, and Broiler.Mail's acceptance record and roadmap
status of 2026-10-05. Each item was checked against the source at `fd7657f`;
file paths and line numbers are at that revision. Items already fixed are listed
at the end and are not open. "Found by" names the slice or consumer that
reported the item; "by calculation" means a contrast ratio worked out from
colors, not measured on screen.

The identifiers (D-, V-, B-, C-) are local to this file.

## Design decisions for the owner

These were deliberately not changed, or ship only with the owner's sign-off.
Each would change what a preset or every consumer draws, or how controls behave.

### D-1 Light and Dark scrollbar thumbs, and the two scrollbar looks

- **Now:** under Light and Dark, `StandardScrollView`, `StandardRichEdit` and
  `StandardFormatCodeView` draw a translucent pair (a #A1ADBD thumb on #EAEDF1
  over white in Light), while `StandardListView`, `StandardTreeView` and
  `StandardCodeEditor` draw the roles (#8DA0B6 on #F1F4F8 in Light). One window
  shows both.
- **Contrast:** thumb on track below 3:1 in both: translucent 1.94 (Light) and
  2.30 (Dark), roles 2.43 and 2.21 (ADR 0033). Mail's visual review 3 measured
  1.9–2.4:1 on screen.
- **Tied to it:** a focused list's ring where it crosses the thumb stays at 1.83
  (Light) and 2.92:1 (Dark). The text color would read at 5.83 and 5.55:1 there
  (ADR 0034, Not covered).
- **Decide:** which look the presets keep (or a third), whether to raise the
  thumb to 3:1, and then whether the list ring over the thumb uses the text
  color.
- **Where:** `StandardThemeTokens` presets
  (`src/Foundation/Broiler.UI.Standard/Theme/StandardThemeTokens.cs`),
  `StandardControlPaint.ScrollbarColors`, the
  `OwnScrollbarTrack`/`OwnScrollbarThumb` pairs in the three overlay controls.
- **Found by:** Broiler.UI scrollbar-roles review (ADR 0033); Mail visual
  review 3.

### D-2 Splitter grip contrast

- **Now:** `StandardSplitter`'s grip line measured 2.3–2.6:1 in Mail's
  screenshots.
- **Decide:** whether the grip is a control part that needs 3:1 (WCAG 1.4.11)
  and which token it takes.
- **Where:** `StandardSplitter.GripColor`
  (`src/Implementations/Standard/Layout/Broiler.UI.Splitter.Standard/StandardSplitter.cs`,
  lines 44–50).
- **Found by:** Mail visual review 3.

### D-3 Unfocused input border contrast

- **Now:** unfocused field borders measured 1.3–1.4:1, up to 1.6:1 at the
  darkest antialiased pixels.
- **Decide:** whether the presets' input border must reach 3:1 (the WCAG 1.4.11
  question), which changes every form in Light and Dark.
- **Found by:** Mail visual review 3.

### D-4 The 0.5 DIP tolerance on `Auto` scrollbars (ships in preview.18)

- **Now:** an overflow of no more than 0.5 DIP shows no `Auto` bar and makes no
  `FocusWhenScrollable` stop, but the content can still be scrolled by that
  fraction (wheel, `MakeVisible`, `ScrollToEnd`) with no bar to show it. Before,
  any overflow showed a bar. This is in every `StandardScrollView` (ADR 0034,
  "One tolerance"; commits `5663785`, `5d95727`).
- **Why:** with `Auto` bars a stop then exists exactly while a bar shows, and a
  form's last field keeps its ring room.
- **Decide:** sign off the tolerance, or choose another rule, before preview.18
  is published.

### D-5 How to fix the toolbar notch above a splitter

See V-1. The fix is one of two changes to what every consumer draws: a splitter
that leaves the outer half-DIP of its edges to its neighbors, or frames stroked
inside their bounds (moving every frame by 0.5 DIP in every preset).

### D-6 A focused-but-unselected item state

See B-1. Decide whether WinUI-style item focus without selection is wanted in
lists and tab views.

### D-7 Modifiers on the tab strip's keys

See B-2. Restricting the strip to unmodified keys is a behavior change.

### D-8 Button roles, and the system palette's scrollbar thumb

- **Now:** Broiler.UI has no button roles, so Hosting's system contrast palette
  draws buttons, borders and the scrollbar thumb in WindowText. WinUI draws the
  thumb (and buttons) in ButtonText, which differs in Desert, Dusk and
  Night sky.
- **Decide:** whether Broiler.UI adds button face/text roles; Hosting would then
  map buttons and the thumb to ButtonFace/ButtonText together. If hovered or
  pressed thumb roles are added (see B-8), Hosting would map them to Highlight,
  guarded at 3:1, and a disabled thumb to GrayText.
- **Found by:** Broiler.Hosting palette review (hc-palette-and-input).

## Visual defects

### V-1 Toolbar bottom line notched above a `StandardSplitter`

- **Symptom:** where a split container is docked directly under a toolbar, the
  toolbar's bottom line has a notch above the 8 DIP splitter column (Mail's
  inbox; reported from the Dusk and high-contrast captures).
- **Cause:** `StandardToolbar` strokes its 1 DIP frame centered on its bounds
  (`StandardToolbar.cs:520`), so the lower half lies below it.
  `StandardSplitter.RenderCore` fills its whole bounds after the toolbar
  (`StandardSplitter.cs:41`, `list.FillRect(Bounds, ...)`) and covers that half.
  The list and the reader pane stroke their own top frames over the line, so the
  notch shows only above the splitter.
- **Fix:** needs D-5. Option A: the splitter leaves the outer 0.5 DIP of the
  edges it shares with neighbors unfilled. Option B: frames are stroked inside
  their bounds (every framed control moves its stroke by 0.5 DIP). Consumers can
  avoid it meanwhile with 1 DIP between the toolbar and the split container;
  that is a workaround, not a fix.
- **Found by:** Mail visual review 3 (reported with the list frame); root cause
  in the list-form-polish review (ADR 0034, "The notch in Mail's toolbar line").

### V-2 Tab view focus ring flush against the window frame in contrast palettes

- **Symptom:** in the contrast palettes, the ring on the selected header sits
  against the window's client frame, so in `high` and some palettes the ring and
  the frame read as one thick line. Cosmetic.
- **Cause:** headers are laid out from the view's left edge, and the ring is
  drawn the theme's `FocusRingOffset` (2 DIP) inside the header's top and sides,
  at least 2 DIP thick in high-contrast palettes
  (`StandardTabView.DrawFocusRing` and `GetFocusRingBounds`,
  `StandardTabView.cs:293` and `:338`). The stroke's outer edge is then 1 DIP
  inside the header. Where the header's edges meet the window's frame, as the
  first header does in Mail, only that 1 DIP separates ring and frame.
- **Fix (suggested):** keep at least one stroke width plus 1 DIP between the
  ring and any edge where the header meets the view's own edge, at least in
  high-contrast palettes; or start the strip a few DIP in from the view's edge
  (this moves header bounds and hit testing). A consumer can also pad the tab
  view.
- **Found by:** Mail visual review 5 (`final3-adopt` contrast palettes). Only in
  builds with ADR 0031.

### V-3 Half-stroke sliver where a ring crosses a thumb's rounded end

- **Symptom:** where a side of the focus ring crosses the rounded end of an
  opaque scrollbar thumb, a sliver at most half the stroke wide stays in the
  ring's low-contrast color on the thumb: about 0.5 DIP long for the list's
  1 DIP ring, about 1 DIP for the scroll view's 2 DIP ring. High-contrast
  presets and Hosting contrast palettes.
- **Cause:** render lists clip to rectangles only.
  `StandardControlPaint.PillAreasUnderRing` (`StandardControlPaint.cs:249`)
  approximates the pill with rectangles; used by `StandardListView.cs:444` and
  `StandardScrollView.cs:585`.
- **Fix:** a rounded (or path) clip in the render list API (Broiler.Graphics
  `BRenderList`), or drawing the redrawn stretch as a path that follows the pill
  instead of clipping.
- **Found by:** Broiler.UI list-form-polish review (ADR 0034, Not covered).

### V-4 Low-contrast ring over the thumb in Light and Dark

1.83 and 2.92:1 where a focused list's ring crosses its thumb. Decided together
with D-1.

### V-5 Scrollbar track ends under Mail's divider

- **Symptom:** in Mail's reader, the message body's thumb at the top of its
  track starts directly under Mail's 1 DIP header divider, so the two touch.
  Seen in the contrast palettes, where the thumb is opaque.
- **Cause:** a scrollbar's track runs the full length of its side.
  `StandardScrollView.ScrollbarGap` (ADR 0034) keeps content away from a bar,
  not the ends of a track from a neighbor. The reading pane is a
  `StandardRichEdit` (ADR 0033), whose vertical track runs the height of its
  inner bounds (`StandardRichEdit.cs:455`, `RichEditScroller.PaintScrollbar`).
- **Fix (suggested):** an end inset for scrollbar tracks (for example a
  `ScrollbarTrackInset`) on `StandardScrollView` and `StandardRichEdit`; or Mail
  leaves 1–2 DIP between its divider and the body.
- **Found by:** Mail visual review 4 (`final2`, contrast palettes); noted again
  in review 5. ADR 0034, Not covered.

### V-6 `StandardCheckBox` draws a white check on the accent

- **Symptom:** the check and the indeterminate dash are always white on the
  `Accent` fill. In Hosting's system palette for Aquatic, Dusk and Night sky,
  whose highlights are light, that is about 1.5–1.9:1 (by calculation). By the
  same calculation the `HighContrastDark` preset (accent #00E0FF, `OnAccent`
  black) gives about 1.6:1.
- **Where:**
  `src/Implementations/Standard/ValueAndSelection/Broiler.UI.CheckBox.Standard/StandardCheckBox.cs:86`
  and `:88` (`BColor.White`).
- **Fix (suggested):** add a check color property that follows `OnAccent` (set
  in `ApplyTheme`, taken from the shared palette when unthemed) and draw the
  glyphs in it.
- **Found by:** Broiler.Hosting palette review (hc-palette-and-input), in
  passing. Mail has no check boxes.

### V-7 Format code view token colors on the selection in high contrast

- **Symptom:** `StandardFormatCodeView` keeps each token's own color on the
  selection fill while `SelectionText` equals `Text`. In Hosting's palette for a
  custom theme whose HighlightText is its WindowText, paragraph, structure,
  escape, pending and diagnostic codes are drawn on Highlight with no contrast
  guarantee (HotLight #0000EE on #767676 is about 2.1:1 by calculation). Inline
  codes are covered by Hosting's `AccentText` fallback.
- **Where:** `StandardFormatCodeView.ApplyTheme`,
  `StandardFormatCodeView.cs:179`
  (`SelectionForeground = theme.SelectionText == theme.Text ? null : theme.SelectionText`).
- **Fix (suggested):** set `SelectionForeground` to `SelectionText` whenever the
  palette `IsHighContrast`.
- **Found by:** Broiler.Hosting palette review (hc-palette-and-input). Listed as
  a known gap in Hosting's README.

### V-8 File and font dialog combo boxes at large text

- **Symptom:** at 200 % text (a 40 DIP line) the dialogs' combo boxes draw their
  text and arrow up to 14 DIP below the frame. Their edits and buttons have
  fixed heights too.
- **Where:** `StandardFileDialog.cs:146–157` (`_fileTypeCombo` 430x30,
  `_sortCombo` 132x26, item height 26) and the fixed layout heights at lines
  436–446; `StandardFontDialog.cs:84` (`_weightCombo` 140x28), the spin box at
  line 72 and `rowHeight = 28` at line 251. They take a text-scaled theme from
  their dialog.
- **Fix (suggested):** size the boxes from the font, as ADR 0029 does for a
  combo box without a set size, and derive the dialog's row heights from the
  font. `PreferredSize` sets width and height together, so a width-only
  preference (see B-7) may be needed.
- **Found by:** Broiler.UI reviews of ADR 0029 and ADR 0033. No consumer
  screenshot shows these dialogs.

### V-9 Code editor bracket match drawn over the bracket

- **Symptom:** `StandardCodeEditor` fills the bracket-match cell after the text,
  so an opaque fill (every preset) covers the matching bracket.
- **Where:** `StandardCodeEditor.cs:362`, after `RenderClassifiedText`.
- **Fix:** draw the fill before the text. This changes how every theme renders
  it, and the bracket would then be drawn in its classification color, not in
  `StateText`.
- **Found by:** Broiler.UI state-role slice (ADR 0029, Not covered).

### V-10 Italic glyph ink split at selection edges

- **Symptom:** editors draw each line once per strip (before, on and after the
  selection), each clipped to its strip, so ink that overhangs a selection edge
  (an italic glyph) is cut between the two colors. Only when a theme sets a
  distinct selection text color.
- **Where:** `StandardEdit` and `StandardRichEdit` selection drawing (ADR 0029,
  "Editors").
- **Fix:** none planned; drawing the overhang would need glyph-level clipping or
  run splitting, which breaks shaping. Documented.
- **Found by:** Broiler.UI ADR 0029 review.

### V-11 1 DIP focus rings anti-aliased across two pixel rows

- **Symptom:** 1 DIP rings stroked on whole-DIP edges are spread over two pixel
  rows at 100 % and 150 %, so the contrast that reaches the screen is below the
  ring color's.
- **Fix:** align strokes to device pixels in the shared drawing helpers or the
  renderer, for every control at once.
- **Found by:** Broiler.UI tab view review (ADR 0031, Not covered, "F11").

### V-12 Tab page clip and the frame's rounded corners

The page clip is rectangular, so opaque content can still cover about 1 DIP of
the page frame's 6 DIP corner arc along the diagonal (ADR 0031, Not covered).

### V-13 Tab headers past a narrow tab view

- **Symptom:** the fills and labels of headers laid out past a narrow
  `StandardTabView`'s right edge are drawn there; `VisibleTabCapacity` does not
  affect layout. The ring and the selected-tab bar stay inside since ADR 0031.
- **Fix:** clip or scroll the strip (a change of its own). Meanwhile
  Broiler.Code should clip its document strip (`CodeShellFactory`) to its pane.
- **Found by:** Broiler.UI tab view review (ADR 0031).

### V-14 Unthemed toggle buttons

- **Symptom:** a `StandardToggleButton` that is never themed keeps `Accent` and
  fixed light fills (#F2F7FF hover, #D8E8FC pressed, #F0F5FF indeterminate):
  3.4:1 for its label on a shared Dark surface, 4.28:1 for a checked label under
  shared Light. Under a custom shared palette with no state label of its own,
  the fixed pressed and indeterminate fills do not follow the palette.
- **Where:** `StandardToggleButton.cs:52–58`.
- **Fix (suggested):** take the label from the shared `AccentText` and the fills
  from the shared palette when built, as other unthemed controls do; until then,
  applications theme their toggle buttons.
- **Found by:** Broiler.UI reviews of ADR 0029 and ADR 0031.

### V-15 Smaller ring and border cases from ADR 0032

- Buttons and toggle buttons draw a 1 DIP ring whatever the theme's
  `FocusRingThickness` (2 DIP in the high-contrast presets), unlike the tab view
  and the scroll view.
- An unthemed button's default label is white, not the shared `OnAccent`, so
  under a shared palette with a light accent its ring is as faint as its label
  (white on Aquatic's highlight, 1.46:1).
- A spin box's 1 DIP border at rest merges with a hovered arrow's fill in the
  system palettes (1.4–1.9:1). It is a border, not focus.
- In Hosting contrast palettes, a pressed secondary button, a pressed spin box
  arrow and a hovered unchecked toggle look the same as at rest.

Found by: Broiler.UI focus-ring review (ADR 0032, Not covered) and the Hosting
palette review.

### V-16 Accent marks and text on the window color in custom contrast themes

- **Symptom:** in Hosting's system contrast palette `Accent` is the Highlight
  color, and Broiler.UI draws marks and text in `Accent` directly on the window
  color with no readable fallback: a checked check box's fill and border, a
  checked radio button's dot, the progress bar's and slider's fill on their
  track, a window's and a dialog's active border, and the code editor's keywords
  (and an unthemed toggle button's label, V-14). That reads in the four
  Windows 11 contrast themes (Hosting's tests check the accent on the window
  color and on a track), but not in a custom contrast theme whose Highlight is
  close to its Window color (about 1.5:1 in Hosting's custom test palette).
  Accent text has such a fallback (`AccentText`, ADR 0031, which Hosting sets),
  and so has the list's unread dot (ADR 0034).
- **Where:** `StandardCheckBox.cs:81` and `:83`; `StandardRadioButton.cs:196`;
  `StandardProgressBar` and `StandardSlider` (`FillColor = theme.Accent`);
  `StandardWindow` and `StandardDialog` (`ActiveBorderColor = theme.Accent`);
  `StandardCodeEditorPalette.cs:49` (`Keyword = Adjust(tokens.Accent, dark)`).
- **Fix (suggested):** an accent-mark role for marks on the window color,
  guarded at 3:1 against it as `AccentText` is for text, which Hosting would
  set; or a high-contrast fallback in these controls (for example the text color
  where `Accent` is below 3:1 on `Surface` and the palette `IsHighContrast`).
- **Found by:** Broiler.Hosting palette review (hc-palette-and-input). Listed as
  a known gap in Hosting's README and the `CreateHighContrastTheme` remarks,
  which also name the list's unread dot; ADR 0034 has since given the dot a 3:1
  choice against its fill.

### V-17 No hover or press feedback on a primary button in Hosting's system palette

- **Symptom:** in Hosting's system contrast palette a primary (default) button
  looks the same at rest, hovered and pressed. `Accent`, `AccentHover` and
  `AccentPressed` are all Highlight, and the label is `OnAccent`, which is
  HighlightText. The state pair (`StateFill`, `StateText`) cannot fix it, since
  the label already is HighlightText on Highlight. V-15's last bullet lists the
  other states that look as at rest in these palettes.
- **Where:** `StandardButton.ApplyTheme` (`StandardButton.cs:30–34`); Hosting's
  `WindowsTheme.CreateHighContrastTheme`
  (`src/Broiler.Hosting.Windows/Theme/WindowsTheme.cs`, lines 127–131 at
  `c388a66`).
- **Fix (suggested):** a hover and pressed role for primary buttons that Hosting
  can map apart from Highlight, or feedback that does not rely on color (for
  example a thicker border while hovered or pressed).
- **Found by:** Broiler.Hosting palette review (hc-palette-and-input), noticed
  while verifying. Not documented as a gap in Hosting.

## Behavior and API gaps

### B-1 No focused-but-unselected list or tab item

- **Now:** `UiListView` and `UiTabView` have no current item apart from the
  selection; only `UiTreeView` has `FocusedNode`, and it moves with the
  selection. Hosting therefore reports only the selected row or tab as keyboard
  focusable, and an accessibility checker (Axe.Windows) may flag unselected
  items as "should be focusable".
- **Fix (suggested, after D-6):** a focused item index separate from the
  selection (Ctrl+arrow moves it, Space selects), reported in the item's
  semantic node so Hosting can expose it through GetFocus, HasKeyboardFocus and
  IsKeyboardFocusable.
- **Found by:** Broiler.Hosting UIA review (uia-semantics-mapping).

### B-2 Tab strip keys ignore modifiers

- **Symptom:** while a `StandardTabView` has focus, Alt+Left, Ctrl+Right,
  Shift+End and so on switch tabs. In Mail, Alt+Left falls through to the
  session when Back does not apply, so with focus on the strip it switches to
  the previous tab.
- **Where:** `StandardTabView.HandleKeyboard` (`StandardTabView.cs:383`) checks
  the key only.
- **Fix (suggested, after D-7):** return unhandled when
  `UiInputEvent.KeyModifiers` holds Alt, Ctrl or Shift.
- **Found by:** Broiler.UI layout-input-fixes review. The bubbling of arrow keys
  from tab content is fixed (see the end of this file).

### B-3 List anchor fallback prefers a row above the viewport

- **Symptom:** when the top visible row is removed, `UiListView.SetItems` keeps
  the nearest surviving row of the old order in place, searching outward and
  preferring the row after the anchor at equal distance. When the anchor and the
  row after it are both removed, the row before the anchor (off screen) wins
  over the first visible survivor two rows below.
- **Where:** `UiListView.TryFindSurvivingRow`
  (`src/Abstractions/ValueAndSelection/Broiler.UI.ListView/UiListView.cs:534`).
- **Fix (suggested):** prefer survivors that were on screen before falling back
  to rows above the viewport.
- **Found by:** Broiler.UI ADR 0029 review.

### B-4 A control flag for inline IME composition

- **Now:** Broiler.Mail turns the IME off while the focus draws no composition,
  and its Windows text input names `StandardRichEdit` concretely to decide that;
  Hosting's `DrawsCompositionInline` is window-wide.
- **Fix (suggested):** a toolkit flag (an interface or property) saying whether
  a control draws the IME composition inline, so hosts can apply the rule per
  focused control.
- **Found by:** Broiler.Mail adoption review.

### B-5 Scroll-stop hand-off details

- A stop that focus is handed to and that is taller than its scroll viewport
  shows its far edge, because `MakeVisible` aligns the edge that lies outside
  (as with Tab). Mail's own reveal keeps the top in view instead.
- A change that invalidates nothing, such as setting a child's `Focusable`,
  hands focus on only with the host's next frame.

Found by: Broiler.UI focus hand-off review (ADR 0032, Not covered).

### B-6 `MoveFocus` from an element that is no stop

`StandardFocusScope.MoveFocus` from an element that is not a tab stop restarts
at the first (or last) stop rather than continuing from the element's place.
`FindAdjacentStop` gives hosts that place (ADR 0032, Not covered).

### B-7 Combo box sizing gaps

- `PreferredSize` sets width and height together, so an application that sets
  only a width also fixes the height. A width-only preference would be an
  addition (ADR 0029).
- A combo box narrower than its arrow slot draws the arrow over its left edge.
  The arrow stays a glyph; a family whose "v" is much wider for its line than
  the default font's comes closer to the frame. A drawn chevron sized from the
  line would remove that, but changes the default rendering (ADR 0033).

### B-8 No hover, pressed or disabled scrollbar look

No control draws a hovered, dragged or disabled thumb differently. A later state
role would default to `ScrollbarThumb` (ADR 0033). See D-8 for Hosting's
mapping.

### B-9 Semantics not covered by ADR 0028

- Per-line text-range geometry.
- Clipping for other cropping containers, such as the toolbar's overflow strip.

### B-10 Selection roles reach only themed controls

A list, combo box, menu, tree or editor built after the shared palette changes,
and never themed, draws selected text in its ordinary color; state fills do
follow the shared palette. ADR 0029 asks applications to use
`StandardThemeController.ApplyToSubtree` for content built later. Taking the
shared selection roles when built, as buttons, toggle buttons and spin boxes
take their state labels (ADR 0029), would remove the trap.

### B-11 The state-label guard compares colors exactly

The toggle button and spin box switch their label to `StateText` only when
`Accent` or `TextMuted` equals the state fill exactly. A theme whose accent is
nearly the state fill must set `CheckedForeground`, `ArrowHoverColor` or
`StateText` itself. A contrast test instead of equality would cover it.

### B-12 Possible double speech, for the H-01 screen-reader pass

- A `FormField` group still names the error and reports `Invalid` while its
  control's description also carries it.
- Tree row names still end in ", expanded" or ", collapsed" alongside the state
  flags.

If Narrator or NVDA says these twice once Hosting maps the control state and the
row flags, drop them from the group and the row names (ADR 0028, Consequences).
Whether the error also belongs in HelpText is open with Hosting.

### B-13 Should a tree's rows in view raise `StructureChanged`?

- **Now:** when its rows in view change (a scroll, or a resize that changes how
  many rows fit), `UiTreeView` raises only a Semantic invalidation (ADR 0030).
  Only `UiTabView` and `UiListView` call `NotifyStructureChanged` (ADR 0028). No
  ADR records whether the tree should too.
- **Decide:** whether a tree's change of rows in view should also raise
  `StructureChanged`, or whether Hosting's own tree-rows comparison is the
  agreed signal. Record the decision in ADR 0030.
- **Found by:** Broiler.UI layout-input-fixes review, left open for the merge
  with the integration branch.

### B-14 Measurement hooks for Mail's UI-12 harness

- Text-layout call counts need layout and `StandardRichEdit` counters in
  Broiler.UI. Broiler.Graphics could provide the counting instead, with a public
  `BTextMeasurer.Provider` getter that Mail can wrap.
- A layout phase hook would stop Mail's `--detail` runs, which time measure and
  arrange in a pre-pass, from losing the invalidations raised in that pre-pass
  to the frame's `RenderFrame` (nothing reads them today).

Found by: Broiler.Mail's UI-12 measurement slice.

## Code health

### C-1 The Win32 demo fails at startup

- **Symptom:** `Broiler.UI.Win32.Demo` throws `InvalidOperationException`
  "Controls can only be created after the native window exists" at startup, on
  `main` (`cdd54f3`) and on this branch, so no native check can run against the
  demo without a patch.
- **Cause:** the `Win32DemoWindow` constructor registers an animation
  (`Win32DemoWindow.cs:166`, `_animations.Register(...)`), which reaches
  `DemoUiHost.StartAnimation` → `_window.StartAnimationTimer(16)`
  (`Win32DemoWindow.cs:1886`) before the native window exists; the window handle
  exists only from `OnCreated` (line 170).
- **Fix (suggested):** remember a start request made before creation and start
  the timer in `OnCreated`, or register the animation there. The second was
  tried: the ADR 0032 slice's temporary, uncommitted probe patch moved the
  registration into `OnCreated`, and the demo then ran for its ring, spin box
  and hand-off captures.
- **Found by:** Broiler.UI layout-input-fixes slice; confirmed again by the
  later slices. `docs/roadmap.md` says the demo builds again, which is true; it
  does not run.

### C-2 The extremes-of-lightness test exists four times

`StandardListView.cs:64`, `StandardTreeView.cs:139`,
`StandardCodeEditorPalette.cs:73–80` and `StandardControlPaint.cs:213–214` each
decide "high contrast" as `IsHighContrast`, or `Surface` and `Text` more than
0.9 apart, each with its own copy of the same weighted channel average
(`Luminance` or `Lightness`). Move it to one shared helper. Found by the
scrollbar-roles review (ADR 0033, Not covered).

### C-3 `StandardRichEdit` scrollbar setters do not invalidate Measure

`ScrollbarTrack`, `ScrollbarThumb` and `ScrollbarThickness` are auto-properties
(`StandardRichEdit.cs:156`, `:163`, `:165`). Since ADR 0033 an opaque bar color
changes the text column, so a box sized to its text
(`VerticalScrollPolicy.Never`, horizontal policy not `Never`) keeps a stale size
when the application sets an opaque color without re-theming. `ApplyTheme` does
invalidate. Give the setters an invalidation. Found by the scrollbar-roles
review.

### C-4 Layout hazards noticed in passing (not verified as defects)

- `UiElement.Arrange` marks the element arrange-valid after `ArrangeCore`, so an
  arrange invalidation raised inside a descendant's arrange (a scroll offset
  changed mid-layout) is lost for the ancestors in progress, and that content is
  not arranged again next frame. ADR 0030 records this as the rule ("overwritten
  when that ancestor finishes"); the scroll-stop hand-off avoids it by acting at
  draw time. Other code that scrolls during layout can hit it.
- `UiElement.BringIntoView` passes the element's own pre-scroll `Bounds` to
  outer scroll ancestors rather than the inner scroll view's resulting bounds,
  so nested reveals may be approximate.

Found by: Broiler.UI focus hand-off review.

### C-5 Source files stored with CR CR LF line endings

Eleven source files are stored with CRLF endings (every other source file is
stored with LF except `StandardListView.cs`, which is stored with CRLF but is
still treated as text; on `main` it also had one bare-LF line, and the round
stored it all CRLF), and each has one line that ends in CR CR LF followed by one
that ends in a bare LF. The stray CR makes git's line-ending handling treat them
as not text (`git ls-files --eol` shows `i/-text`), so `core.autocrlf` leaves
them alone: `StandardButton.cs`, `StandardMenu.cs`, `StandardToggleButton.cs`,
`StandardToolbar.cs`, `StandardTabView.cs`, `StandardTooltip.cs`,
`StandardEdit.cs`, `StandardRichEdit.cs`, `StandardCheckBox.cs`,
`StandardComboBox.cs` and `StandardRadioButton.cs`. This predates the round (all
eleven are like this on `main`). Editors and tools that normalize line endings
change those lines, and in one slice rewrote a whole file; the slices restored
the original bytes by hand to keep their diffs to the real change. Normalize
them in a commit of their own, with `StandardListView.cs` if every source file
is to be stored with LF. Found by the state-role, focus-ring and scrollbar-roles
slices; the list was taken with `git ls-files --eol` at `fd7657f`.

### C-6 27 test warnings

A clean Release build reports 27 warnings (xUnit2031, xUnit2029, CS8625) in 13
existing test files, among them `StandardRichEdit*Tests`,
`StandardFormatCodeViewRenderTests`, `StandardEditMouseSelectionTests` and
`StandardSplitterTests`. None comes from this round's changes.

### C-7 `DefaultListItemPresenter` keeps its own ring rule

It picks its ring color against the selection in high contrast only and without
the opacity check of `StandardControlPaint.FocusRingColor` (ADR 0032, Not
covered). Move it to the shared helper.

### C-8 ADR text to correct before acceptance

ADR 0032's Consequences still say that Mail's `BroilerUiVersion`
0.1.0-preview.18 "does not contain this change" (`docs/adr/0032-...md`, lines
182–185). preview.18 will be cut from `fd7657f`, which contains it, and Mail's
adoption branch has already removed `DefaultButtonFocus`. ADR 0030's last
Consequence still calls Hosting's tree row support unreleased and unverified
against this change; Hosting's release candidate now passes its tests against
`local.7`. Found by the Mail adoption review.

### C-9 Frame-build time in the adoption build (to profile)

Mail's performance record of 2026-10-05 found its adoption build (UI `local.6`,
Hosting `local.4`) 0.31–0.37 ms slower at p50 than the published build in
scroll, theme, select and splitter, allocating 5–7 KB more per frame. The cause
was not profiled and may lie in Broiler.UI, Hosting or Mail. Profile one
workload (theme, +0.37 ms) against preview.17 to place it.

### C-10 Review annotations for the round's declarations

`main` has declaration annotations for human review in progress (`0b00e45`, per
`assurance.config.json`): `Broiler-AI`, `Broiler-Falsified-If` and
`Broiler-Human` comment lines, so far in 112 files under `src/Abstractions`. The
round's 101 commits add none.

- In the seven annotated files the round changed (`UiMenu`, `UiScrollView`,
  `UiTabView`, `CodeEditorPalette`, `UiComboBox`, `UiListView`, `UiTreeView`),
  new declarations have no lines, for example `UiTabView.GetTabHeaderBounds`,
  `UiListView.ClipItemSemanticNode`, `UiComboBox.IsPreferredSizeSet` and
  `CodeEditorPalette.SelectionForeground`.
- Annotated declarations whose code changed, such as `UiListView.SetItems`, keep
  the assessment written before the change.
- The rest of the new API of ADRs 0028–0034 (in `UiElement` and the Standard
  implementations) lies in files that are not annotated yet.

Assess them before the assurance files are generated, or leave that to the
owner's tool. `HUMAN_REVIEW.md` stays `PENDING` either way. Found by the
documentation review of this file, not by the round's slices.

## Recorded limits (no change planned)

- A wheel tilted while Shift is held looks the same as Hosting's Shift+wheel
  shape and scrolls the other way (ADR 0030).
- `StandardListView` reads only vertical notches; it cannot scroll sideways.
- A neighbor drawn after a list covers the outer half of the list's frame on
  that side (ADR 0034).
- A field the user scrolls part way out of a form loses the outer edge of its
  ring there with the rest of what is scrolled out (ADR 0034).
- Code that measures an arranged `StandardScrollView` at a new size and reads
  `ViewportSize` or `ExtentSize` before arranging it gets the last arrange's
  sizes (ADR 0034).
- The sample applications' sidebars draw the selected item's label in the accent
  on the selection fill (samples only, ADR 0031).

## Fixed this round (not open)

Listed so they are not reopened from older records:

- `StandardTabView` switched tabs on arrow keys that bubbled up from its
  content: fixed by ADR 0030 (`3440a63`). Only the modifier case (B-2) remains.
- `AccentSoft` used as a state fill: `StateFill`/`StateText` (ADR 0029). At
  `fd7657f` every remaining use of `AccentSoft` in `src` is a selection; the
  residual state cases are V-9, V-14, V-15 and B-11.
- The presets' checked toggle label on `AccentSoft` (2.76:1 Dark, 4.28:1 Light):
  fixed for themed toggles by `AccentText` (ADR 0031); unthemed toggles are
  V-14.
- The selected tab header drawn in the accent on `Surface`: ADR 0031.
- Tree scrolling not reported to hosts: ADR 0030.
- A focused secondary button hovered by the pointer losing its ring in system
  palettes: ADR 0032, with Hosting preview.7's state pair.
- A `FocusWhenScrollable` area keeping focus after it stops scrolling: ADR 0032.
- The list's focus ring lost on an opaque thumb, and the selected row meeting
  the thumb: ADR 0034.
- The inbox notice as a Tab stop with nothing to scroll, and a form's thumb
  touching a focused field's ring: ADR 0034 (`scroll-stop-tolerance`).
- High-contrast scrollbar colors in the scroll view, rich edit and format code
  view: ADR 0033.
- A tilt wheel scrolling the wrong way in `StandardScrollView`,
  `StandardRichEdit` and `StandardFormatCodeView`: ADR 0030 (`c2bd1f6`;
  Shift+wheel in `a67b642`).
- Toolbar arrow keys landing on disabled items and on children that never take
  focus: ADR 0030 (`8832db3`, `4ea033b`).
- Hidden tabs arranged at an empty rectangle, which Mail's `TabContent` worked
  around: ADR 0030 (`0e45ca8`).
- Arrange invalidation stopping at the first arrange-invalid ancestor, so a
  scroll offset change below it did not move the content (seen headless in
  Mail's compact inbox): ADR 0030 (`ba35e3f`).
- `FormSurface` not keeping the focused field in view when its viewport shrinks:
  ADR 0030 (`21777d5`).
- The list jumping when its top row is removed: ADR 0029 (`fcb5e5b`). The order
  of the fallback is B-3.
- Combo boxes not sized from their font: ADR 0029 (`8246e69`), with the arrow
  slot in ADR 0033 (`251d925`). The dialogs' fixed combo boxes are V-8.
- The two-line row cutting the date: ADR 0029 (`64f1db1`, `fc2f619`).
- No focus ring on a focused read-only scroll view: ADR 0028 (`4720d80`,
  `7f7f37e`).
- A stack overflow on a `LabeledBy` cycle (and on `DescribedBy` and
  `ErrorMessage` cycles): ADR 0028 (`a0ab6b6`).
