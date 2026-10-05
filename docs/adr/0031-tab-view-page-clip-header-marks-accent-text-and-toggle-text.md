# ADR 0031 - Tab view page clip, header focus and selection marks, accent text, and disclosure toggle text

**Status:** Proposed  
**Date:** 2026-10-04

## Context

A pixel-level review of Broiler.Mail's native acceptance screenshots (all fixtures, 640x480 to
1920x1080, light and dark, at 150 %) found four defects that start in Broiler.UI:

- **Page content past the frame.** `StandardTabView` arranged the selected content at the whole page
  below the header strip and drew it without a clip. Whatever the content drew at its own edges ran
  over the page's 1 DIP frame: the last, partly scrolled row of a list, a scrollbar thumb and track, an
  info banner's accent stripe. In every scrolled list the glyphs of the cut row continued for 2 px
  between the focus ring and the frame.
- **A focus ring around the whole view.** While the tab view had focus it stroked its ring 2 DIP inside
  its whole bounds, around the strip and the entire page. The ring did not say which tab had focus,
  enclosed every control on the page (a sighted keyboard user could not tell that focus was on the
  strip), read as a second window frame beside the native border, and ran across the page content
  described above. UI Automation reported the tab item as focused, the drawing the whole view.
- **The selected label.** The selected tab's label was drawn in `StandardControlPaint.Accent`. In the
  `Dark` preset that is #2673CE on #202024, 3.4:1, below the 4.5:1 normal text needs, and dimmer than
  the unselected labels next to it. The color was also the only thing that marked the selected tab,
  which WCAG 1.4.1 does not allow. `Accent` is chosen as a fill for `OnAccent` text (a primary button),
  not as text on a surface; Light's accent happens to pass as text on its surfaces (4.9:1), though not
  on its soft accent fill (4.3:1). ADR 0029 left the tab header on its not-covered list.
- **Toggle text.** A collapsible `FormSection` named its toggle "Show " or "Hide " and its title, so a
  sentence-case title kept its capital in the middle of the phrase: "Show Keyboard shortcuts" beside
  "Show plain text" and "Save settings".

## Decision

### The page is drawn inside its frame

- The selected content is drawn under a clip to the page inside its frame: the page (the view below
  `EffectiveHeaderHeight`) inset by the frame's 1 DIP. Whether a backend centers the frame's stroke on the
  page's edge (Direct2D) or draws it inside, the content stays clear of it.
- `StandardTabView` reports the same rectangle through `GetClipBoundsForChild` (ADR 0028), so
  `GetVisibleBounds()` of the content and everything in it, and therefore the bounds and `Offscreen`
  state a host reports, agree with the drawing. It hit-tests its children there too
  (`ShouldHitTestChildren`), as `StandardScrollView` does with its content bounds: a pointer on the
  frame belongs to the tab view, which leaves it unhandled.
- The content is still arranged and measured at the whole page; only its outermost DIP is hidden. A
  list's or a form's scrollbar at the page's edge loses 1 DIP of its width to the frame. Arranging the
  content inside the frame would move every consumer's layout by 1 to 2 DIP and is not done.
- Drop-downs, menus and other overlays are drawn deferred, after the tree and outside every clip, so a
  combo box near the bottom of a page still opens past it.

### The focus ring marks the selected tab's header

- While the view has focus, the ring is stroked around the selected tab's header
  (`GetTabHeaderBounds(SelectedIndex)`), which is what the strip's keys act on, and no longer around
  the view. It is drawn the theme's `FocusRingOffset` inside the header, in its `FocusRingThickness`,
  with the corner radius less the offset. Light and Dark draw it as before, 1 DIP thick and 2 DIP in;
  the high-contrast presets draw it 2 DIP thick. The view captures the theme's offset and thickness in
  `ApplyTheme`, as `StandardScrollView` does (ADR 0028); unthemed, it uses the shared palette's at
  construction.
