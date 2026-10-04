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
  not as text on a surface; Light's accent happens to pass as text (4.9:1). ADR 0029 left the tab
  header on its not-covered list.
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
  ring encloses the header less the bar, inset by the offset. Inside the header it cannot leave the view.
  At the default font and DirectWrite's Segoe UI metrics a label's descenders end about 6 DIP above the
  header's bottom; the ring's stroke runs 1 to 1.5 DIP below them and as far above the bar.
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

- **`StandardThemeTokens.AccentText`** is the color of text and marks drawn in the accent on a surface.
  Like the selection and state roles (ADR 0029) it is not `required` and follows another role until a
  theme sets it: it is `Accent` unless set.
- Only `Dark` sets it, to #7AB7FF: the accent's hue (212.5 degrees), light enough to read on every dark
  surface. It is Dark's `FocusRing` and `Info` color, so the dark palette keeps the shape of the light
  one, where the accent, the focus ring and info are one color. Light keeps its accent, and so do
  `HighContrastLight` and `HighContrastDark`, whose accent is their highlight; a palette a host builds
  from them (Hosting's system palette maps `Accent` to Highlight) follows its own accent.
- `AccentTextContrast` reports `AccentText` on `Surface`; `StandardControlPaint.AccentText` reads the
  shared palette. Every preset reaches 4.5:1 on `Surface` and `SurfaceAlt`, which the tests check:

  | Preset | `AccentText` | on `Surface` | on `SurfaceAlt` | on `AccentSoft` |
  |---|---|---|---|---|
  | Light | #0B6FD8 (the accent) | 4.91 | 4.69 | 4.28 |
  | Dark | #7AB7FF (was the accent #2673CE) | 7.77 (3.42) | 6.82 (3.01) | 6.27 (2.76) |
  | HighContrastLight | #0000CC (the accent) | 11.22 | 11.22 | 9.46 |
  | HighContrastDark | #00E0FF (the accent) | 13.11 | 13.11 | 8.54 |

- A set value is kept by every copy, as `StateFill` is. A copy of `Dark` that changes its accent keeps
  #7AB7FF as its accent text and should set `AccentText` too; a copy of any other preset follows the
  new accent.

#### Inventory of the accent drawn as text

Every place in `src/Foundation` and `src/Implementations`, and in the samples, that draws text or a glyph
in `Accent`, with the contrast it had in Dark and Light:

| Where | Drawn on | Dark / Light before | Now |
|---|---|---|---|
| `StandardTabView` selected label | `SelectedHeaderBackground` (`Surface`) | 3.42 / 4.91 | `SelectedHeaderForeground` (new), set to `AccentText` by `ApplyTheme`; unthemed, the shared `AccentText` read when drawn, as `Accent` was |
| `StandardLabel` role `Accent` | a surface | 3.42 / 4.91 | `AccentText` |
| `StandardToggleButton`, themed: label and icon off, hovered, and on the state fills | `Surface`, `SurfaceAlt`, `StateFill` | 3.42, 3.01, 2.76 / 4.91, 4.69, 4.28 | `Foreground` is `AccentText`: 7.77, 6.82, 6.27 in Dark |
| `StandardToggleButton`, never themed | `Surface` and fixed light hover and pressed fills (#F2F7FF, #D8E8FC) | 3.42 on a Dark `Surface`; 3.8 to 4.6 on the fixed fills in both | keeps `Accent`: `AccentText` would read at 1.9:1 on the fixed hover fill. Under a shared Dark palette an unthemed toggle's label stays at 3.42 on the surface; theme it |
| `StandardFormatCodeView.InlineCodeForeground` | its `Background` (`SurfaceAlt`) | 3.01 / 4.69 | `AccentText` (6.82 in Dark), also for an unthemed view, whose background is the shared `SurfaceAlt` |
| `StandardCodeEditorPalette` `Keyword` | `Surface`, current line `SurfaceAlt` | 6.24, 5.49 / 4.91, 4.69 | unchanged: dark palettes already lighten the accent by 35 % |
| `StandardCodeEditorPalette` `ControlKeyword` (`AccentPressed`) | as above | 5.29, 4.64 / 8.41, 8.04 | unchanged |
| Samples: Win32 and WebAssembly demo sidebar, selected item's label and glyph | `AccentSoft` | 2.76 / 4.28 | unchanged (sample code, not shipped). Text on the selection fill belongs to `SelectionText` (ADR 0029), not `AccentText` |
| Samples: the same demos' theme chevron | `Surface` | 3.42 / 4.91 | unchanged (sample code) |

- `StandardToggleButton` replaced its label with `StateText` when "the accent" was the state fill
  (ADR 0029). It now compares the label it actually draws off: `AccentText` when themed, `Accent` when
  not. In the presets and in palettes built like Hosting's (accent text following the accent) the
  result is the same.
- Broiler.UI has no link control; `StandardLabel`'s `Info` role draws `Info`, which reads in every
  preset (7.77:1 in Dark). The `FormSection` toggle is a `StandardButton` and draws in `Text`.
- The accent as a fill or a mark that is not text keeps `Accent` and needs 3:1, which Dark's accent
  meets on `Surface` (3.42) and `SurfaceAlt` (3.01): check box and radio button fills, progress bar and
  slider fills, the list's unread dot, window and dialog active borders, the file dialog's title bar and
  the primary button (both with `OnAccent` text, 4.74:1 in Dark).

### A selected-tab mark that is not color

- `StandardTabView` draws a bar with rounded ends under the selected tab's label: along the bottom of
  its header, on the page's frame, as wide as the header less `HeaderPaddingX` on each side (the whole
  header when that leaves nothing). It is drawn for the selected tab only, after its label.
- **`SelectedIndicatorThickness`** is 2 DIP by default; 0 (or less, or NaN) draws none. A 3 DIP bar put
  the ring 5 DIP above the header's bottom, where a high-contrast theme's 2 DIP ring touched the
  descenders.
- **`SelectedIndicatorColor`** follows `SelectedHeaderForeground` until the application sets it, so a
  label color the application chooses colors the bar too. A mark that is not text needs 3:1 against
  what surrounds it; in every preset the bar has at least 4.5:1 against the selected header's fill and
  against `Surface` and `SurfaceAlt`, either of which can show through the strip
  (`HeaderBackground` is transparent).
- The bar lies inside the header. `EffectiveHeaderHeight`, `GetTabHeaderBounds`, the tab semantic nodes
  and hit testing are the same with or without it, at every font size.

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
  - A focused tab view rings the selected header instead of the whole view. Code or tests that look
    for the ring at `Inset(Bounds, 2)` must look at the header.
  - The selected tab has a 2 DIP bar under its label.
  - Dark: the selected tab label, accent labels, themed toggle buttons' labels and icons, and inline
    format codes are #7AB7FF instead of #2673CE. Light and the high-contrast presets draw the same
    colors as before.
  - A focused tab view in a high-contrast palette draws its ring at least 2 DIP thick.
- At the default font and in every preset, header and page geometry and every layout are unchanged.
- ADR 0029's not-covered item "`StandardTabView` draws its selected header in the accent on `Surface`"
  is resolved. Its other items stand; in particular Light's checked toggle label on `AccentSoft` stays
  at 4.28:1, since `AccentText` is defined against the surfaces and Light's accent passes there.
- Consumer follow-ups:
  - Broiler.Mail: take the release and refresh the native screenshot baselines (the tab strip, the
    ring, the page edges). Give the collapsible sections sentence-case toggles:
    `SettingsView` ("Show keyboard shortcuts" / "Hide keyboard shortcuts") and `AccountProfileView`
    ("Show sent-copy settings" / "Hide sent-copy settings"); the composer's "Cc and Bcc" reads correctly
    composed. Mail tests that compare an element's `GetVisibleBounds()` with its `Bounds`, or hit-test
    at the edge of a tab page, see the 1 DIP clip.
  - Broiler.Hosting: the system high-contrast palette leaves `AccentText` following `Accent`, the
    Highlight color, so the selected tab label, its bar and themed toggle labels are drawn in Highlight
    on Window. That keeps the highlight's meaning and reads in the Windows 11 contrast themes, but a
    user's own pair is not guaranteed 4.5:1. Hosting should set `AccentText` to WindowText when Highlight
    falls short on Window, as it already falls back for `Info` and `FocusRing`, and extend its palette
    readability test to `AccentText` on `Surface`. The 2 DIP minimum ring already applies there.
  - Applications that copy `Dark` and change its accent set `AccentText` as well.
- Not covered:
  - The clip is rectangular. In the page's rounded corners (6 DIP radius) opaque content drawn into
    the clip's square corner can still cover the frame's arc, by about 1 DIP along the diagonal.
  - A toggle button that is never themed keeps `Accent` and its fixed light state fills, so under a
    shared Dark palette its label stays at 3.4:1 on the surface.
  - The sample applications' sidebar, which draws the selected item's label in the accent on the
    selection fill.
