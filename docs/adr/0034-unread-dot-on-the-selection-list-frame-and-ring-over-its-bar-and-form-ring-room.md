# ADR 0034 - The unread dot on the selection, the list's frame and ring over its bar, and room for rings and feedback in forms

**Status:** Proposed  
**Date:** 2026-10-05

## Context

A pixel-level review of Broiler.Mail's native screenshots, taken on local packs of the roadmap integration
build (ADRs 0028 to 0033, at 161ec5c), confirmed five defects that start in Broiler.UI. None is new in that
build; the earlier whole-view focus ring hid some of them.

- **An unread dot lost on the selection.** `StandardTwoLineListItemPresenter` drew the unread dot in `Accent`,
  or in the selected text color where the selection has one of its own (ADR 0029). In `Dark` the selection
  text is the ordinary text, so a selected unread row drew #2673CE on `AccentSoft` #17324E: 2.76:1, below the
  3:1 a mark that is not text needs. ADR 0031 kept the dot in `Accent` and checked it on `Surface` (3.42) and
  `SurfaceAlt` (3.01), not on the selection fill it also sits on. Mail leaves a selected message unread, so
  the state is common. `DefaultListItemPresenter` draws no dot.
- **A form's field ring cut at its sides.** `StandardEdit` strokes its 2 DIP focus ring centered on its bounds,
  as `StandardSpinBox`, `StandardRichEdit` and `StandardFormatCodeView` stroke their focused frames, so 1 DIP of
  it lies outside the control. `FormViewport` laid its content flush with its scroll view's `ContentBounds`,
  which is also the clip. A field as wide as the form therefore kept only the inner half of its ring's left and
  right sides, with flat-cut outer corners, and half of its 1 DIP border at rest. In Mail this shows on every
  full-width field (To, Email address) in every palette and at every size.
- **The list's frame under its bar.** `StandardListView` stroked its rounded frame first and then drew its
  scrollbar, a 12 DIP track and thumb running the whole height of the right edge. The bar covered the frame's
  right side. Only the outer halves of the frame's rounded corners were left, which showed as a stray arc at the
  bottom right of Mail's message list. Mail's splitter fills its own bounds after the list and covers the
  frame's outer half there, so no right edge was left at all.
- **Feedback flush with the action strip.** `FormSurface` placed its feedback directly at the bottom edge of
  the action strip. The strip strokes its frame centered on that edge, so the first banner's fill covered the
  lower half of the strip's bottom line, and the banner's accent bar ran into the strip's rounded corner. The
  banners Mail stacks below it, in a `StandardPanel { Spacing = 4 }`, sit 4 DIP apart.
- **The list's focus ring across an opaque thumb.** A focused list strokes its ring 2 DIP inside its bounds,
  after the bar, so the ring's right side runs through the thumb. In the palette Broiler.Hosting builds from a
  Windows contrast theme the ring is the highlight and the thumb the window text: 1.46:1 (Aquatic), 1.44
  (Desert), 1.91 (Dusk) and 1.78 (Night sky). In `HighContrastLight` both are black (1.00), and in
  `HighContrastDark` the ring is yellow on a white thumb (1.07). ADR 0033 made the scroll view draw that
  stretch of its ring again; the list did not. Hosting's README records it as a known gap, and adds that "a
  selected row's highlight fill meets the thumb".

A review of the first fixes found three more problems of the same kind, which this ADR also covers: the
stretch of ring drawn again over the thumb was clipped to the thumb's rectangle, not its pill, and erased the
ring beside the thumb's rounded ends (the scroll view had done so since ADR 0033); a field the keyboard
brought into view was placed flush with the top or the bottom of the form's viewport, which cut the outer
half of its ring there; and the list's accent text did not follow an accent the application set after the
theme.

## Decision

### The unread dot stands out from the fill it is drawn on

- **`UiListItemRenderContext.AccentText`** is a shade of `Accent` chosen to read on `Background` and on
  `SelectedBackground`. Like the selected text colors (ADR 0029) it is not `required`: unless the caller sets it,
  it is `Accent`. `WithItem` copies it.
- **`StandardListView.AccentText`** is passed to every row. `ApplyTheme` sets it to the theme's `AccentText`
  (ADR 0031); an unthemed list takes the shared palette's. Either belongs to the accent it was chosen for, as
  the theme token's does: once the application sets `Accent` to another color, `AccentText` is that accent,
  so a brand accent is not paired with Dark's #7AB7FF, and a brand accent lost on the selection fill gives
  the dot the row's text color. A value the application sets is its own until the next theme.
