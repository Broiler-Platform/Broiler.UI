# ADR 0033 - Scrollbar theme roles, and a drop-down arrow that grows with the font

**Status:** Proposed  
**Date:** 2026-10-05

## Context

Two defects remained after ADRs 0028 to 0032, both visible in Broiler.Mail under a contrast theme or a
larger text size.

- **Scrollbars that ignore the theme.** `StandardScrollView`, `StandardRichEdit` and `StandardFormatCodeView`
  drew their bars in one fixed translucent pair, a track of #94A3B8 at 20 % and a thumb of #7D8DA3 at 67 %, in
  every palette. `ApplyTheme` did not touch them; ADR 0028 left the mapping to a high-contrast pass. Over white,
  as in `HighContrastLight`, that is a #A1ADBD thumb on a #EAEDF1 track: 1.94:1, and 2.28:1 against the surface
  beside the bar. WCAG asks 3:1 for the parts of a control that show what it is and where it stands. Meanwhile
  `StandardListView`, `StandardTreeView` and `StandardCodeEditor` themed their bars from `SurfaceDisabled` and
  `BorderStrong`, so in high contrast they drew window text on the window color. There was no role for a
  scrollbar, so a palette had no way to restyle every bar, and in one contrast-themed window the list's bar was
  black on white while the form scrolling beside it was pale gray.
- **A drop-down arrow at a fixed offset.** `StandardComboBox` draws its arrow as a glyph of its own font, "v"
  (or "^" while open), at `Bounds.Right - 18`, and clips its text to `Width - 22`. ADR 0029 made the box as
  tall as its font needs, but the arrow's place stayed fixed. At 200 % text the "v" is 15.3 DIP wide and ends
  2.7 DIP from the right edge: on the 1 DIP frame's inner side, over the focus ring drawn 2 DIP inside it. The
  open "^" (21.9 DIP) ran 3.9 DIP past the edge. The text still stopped 4 DIP before the arrow.

## Decision

### Scrollbar roles

- **`StandardThemeTokens.ScrollbarTrack`** is the track of a scrollbar and the corner where a vertical and a
  horizontal bar meet. **`ScrollbarThumb`** is the thumb drawn on it. Like the selection and state roles
  (ADR 0029), neither is `required`, and the presets leave both unset:
  - `ScrollbarTrack` is `SurfaceDisabled` and `ScrollbarThumb` is `BorderStrong` until a theme sets them. These
    are the colors the list, tree and code editor have always drawn, so those controls draw the same colors in
    every palette.
  - A copy that changes `SurfaceDisabled` or `BorderStrong` carries the change onto the bars. A set value is kept
    by every copy (`with`, `WithTextScale`).
  - In the high-contrast presets the pair is white and black (`HighContrastLight`) or black and white
    (`HighContrastDark`). Broiler.Hosting's system contrast palette (`WindowsTheme.CreateHighContrastTheme`) maps
    `SurfaceDisabled` to Window and `BorderStrong` to WindowText, so it draws a WindowText thumb on a Window track
    without a change in Hosting.
- **`StandardThemeTokens.ScrollbarThumbContrast`** is the lesser of the thumb's ratio on the track and on
  `Surface`. A thumb is as wide as its track, so its sides meet the surface beside the bar, and in high contrast
  the track is the surface itself. Tests check 3:1 for both high-contrast presets, a scaled copy, and palettes
  shaped like Hosting's for Aquatic and Desert.
- **`StandardControlPaint.ScrollbarTrack`** and **`ScrollbarThumb`** read the shared palette, like the other roles.
- There are no hover or pressed roles. No standard control draws a hovered or dragged thumb differently. A state
  added later can bring a role that defaults to `ScrollbarThumb`.

### Controls use the roles

The list, tree and code editor draw the roles in every theme. Unless a theme sets them, the roles are exactly
the colors these controls drew before.

The scroll view, rich edit and formatting code view keep their translucent bars until the theme gives scrollbars
colors of their own. **`StandardControlPaint.ScrollbarColors(theme, track, thumb)`** applies this rule. A theme
gives scrollbars colors when:

- it sets either role, even to the very color the role would follow. Whether a role was set is recorded on the
  tokens and kept by every copy, so `Light with { ScrollbarThumb = Light.BorderStrong }` reaches all six controls;