- The ring's bottom edge is drawn above the bar that marks the selected tab (below), not across it: the
  ring encloses the header less the bar, inset by the offset. At the default font and DirectWrite's
  Segoe UI metrics a label's descenders end about 6 DIP above the header's bottom; the ring's stroke runs
  1 to 1.5 DIP below them and as far above the bar.
- The ring's strokes stay out of the label's line, which is centered in the header. A `HeaderHeight`
  below about 31 DIP leaves less room under the label than the bar, the offset and the stroke need; the
  ring's top and bottom then move toward the header's edges, so the ring touches or covers the bar rather
  than crossing the label's descenders. At the default height and above nothing moves.
- **Inside the view.** Headers are laid out from the view's left edge whatever its width, so in a strip
  wider than the view (a narrow window, a large text size, or a document strip with many tabs, as in
  Broiler.Code) the selected header can lie partly or wholly past the view's right edge. The ring is drawn
  around the part of the header inside the view. When less of it shows than the narrowest header (48 DIP),
  too little to read as a tab, the ring is drawn around the visible strip instead, where the strip's keys
  still act; a focused strip always shows its ring.
- **High contrast.** In a palette with `IsHighContrast` the ring is at least 2 DIP thick. Those
  palettes draw their borders in the text color, often the focus color too (black on white), and a
  1 DIP ring would read as one more border. Both high-contrast presets already set 2 DIP; a host palette
  that copies a preset keeps it, and one that does not still gets 2 DIP.
- **Every theme.** The ring is drawn on the selected header's fill. When `FocusRing` has less than 3:1
  against an opaque `SelectedHeaderBackground` (a focus color equal to the fill, for example), the ring
  takes `SelectedHeaderForeground`, the color chosen to be read there. No preset triggers this: the ring
  stands at 4.9:1 (Light), 7.8:1 (Dark), 21:1 and 19.6:1 (high contrast) on the selected header.
- With no tab selected (no tabs, or the view not laid out) the ring is drawn around the view as before.
- The ring is drawn whenever the view has focus, as before, not only after keyboard input. An
  application moves focus to the strip itself (Mail does at startup and after a tab change), and the
  ring is how the user finds where the arrow keys go.

### An accent text role

- **`StandardThemeTokens.AccentText`** is the color of text and marks drawn in the accent on a surface or
  on the soft accent fill (`AccentSoft`, where a checked toggle button draws its label). Like the
  selection and state roles (ADR 0029) it is not `required` and follows another role until a theme sets
  it: it is `Accent` unless set.