- The two-line presenter picks the dot's color against the fill under it, the selection fill on a selected row
  and the list's background otherwise:
  1. on a selected row whose selection has a text color of its own, that color, as before;
  2. `Accent`, where it reaches 3:1 on the fill, or where the fill is not opaque (what shows through it is not
     known; ADR 0032 keeps the ring for the same reason);
  3. otherwise `AccentText`, where it reaches 3:1;
  4. otherwise the row's text color, which the theme chose to be read on that fill.

| Palette | Unselected row | Selected row, before | Selected row, now |
|---|---|---|---|
| Light | `Accent` #0B6FD8, 4.91 | `Accent`, 4.28 | unchanged |
| Dark | `Accent` #2673CE, 3.42 | `Accent`, 2.76 | `AccentText` #7AB7FF, 6.27 |
| HighContrastLight | `Accent` #0000CC, 11.22 | `Accent`, 9.46 | unchanged |
| HighContrastDark | `Accent` #00E0FF, 13.11 | `Accent`, 8.54 | unchanged |
| Hosting, Aquatic | Highlight, 11.17 | HighlightText, 7.89 | unchanged |
| Hosting, Desert | Highlight, 7.27 | HighlightText, 7.00 | unchanged |
| Hosting, Dusk | Highlight, 6.80 | HighlightText, 7.33 | unchanged |
| Hosting, Night sky | Highlight, 11.81 | HighlightText, 7.96 | unchanged |

- Only Dark's selected row changes in the presets and in the palettes Hosting builds from the Windows 11
  contrast themes. Steps 3 and 4 also cover a palette whose highlight is lost on its window and whose highlight
  text is its window text, such as a dark highlight under white text on black. There the dot was the highlight on
  both rows (1.57:1 on the window, 1:1 on the selection), and is now the accent text shade, which Hosting sets to
  the window text in such a palette.

### Room for a field's focus ring in a form

- **`StandardScrollView.HorizontalContentInset`** (DIP, default 0) is room left between the content and the left
  and right edges of the area the content is drawn and clipped in. With `ConstrainWidth` the content is measured
  and arranged that much narrower on each side; otherwise it keeps its width, and the extent grows by the room on
  each side, so content scrolled to either end keeps it. `MakeVisible` keeps it too. The scrollbars, the clip and
  `ContentBounds` do not move. At 0 the view lays out exactly as before.
- **`FormViewport`** sets it to 1 DIP: the outer half of the 2 DIP frame a focused edit, spin box, rich edit or
  formatting code view strokes on its edge, the widest stroke a standard field draws outside itself. A toolbar's or
  combo box's 1 DIP frame needs half of that.
- **`FormSurface`** arranges both of its viewports, the form and the feedback, 1 DIP wider on each side, into
  its 12 DIP margins (never more than the margin). The fields and banners inside keep exactly the bounds they had,
  aligned with the action strip; only the clip and the vertical bar move 1 DIP outward. At 640 DIP wide:

  | | Before | Now |
  |---|---|---|
  | Form viewport | x 12 to 628 | x 11 to 629 |
  | Its clip, no bar | 12 to 628 | 11 to 629 |
  | A full-width field, no bar | 12 to 628 | 12 to 628 |
  | The vertical bar | 616 to 628 | 617 to 629 |
  | A full-width field beside the bar | 12 to 616 | 12 to 616 |
  | The field's ring, outer edges | 11 to 629, cut to 12 to 628 | 11 to 629, whole; beside the bar 11 to 617, whole |

- **`StandardScrollView.VerticalContentInset`** (DIP, default 0) is the same room above and below the content.
  The extent grows by it at each end, so the first and the last control keep it when scrolled to either end;
  with `ConstrainHeight` the content is measured and arranged that much shorter. `MakeVisible` keeps it, so a
  control the keyboard brings into view from below ends 1 DIP above the clip's bottom, and one brought in
  from above starts 1 DIP below its top. At 0 the view lays out exactly as before.
- **`FormViewport`** sets it to 1 DIP as well, and keeps it when it shows the focused control again after the
  viewport shrinks. **`FormSurface.Reveal`** keeps it too.