- it says it is high contrast (`IsHighContrast`), as every contrast preset and Hosting's system palette do; or
- its surface and text sit at the extremes of lightness. This is the test the list, the tree and the code
  editor's palette already make for their own high-contrast cues (the weighted channel average of `Surface` and
  `Text` more than 0.9 apart). A black-and-white palette built with the four-color constructor, which does not
  set the flag, therefore draws every bar in its roles, as the list always did. Light (0.87) and Dark (0.83) are
  below the threshold.

The rule:

- returns the theme's pair, or the control's own pair. It never mixes them: a theme that sets only the thumb
  gets that thumb on the theme's track, not on the translucent one.
- `ApplyTheme` moves each bar color on to the new theme's while it still holds the color the last theme gave it,
  the way `StandardThemeFonts.Follow` treats a font. A theme that says nothing about scrollbars, applied after a
  contrast theme, brings the translucent bars back. A color the application set on `ScrollbarTrack` or
  `ScrollbarThumb` is kept by every later theme, as ADR 0028 kept it; only the color it left alone follows.
- A control that is built under the shared palette and never themed takes the pair from the shared palette, as
  the list already does.

This is ADR 0029's selection rule applied to scrollbars. Drawing the roles in these three controls under every
theme would change the Light and Dark presets. In Light, for example, the translucent bars composite to a #A1ADBD
thumb on #EAEDF1, while the roles are #8DA0B6 on #F1F4F8. So under Light and Dark one window still shows two
scrollbar looks, the translucent one and the role one, and `ScrollbarTrack`, `ScrollbarThumb` and
`ScrollbarThumbContrast` describe only the second. Their XML documentation says so. Unifying the two is a preset
change and is listed below as a follow-up that needs a design decision.

Every control that draws scrollbars:

| Control | Bars | Colors | Before | Now |
|---|---|---|---|---|
| `StandardScrollView` | vertical, horizontal, corner | `ScrollbarTrack`, `ScrollbarThumb` | translucent pair; `ApplyTheme` left them | `ScrollbarColors`, app colors kept; ring whole over the thumb |
| `StandardRichEdit` | vertical, horizontal | `ScrollbarTrack`, `ScrollbarThumb` | translucent pair over the text; not themed | `ScrollbarColors`, app colors kept; opaque bars beside the text |
| `StandardFormatCodeView` | vertical, horizontal | `ScrollbarTrack`, `ScrollbarThumb` | translucent pair over the text; not themed | `ScrollbarColors`, app colors kept; opaque bars beside the text |
| `StandardListView` | vertical | `ScrollbarTrack`, `ScrollbarThumb` | `SurfaceDisabled`, `BorderStrong` | the roles |
| `StandardTreeView` | vertical, horizontal (`StandardScrollbars`) | `StandardScrollbars.ApplyPaint` | `SurfaceDisabled`, `BorderStrong` | the roles |
| `StandardCodeEditor` | vertical, horizontal (`StandardScrollbars`) | `StandardScrollbars.ApplyPaint` | `SurfaceDisabled`, `BorderStrong` | the roles |
| `StandardScrollbar` (unthemed default) | - | `Track`, `Thumb` | shared `SurfaceDisabled`, `BorderStrong` | shared roles |

`FormViewport` and `FormSurface` scroll through a `StandardScrollView` and follow it. The file and font
dialogs scroll through `StandardListView`. No other standard control draws a scrollbar. The combo box's drop-down
shows at most `MaxDropDownItems` rows, and the menu, edit, tab view and toolbar do not scroll with a bar.

Thumb contrast of the scroll view, rich edit and formatting code view, on the track / on the surface beside the
bar (WCAG ratios; the translucent pair is composited over the surface):