- `Dark` sets it to #7AB7FF: the accent's hue (212.5 degrees), light enough to read on every dark
  surface. It is also Dark's `FocusRing` and `Info` color. `Light` sets it to #0A61BE, its `AccentHover`
  shade: Light's accent reads on the surfaces (4.9:1) but at 4.3:1 on `AccentSoft`, where a checked,
  pressed or indeterminate toggle button draws its label (ADR 0029 left that on its not-covered list).
  `HighContrastLight` and `HighContrastDark` keep their accent, the highlight; a palette a host builds
  from them (Hosting's system palette maps `Accent` to Highlight) follows its own accent.
- `AccentTextContrast` reports `AccentText` on `Surface`; `StandardControlPaint.AccentText` reads the
  shared palette. Every preset reaches 4.5:1 on `Surface`, `SurfaceAlt` and `AccentSoft`, which the tests
  check:

  | Preset | `AccentText` (was) | on `Surface` | on `SurfaceAlt` | on `AccentSoft` |
  |---|---|---|---|---|
  | Light | #0A61BE (the accent #0B6FD8) | 6.06 (4.91) | 5.79 (4.69) | 5.28 (4.28) |
  | Dark | #7AB7FF (the accent #2673CE) | 7.77 (3.42) | 6.82 (3.01) | 6.27 (2.76) |
  | HighContrastLight | #0000CC (the accent) | 11.22 | 11.22 | 9.46 |
  | HighContrastDark | #00E0FF (the accent) | 13.11 | 13.11 | 8.54 |

- **Copies.** A value set in an initializer or a `with` is kept by every copy, as `StateFill`'s is. A
  preset's own value belongs to its accent: the preset keeps the accent it chose the shade for beside it,
  and a copy that changes `Accent` without setting `AccentText` (`Dark with { Accent = brand }`) draws its
  new accent as text, as it did before the role existed, rather than Dark's blue beside the brand's check
  boxes and buttons. A copy that keeps the accent (`WithTextScale`, `Select` with a density, another text
  color) keeps the preset's accent text. A copy that changes the accent and wants a readable accent text
  sets `AccentText` as well, in either order.

#### Inventory of the accent drawn as text

Every place in `src/Foundation` and `src/Implementations`, and in the samples, that draws text or a glyph
in `Accent`, with the contrast it had in Dark and Light:

| Where | Drawn on | Dark / Light before | Now |
|---|---|---|---|
| `StandardTabView` selected label | `SelectedHeaderBackground` (`Surface`) | 3.42 / 4.91 | `SelectedHeaderForeground` (new), set to `AccentText` by `ApplyTheme`: 7.77 / 6.06. Unthemed, the shared `AccentText` when the view is built, as its fills are |
| `StandardLabel` role `Accent` | a surface | 3.42 / 4.91 | `AccentText` |
| `StandardToggleButton`, themed: label and icon off, hovered, and on the state fills | `Surface`, `SurfaceAlt`, `StateFill` | 3.42, 3.01, 2.76 / 4.91, 4.69, 4.28 | `Foreground` is `AccentText`: 7.77, 6.82, 6.27 / 6.06, 5.79, 5.28 |
| `StandardToggleButton`, never themed | `Surface` and fixed light hover and pressed fills (#F2F7FF, #D8E8FC) | 3.42 on a Dark `Surface`; 3.8 to 4.6 on the fixed fills in both | keeps `Accent`: Dark's `AccentText` would read at 1.9:1 on the fixed hover fill. Under a shared Dark palette an unthemed toggle's label stays at 3.42 on the surface; theme it |
| `StandardFormatCodeView.InlineCodeForeground` | its `Background` (`SurfaceAlt`) | 3.01 / 4.69 | `AccentText` (6.82 / 5.79), also for an unthemed view, whose background is the shared `SurfaceAlt` |
| `StandardCodeEditorPalette` `Keyword` | `Surface`, current line `SurfaceAlt` | 6.24, 5.49 / 4.91, 4.69 | unchanged: dark palettes already lighten the accent by 35 % |
| `StandardCodeEditorPalette` `ControlKeyword` (`AccentPressed`) | as above | 5.29, 4.64 / 8.41, 8.04 | unchanged |
| Samples: Win32 and WebAssembly demo sidebar, selected item's label and glyph | `AccentSoft` | 2.76 / 4.28 | unchanged (sample code, not shipped). Text on the selection fill belongs to `SelectionText` (ADR 0029), not `AccentText` |
| Samples: the same demos' theme chevron | `Surface` | 3.42 / 4.91 | unchanged (sample code) |

- `StandardToggleButton` replaced its label with `StateText` when "the accent" was the state fill
  (ADR 0029). It now does so when either the label it actually draws off (`AccentText` when themed,
  `Accent` when not) or the accent is the state fill. A palette that maps the accent and the selection
  fill to one highlight and sets its own `AccentText` for the surfaces (#7AB7FF on a #005A9E highlight
  reads at 3.6:1) still gets the state text chosen for that fill. In the presets and in palettes built
  like Hosting's the result is the same as before.
- Broiler.UI has no link control; `StandardLabel`'s `Info` role draws `Info`, which reads in every
  preset (7.77:1 in Dark). The `FormSection` toggle is a `StandardButton` and draws in `Text`.
- The accent as a fill or a mark that is not text keeps `Accent` and needs 3:1, which Dark's accent
  meets on `Surface` (3.42) and `SurfaceAlt` (3.01): check box and radio button fills, progress bar and
  slider fills, the list's unread dot, window and dialog active borders, the file dialog's title bar and
  the primary button (both with `OnAccent` text, 4.74:1 in Dark). The unread dot also sits on a selected
  row's `AccentSoft`, where Dark's accent is 2.76:1; ADR 0034 draws it in `AccentText` there.

### A selected-tab mark that is not color

- `StandardTabView` draws a bar with rounded ends under the selected tab's label: along the bottom of
  its header, on the page's frame, as wide as the header less `HeaderPaddingX` on each side (the whole
  header when that leaves nothing). It is drawn for the selected tab only, after its label, and only
  the part inside the view (see the ring, above).
- **`SelectedIndicatorThickness`** is 2 DIP by default; 0 (or less, or NaN) draws none. A 3 DIP bar put
  the ring 5 DIP above the header's bottom, where a high-contrast theme's 2 DIP ring touched the
  descenders. The bar is drawn no thicker than the room between the label's line and the header's
  bottom (6 DIP at the default height), so a large value does not cover the label; the ring is still
  drawn, out of the label's line, over the bar if it must.
- **`SelectedIndicatorColor`** follows `SelectedHeaderForeground` until the application sets it, so a
  label color the application chooses colors the bar too. A mark that is not text needs 3:1 against
  what surrounds it; in every preset the bar has at least 4.5:1 against the selected header's fill and
  against `Surface` and `SurfaceAlt`, either of which can show through the strip
  (`HeaderBackground` is transparent).
- The bar lies inside the header. `EffectiveHeaderHeight`, `GetTabHeaderBounds`, the tab semantic nodes
  and hit testing are the same with or without it, at every font size.
- **The selected label's color is captured.** An unthemed view used to read the selected label from the
  shared palette each time it drew, while it captured the fills under it when it was built. After the
  shared palette changed, one palette's label stood on another's fill: Dark's #7AB7FF on Light's white
  reads at 2.1:1. `SelectedHeaderForeground`, and the bar that follows it, is now captured when the view
  is built, as its other colors are and as `StandardControlPaint.ApplyTheme` documents.

### Toggle text for a collapsible section

- **`FormSection.ShowText`** and **`HideText`** are the toggle's text while the content is hidden and
  while it is shown. Null or blank (the default) composes "Show " or "Hide " and the title, as before.
  Setting either updates the toggle at once; a section that cannot collapse has no toggle and only
  keeps the values.
- The text is the toggle's accessible name. Everything ADR 0028 put on the toggle is unchanged: it
  discloses the section and reports `Expanded` or `Collapsed`, its `Controls` is the section's
  `Content`, the group keeps the title as its name and reports neither state, and `Collapse()` moves
  focus from the content to the toggle.
- Two strings rather than a formatter: they cover sentence case and a translation whose phrase is not
  a prefix and the title, and an application that wants a formatter calls it when it sets them.

## Consequences

- Behavior changes:
  - Tab page content is clipped 1 DIP inside the page's frame. Its visible bounds are cut there, an
    element that lies only under the frame reports `Offscreen`, and a pointer on the frame no longer
    reaches the content.
  - A focused tab view rings the selected header instead of the whole view, and only the part of it
    inside the view; when too little of the selected header shows, it rings the visible strip. Code or
    tests that look for the ring at `Inset(Bounds, 2)` must look at the header.
  - The selected tab has a 2 DIP bar under its label, drawn only inside the view.
  - At a `HeaderHeight` below about 31 DIP the ring keeps out of the label's line and may touch or cover
    the bar; a `SelectedIndicatorThickness` larger than the room under the label is drawn as thick as
    that room.
  - Dark: the selected tab label, accent labels, themed toggle buttons' labels and icons, and inline
    format codes are #7AB7FF instead of #2673CE.
  - Light: the same are #0A61BE instead of #0B6FD8, a shade darker, so a checked, pressed or
    indeterminate toggle button's label reads on its fill. The high-contrast presets draw the same
    colors as before.
  - A copy of `Light` or `Dark` that changes `Accent` without setting `AccentText` draws its accent text
    in its own accent, as before this ADR.
  - An unthemed tab view keeps the selected label color of the shared palette it was built under, as
    it keeps its fills; it used to read the label from the shared palette each time it drew.
  - A focused tab view in a high-contrast palette draws its ring at least 2 DIP thick.
- At the default font and in every preset, header and page geometry and every layout are unchanged.
- ADR 0029's not-covered items "`StandardTabView` draws its selected header in the accent on `Surface`"
  and the presets' checked toggle label on `AccentSoft` (2.76:1 in Dark, 4.28:1 in Light) are resolved
  for themed controls. Its other items stand.
- Consumer follow-ups:
  - Broiler.Mail: take the release and refresh the native screenshot baselines (the tab strip, the
    ring, the page edges, and Light's slightly darker accent text). Give the collapsible sections
    sentence-case toggles: `SettingsView` ("Show keyboard shortcuts" / "Hide keyboard shortcuts") and
    `AccountProfileView` ("Show sent-copy settings" / "Hide sent-copy settings"); the composer's "Cc and
    Bcc" reads correctly composed. Mail tests that compare an element's `GetVisibleBounds()` with its
    `Bounds`, or hit-test at the edge of a tab page, see the 1 DIP clip. Tests that expect Light's accent
    as the color of an accent label or a selected tab expect `AccentText`.
  - Broiler.Code: its document strip (`CodeShellFactory`) is a content-less `StandardTabView` that can be
    wider than its pane. The ring and the bar now stay inside it, but the labels of headers past its edge
    are still drawn past it (see Not covered), and `StandardTabView` lays out every tab whatever
    `VisibleTabCapacity` says, so Code should clip the strip to its pane.
  - Broiler.Hosting: the system high-contrast palette leaves `AccentText` following `Accent`, the
    Highlight color, so the selected tab label, its bar and themed toggle labels are drawn in Highlight
    on Window. That keeps the highlight's meaning and reads in the Windows 11 contrast themes, but a
    user's own pair is not guaranteed 4.5:1. Hosting should set `AccentText` to WindowText when Highlight
    falls short on Window, as it already falls back for `Info` and `FocusRing`, and extend its palette
    readability test to `AccentText` on `Surface`. The 2 DIP minimum ring already applies there.
  - Applications that copy `Light` or `Dark` with their own accent get that accent as text, as before;
    to make it read where the presets' shade did, they set `AccentText` too.
- Not covered:
  - The clip is rectangular. In the page's rounded corners (6 DIP radius) opaque content drawn into
    the clip's square corner can still cover the frame's arc, by about 1 DIP along the diagonal.
  - The headers themselves are not clipped to the view: the fills and labels of headers laid out past a
    narrow view's edge are drawn there, as before. Clipping or scrolling the strip is a change of its
    own.
  - The ring is stroked 1 DIP thick (Light, Dark) centered on whole-DIP edges, as every control's focus
    ring is. At 100 % and 150 % scaling it is anti-aliased across two pixel rows, so the contrast that
    reaches the screen is below the 4.9:1 and 7.8:1 of its color. Aligning strokes to device pixels
    belongs in the shared drawing helpers or the renderer, for every control at once.
  - A toggle button that is never themed keeps `Accent` and its fixed light state fills, so under a
    shared Dark palette its label stays at 3.4:1 on the surface, and under Light its checked label on
    `AccentSoft` at 4.28:1.
  - The sample applications' sidebar, which draws the selected item's label in the accent on the
    selection fill.