- **`FormSurface`** arranges both viewports 1 DIP taller at each end, into the space around them: the 12 DIP
  top margin, the 8 DIP above the action strip, the 4 DIP above the feedback and the 12 DIP bottom margin
  (never more than 4 DIP). The fields keep their places and the scroll range is unchanged, so a field brought
  into view ends where it did, and only the clip and the vertical bar grow by 1 DIP at each end. At 640 x 480
  with twelve fields and no feedback, before this ADR and now:

  | | Before | Now |
  |---|---|---|
  | Form viewport, and its clip | y 12 to 418 | y 11 to 419 |
  | The first field's edit, unscrolled | y 36 to 68 | y 36 to 68 |
  | An edit brought into view from below (Tab) | y 386 to 418, ring to 419, cut at 418 | the same, ring whole |
  | The last edit, scrolled to the end | y 386 to 418, ring cut | the same, ring whole |
  | An edit brought into view from above (Shift+Tab) | y 12 to 44, ring from 11, cut at 12 | the same, ring whole |
  | Shown feedback's viewport | from the strip's bottom to 12 DIP above the bottom | from 3 DIP below the strip to 11 DIP above the bottom; banners 4 DIP below the strip |

- A `FormViewport` used on its own lays its content out 1 DIP short of each edge, 2 DIP narrower in all, and
  1 DIP lower, with 2 DIP more to scroll.
- An application that wants the old geometry sets `Content.Scroll.HorizontalContentInset` and
  `VerticalContentInset`, and those of the feedback viewport (the surface's last `FormViewport` child), back
  to 0; `FormSurface` then places that viewport in its margins as before.

### The list's frame over its bar

- `StandardListView` strokes its frame after the rows and the bar, just before its focus ring. The track and
  thumb keep their bounds, so dragging and paging are unchanged. The frame's inner half now runs along the bar's
  right edge and joins its rounded corners, so the frame is continuous on every side. Anything a row presenter
  draws at the list's edge is now under the frame; the standard presenters draw nothing there (the selection
  fill is inset 2 DIP).
- The frame stays a 1 DIP stroke centered on the list's bounds. Where a neighbor painted later covers its outer
  half, as Mail's splitter does on the right, the inner half is what shows, on the bar as on every other side.
- **The notch in Mail's toolbar line is not the list's; it is the splitter's.** `StandardToolbar` strokes its
  1 DIP frame centered on its bounds, so the lower half of its bottom line lies in the space below it.
  `StandardSplitter` fills its whole bounds with `SurfaceAlt`, drawn after the toolbar, and so paints over that
  half wherever a split container is docked directly under a toolbar. The list and the reader pane stroke their
  own top frames over the line, so the notch shows only above the 8 DIP splitter column. Mail's arrangement, a
  stock `StandardSplitContainer` docked under a stock `StandardToolbar`, is ordinary composition; every consumer
  that docks one under the other gets the notch. It stays open here (see "Not covered"): a splitter that leaves
  the outer half-DIP of its edges to its neighbors, or frames stroked inside their bounds, would remove it, and
  either changes what every split container or every framed control draws.

### Space between the action strip and the feedback

- `FormSurface` leaves 4 DIP between the action strip and its feedback while feedback shows, as far as banners in
  a `StandardPanel { Spacing = 4 }` sit apart. The strip's bottom line is whole, and the first banner no longer
  reads as part of the strip. Measure counts the space as Arrange does: the form's viewport is measured and
  arranged 4 DIP shorter, and the feedback still ends 12 DIP above the surface's bottom. Without feedback there is
  no space, and the layout is the one it was.

### The list's focus ring across an opaque thumb