| Palette | Before | Now |
|---|---|---|
| Light | 1.94 / 2.28 | unchanged |
| Dark | 2.30 / 3.29 | unchanged |
| HighContrastLight | 1.94 / 2.28 | 21.00 / 21.00 |
| HighContrastDark | 2.90 / 3.76 | 21.00 / 21.00 |
| System palette, Aquatic (Window #202020, WindowText #FFFFFF) | 2.31 / 3.29 | 16.29 / 16.29 |
| System palette, Desert (Window #FFFAEF, WindowText #3D3D3D) | 1.89 / 2.21 | 10.43 / 10.43 |

The list, tree and code editor draw what they drew before in every palette: 2.43 / 2.68 in Light, 2.21 / 2.66 in
Dark, and the window text on the window color in the contrast palettes.

### Opaque bars are drawn beside the text, not over it

The rich edit and the formatting code view draw their bars over the edge of their text. The rich edit's vertical
bar covers the last 12 DIP of its text column, and its horizontal bar the bottom 12 DIP of the window onto the
text. The formatting code view's bars run along the very edge of the box and cover the 4 DIP of text inside the
right padding and 6 DIP inside the bottom one. With the translucent pair the text showed through (a track at
20 %). An opaque pair, as high contrast gives them, would hide it. In `HighContrastLight` and `HighContrastDark`
the track is the surface color, so the text would end without a visible edge. The scroll view, the list, the tree
and the code editor already carve their bars out of the content and are not affected.

So once the track or the thumb is fully opaque (alpha 255), whether from a theme or set by the application:

- The rich edit keeps a strip of `ScrollbarThickness` beside the text for each bar its policy allows
  (`VerticalScrollPolicy`, `HorizontalScrollPolicy` not `Never`). It keeps the strip whether or not the bar shows,
  so the bar coming and going never rewraps the document, which is why the bar overlays the text in the first
  place (`RichEditScrollMetrics`). The text column, the page and the padding are laid out in the box less the
  strips, and the bar is drawn in its strip at the same place as before. Each bar stops at the other's strip, so
  they no longer overlap at the corner. A box that is sized to its text (`VerticalScrollPolicy.Never`) grows by
  the horizontal strip.
- The formatting code view ends its text where the bar starts: the text's right edge is `ScrollbarThickness` from
  the box's edge instead of `PaddingX`, and with `NoWrap` its bottom edge likewise. The edge is kept whether or
  not the bar shows.
- Translucent bars keep overlaying the text, so the Light and Dark presets lay out and draw exactly as before.

### A focus ring that crosses an opaque thumb

A focused scroll view that is a keyboard stop strokes its ring 2 DIP inside its bounds, after the bars, so the
ring runs through the vertical bar (and the horizontal one). In `HighContrastLight` the ring and the thumb are
both black (1.00:1). In `HighContrastDark` the ring is yellow on a white thumb (1.07:1). Either way the ring had a
gap the length of the thumb. The view now draws the stretch over each thumb again, clipped to the thumb, in
`StandardControlPaint.FocusRingColor(ring, thumb, surface)`: the surface color where the ring is under 3:1 on an
opaque thumb (ADR 0032's rule). A translucent thumb, as in Light and Dark, adds nothing to the frame.

### A drop-down arrow that grows with the font

- The arrow sits in a slot at the right edge that grows with `Font` by the ratio of a line of the font to a
  default line, never less than 1. The glyph's width keeps that ratio. The box's height grows differently
  (ADR 0029): by the extra height of the line, keeping the default margin, so at 200 % the box is 1.625 times as
  tall while the slot is twice as wide. The glyph starts `18 x` that ratio from the edge, and the text is clipped
  `22 x` that ratio short of it. At the default
  font this is 18 and 22, and the box draws exactly the commands it drew before. At 200 % it is 36 and 44: the
  "v" ends 20.7 DIP from the edge and the "^" 14.1 DIP, and the text stops 8 DIP before the arrow. Those are
  Segoe UI's widths. DejaVu Sans, the face a Linux test run measures with, draws a wider "^", which ends 4.6 DIP
  from the edge at the default font and 9.2 DIP at 200 %. The tests check that, at 150 % and 200 %, both margins
  grow at least as much as the font and the arrow ends inside the focus ring, not that it keeps a fixed distance.
- The slot follows the font even when the application sets `PreferredSize`. A box with a fixed size, such as the
  file and font dialogs' boxes or Mail's, still keeps its arrow clear of the right edge of the frame. At a larger
  font its text gets less width. This is horizontal only: a box whose set height is shorter than a line of its
  font still draws its text and arrow past its bottom edge (see "Not covered").
- The arrow stays a glyph rather than a drawn chevron. The default rendering is unchanged, and the glyph keeps
  the font's weight and color. The slot is scaled from the line height and does not measure the glyph. A family
  whose "v" is much wider for its line than the default font's comes closer to the frame. A drawn chevron sized
  from the line would remove that dependency, but it changes the default rendering. It is left as an option.

## Consequences

- With the Light and Dark presets, every scrollbar and every combo box draws exactly as before. The tests pin
  both kinds of scrollbar colors in Light, Dark and a scaled Dark, the overlay geometry of the rich edit and the
  formatting code view in Light, and the combo box's arrow and text clip at the default font. In the
  high-contrast presets, the scroll view, rich edit and formatting code view now draw the text color as the thumb
  on a track of the surface color (21:1). The list, tree and code editor are unchanged.
- In high contrast, and whenever an application sets an opaque bar color, the rich edit's text column is
  `ScrollbarThickness` (12 DIP) narrower while its vertical policy allows a bar, and the formatting code view's is
  4 DIP narrower. Text wraps earlier there; nothing is hidden under a bar. Tests check, for left- and
  right-aligned wrapped prose and for unwrapped lines scrolled to the end, that no text and no text clip reaches
  into an opaque bar.
- `ApplyTheme` on the scroll view, rich edit and formatting code view now moves `ScrollbarTrack` and
  `ScrollbarThumb` to the theme's pair while they still hold the last theme's. A color the application set is
  kept, as before this ADR. Tests set a thumb and apply Light, a contrast theme and Dark in turn.
- A palette that sets the roles to the colors they follow, or a black-and-white palette that does not set
  `IsHighContrast`, now draws the scroll view, rich edit and formatting code view in its roles, as the list, tree
  and code editor already did.
- `Broiler.UI.Standard.Tests` now references `Broiler.UI.TreeView.Standard`, `Broiler.UI.RichEdit.Standard` and
  `Broiler.UI.FormatCodeView.Standard`, so that one test can cover every scrollbar. This affects the tests only.
- Consumer follow-ups:
  - **Broiler.Hosting:** no change is needed. `CreateHighContrastTheme` already maps the roles' sources and sets
    `IsHighContrast`. Its readability test may add `ScrollbarThumbContrast >= 3` for each Windows contrast theme.
    It may also set the two roles explicitly (Window, WindowText) to make the mapping visible.
  - **Broiler.Mail:** no code change. Once Mail is on the first Broiler.UI release that contains this ADR, its
    scroll views take the window text color for the thumb in a contrast theme. These include the form surfaces,
    the status and notice areas, and the message header. The composer body and the reading pane (both
    `StandardRichEdit`) do the same, and in a contrast theme their text wraps 12 DIP short of the bar instead of
    running under it. Screenshots or pixel baselines taken in a contrast theme change where a bar shows and where
    those two wrap. A visual check of the composer and the reading pane in a Windows contrast theme, with text
    long enough to scroll, belongs to the next native acceptance pass. `AppearanceController.Apply` sizes combo boxes from the font with an explicit `PreferredSize`. That
    sizing still works, and the arrow slot follows the font regardless, so Mail's account and settings choices
    keep their arrows clear of the frame at 200 %. ADR 0029 lets Mail drop that sizing. This ADR adds nothing to
    remove.
- Not covered:
  - The Light and Dark presets keep thumbs below 3:1 on their tracks. These are both the translucent bars (1.94
    and 2.30) and the role colors (2.43 and 2.21). Raising them changes a preset and is left as a follow-up. A
    theme can already set the roles.
  - Under Light and Dark the scroll view, rich edit and formatting code view still draw a different scrollbar
    from the list, tree and code editor. Giving every bar one look means choosing which of the two the presets
    keep, or a third. That is a design decision for the preset owners, left as a follow-up.
  - The list, the tree, the code editor's palette and `ScrollbarColors` each make the extremes-of-lightness test
    on their own. Sharing one helper is a refactor left for later.
  - A combo box whose app-set height is shorter than a line of its font draws its text and arrow past its bottom
    edge, as it did before this ADR. The file dialog's boxes (30 and 26 DIP tall) and the font dialog's (28 DIP)
    set their heights and take a text-scaled theme from their dialog, so at 200 % (a 40 DIP line) their text and
    arrow run up to 14 DIP below the frame. Sizing those boxes from the font, as ADR 0029 does for a box without
    a set size, is left as a follow-up.
  - Scrollbars have no hover, pressed or disabled look in any control.
  - A combo box narrower than its arrow slot draws its arrow over its left edge, as it did at the default font.