- After its ring, a focused list strokes the ring again over the thumb, in
  `StandardControlPaint.FocusRingColor(FocusRing, ScrollbarThumb, Background)`: the list's background where the
  ring is under 3:1 on an opaque thumb (ADR 0032's rule, as the scroll view applies it in ADR 0033).
- **The stretch is clipped to the thumb's pill, not its rectangle.** The thumb is filled with `PillRadius`, a
  radius of 6 DIP on the 12 DIP bar, and the ring runs 2 DIP inside the right edge, where the rectangle's
  corners lie outside the pill for 1.1 to 2.0 DIP at each end of the thumb. A clip to the rectangle redrew the
  ring there over the track, in the background color, which is the track's color in the Hosting palettes and
  the high-contrast presets: a gap of about 1.5 DIP at each end, and the same at the left end of the top or
  bottom line with the thumb at either end of the track. The rectangle is the only clip a render list has, so
  **`StandardControlPaint.PillAreasUnderRing(pill, ring, thickness)`** gives rectangles inside the pill whose
  corners sit where the outer edge or the middle of a side of the ring crosses its outline (two with the thumb
  part way down, four at either end), and the stretch is drawn in each. Nothing beside the pill is drawn over;
  every point of the middle of the stroke that lies on the pill is drawn again, the corner arc included. Where
  a side crosses a rounded end, a sliver at most half the stroke wide and about 0.5 DIP long (about 1 DIP for
  the scroll view's 2 DIP ring) stays in the ring's color on the thumb, so at least half the ring's width shows
  along the whole crossing.
- **The scroll view does the same.** Its stretch over each thumb (ADR 0033) was clipped to the thumb's
  rectangle too and left the same gaps in the high-contrast presets, whose track is the surface color; it now
  takes the same clips, for an upright and a lying thumb.
- Unlike the scroll view's, whose translucent Light and Dark thumbs never call for it, the list's opaque role
  thumbs in Light and Dark are mid tones that neither the ring nor the background reaches 3:1 on. The stretch is
  drawn again only where the background reaches 3:1 on the thumb, so the Light and Dark lists draw as before.

| Palette | Ring on the thumb | Drawn across the thumb |
|---|---|---|
| Hosting, Aquatic | 1.46 | Window #202020, 16.29 |
| Hosting, Desert | 1.44 | Window #FFFAEF, 10.43 |
| Hosting, Dusk | 1.91 | Window #2D3236, 12.95 |
| Hosting, Night sky | 1.78 | Window #000000, 21.00 |
| HighContrastLight | 1.00 | `Surface` white, 21.00 |
| HighContrastDark | 1.07 | `Surface` black, 21.00 |
| Light | 1.83 | the ring, as before (`Surface` would be 2.68) |
| Dark | 2.92 | the ring, as before (`Surface` would be 2.66) |

- **A selected row's fill does not meet the thumb.** Each row ends at the bar and the selection fill is inset
  2 DIP from its row, so 2 DIP of the list's background lie between the highlight and the thumb, and the thumb's
  side meets the window color (10.43 to 21:1 in the four themes). Nothing is changed for it; a test pins the gap.

## Consequences

- Behavior changes:
  - In Dark, a selected unread row's dot is #7AB7FF instead of #2673CE. Other presets and Hosting's palettes draw
    the dot as before. A custom palette whose accent is under 3:1 on its surface or selection fill now draws the
    dot in its accent text shade or its text color there.
  - Fields and banners in a `FormSurface` keep their bounds; the form's and the feedback's viewports are 2 DIP
    wider, their clips reach 1 DIP into the margins, and a vertical bar sits 1 DIP further right. A full-width
    field's focus ring and frame are whole on every side.
  - While feedback shows, the form's viewport is 4 DIP shorter and the feedback starts 4 DIP below the strip.
  - Every `StandardListView` strokes its frame after its rows and its bar. In every palette the frame's right
    side is now drawn along the bar.
  - A focused list in a high-contrast preset or a Hosting contrast palette draws the stretch of its ring over the
    thumb in the surface color, on the thumb's pill alone. A focused scroll view in a high-contrast preset now
    clips its stretch to the pill too, and keeps its ring beside the thumb's rounded ends.
  - The form's and the feedback's viewports also reach 1 DIP into the space above and below them; a field the
    keyboard, `Reveal` or a shrinking viewport brings into view keeps 1 DIP for its ring at the edge it meets,
    in the same place as before.
  - A list given an `Accent` after its theme draws its accent marks in that accent or the row's text color, not
    in the theme's accent text.
- New API: `UiListItemRenderContext.AccentText`, `StandardListView.AccentText`,
  `StandardScrollView.HorizontalContentInset`, `StandardScrollView.VerticalContentInset` and
  `StandardControlPaint.PillAreasUnderRing`. All additive; the defaults keep the earlier behavior where noted.
- Unchanged: scrollbar track and thumb colors, the splitter grip, input border colors, every ring's position and
  thickness, the list's bar geometry and hit testing, and `StandardScrollView` with no inset.
- Tests: `UnreadDotContrastTests` (every preset, text-scaled copies, the four Hosting-shaped palettes and a dark
  highlight); `ListViewFrameAndFocusRingTests` (the frame over the bar in eight palettes, the ring across the thumb
  in the four Hosting-shaped palettes and both high-contrast presets, Light and Dark unchanged, the selection's
  gap, with the thumb at the top, part way down and at the bottom, each clip inside the pill and the middle of the
  ring on the pill all drawn again); `ScrollViewFocusTests` (the same for the scroll view's upright and lying
  thumbs); `UnreadDotContrastTests` also covers an accent set after the theme and the accent text's pairing;
  `CompactFormsTests` (a full-width field's ring inside the clip with and without a bar, fields aligned with
  the strip, every field brought into view by Tab and Shift+Tab, by `Reveal` and by a shrinking viewport with its
  whole ring, fields in their places, the 4 DIP space and that Measure counts it); `ScrollViewControlTests` (the
  inset when constrained to the width or the height, when scrolling sideways or up and down, `MakeVisible`,
  argument checks). `HostingShapedPalettes` builds the Windows 11 contrast palettes with the roles
  `WindowsTheme.CreateHighContrastTheme` gives them; `PillGeometry` checks clips against a thumb's pill.
  `SelectionTextRoleTests.Presets_Draw_Selected_Rows_As_Before` now expects Dark's accent text dot, and
  `TabViewHiddenContentLayoutTests` the wider viewport and the feedback viewport's room at the bottom.
- Consumer follow-ups:
  - **Broiler.Mail:** one small code change, then tests and baselines.
    - `MailMessageItemPresenter` on `main` (and every Mail worktree but the adoption branch) builds a new
      `UiListItemRenderContext` member by member and drops `AccentText` (and the selected text colors). It should
      hand its adapted row on with `context.WithItem(...)`, as the adoption branch
      (`claude/ui-09-upstream-adoption`) already does. Until then the Dark selected unread dot falls back to the
      row's text color (#F2F4F8, 11.9:1 on the selection), which reads but is not the accent. A native acceptance
      pass should expect #7AB7FF only with the `WithItem` presenter, and #F2F4F8 without it.
    - Screenshot and pixel baselines change: the Dark selected unread dot, the message list's right edge and its
      bottom-right corner (no stray arc), the form scrollbars (1 DIP to the right, 2 DIP taller), the first banner
      (4 DIP lower) and a focused full-width field's ring (whole, also at the top or the bottom of the form after
      Tab or Shift+Tab). In the contrast themes, a focused list shows its ring across the thumb.
    - Layout tests that pin a form viewport's bounds need the new values: 2 DIP wider and 2 DIP taller, starting
      1 DIP higher, and 4 DIP shorter with feedback shown; the feedback viewport ends 11 DIP above the bottom.
      Field bounds and scroll offsets are unchanged.
    - The toolbar notch above the splitter is the toolkit's (see "The notch in Mail's toolbar line"), not Mail's.
      If Mail wants it gone before the toolkit fixes it, 1 DIP between the inbox toolbar and the split container
      (for example spacing on the docking panel) keeps the toolbar's bottom line whole; that is a workaround, not
      a fix Mail owes.
  - **Broiler.Hosting:** once it is on a Broiler.UI release with this ADR, replace the known gap on the list's focus
    ring in `README.md` and the `CreateHighContrastTheme` remarks: the ring now crosses the thumb in the window
    color and stays whole beside the thumb's rounded ends, with at most half its width showing for about 0.5 DIP
    where it crosses each end. The claim that a selected row's highlight fill meets the thumb can go. Its theme
    tests may check that a focused list's ring crosses the thumb in the window color.
- Not covered:
  - In Light and Dark the list's ring stays at 1.83 and 2.92:1 where it crosses the thumb. The text color would
    read at 5.83 and 5.55:1 there, but changes what the presets draw; it belongs with the thumb colors ADR 0033
    leaves to the preset owners.
  - **Open in the toolkit:** `StandardSplitter` fills its whole bounds after its neighbors and covers the outer
    half of a neighbor's centered 1 DIP frame, so a toolbar's bottom line is notched above a split container's
    splitter (Mail's inbox shows it). A splitter that leaves the outer half-DIP of its edges alone, or frames
    stroked inside their bounds (moving every frame by 0.5 DIP in every preset), would cure it; both change what
    every consumer draws and are left for a decision.
  - A neighbor drawn after the list still covers the outer half of the list's frame on that side.
  - A field the user scrolls part way out of a form's viewport loses the outer edge of its ring there with the
    rest of what is scrolled out. A field brought into view, or at either end of the content, keeps room on every
    side.
  - Where a side of the ring crosses a thumb's rounded end, a sliver at most half the stroke wide and about
    0.5 DIP long (1 DIP for the scroll view's 2 DIP ring) keeps the ring's color on the thumb; a render list
    clips to rectangles only.
  - The scrollbar thumb and track colors, the splitter grip and the input border colors keep their values: their
    contrast in Light and Dark is a design decision for the preset owners.
