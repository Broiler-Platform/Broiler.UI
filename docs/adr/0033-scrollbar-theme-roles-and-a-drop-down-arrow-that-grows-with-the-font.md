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
colors of their own. A theme does that when it is high contrast (`IsHighContrast`), or when either role differs
from the role it follows. **`StandardControlPaint.ScrollbarColors(theme, track, thumb)`** applies this rule:

- It returns the theme's pair, or the control's own pair. It never mixes them: a theme that sets only the thumb
  gets that thumb on the theme's track, not on the translucent one.
- `ApplyTheme` writes the pair every time, so a theme that says nothing about scrollbars, applied after a contrast
  theme, brings the translucent bars back.
- A control that is built under the shared palette and never themed takes the pair from the shared palette, as
  the list already does.

This is ADR 0029's selection rule applied to scrollbars. Drawing the roles in these three controls under every
theme would change the Light and Dark presets. In Light, for example, the translucent bars composite to a #A1ADBD
thumb on #EAEDF1, while the roles are #8DA0B6 on #F1F4F8. The rule reads the `IsHighContrast` flag. Every preset
and Hosting's system palette set it. The rule does not repeat the luminance fallback that the list and tree use
for their high-contrast cues. A custom palette at the extremes that does not set the flag can set the roles.

Every control that draws scrollbars:

| Control | Bars | Colors | Before | Now |
|---|---|---|---|---|
| `StandardScrollView` | vertical, horizontal, corner | `ScrollbarTrack`, `ScrollbarThumb` | translucent pair; `ApplyTheme` left them | `ScrollbarColors` |
| `StandardRichEdit` | vertical, horizontal | `ScrollbarTrack`, `ScrollbarThumb` | translucent pair; not themed | `ScrollbarColors` |
| `StandardFormatCodeView` | vertical, horizontal | `ScrollbarTrack`, `ScrollbarThumb` | translucent pair; not themed | `ScrollbarColors` |
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

### A drop-down arrow that grows with the font

- The arrow sits in a slot at the right edge that grows with `Font` by the ratio of a line of the font to a
  default line, never less than 1. This is the ratio the box's height already grows by (ADR 0029). The glyph
  starts `18 x` that ratio from the edge, and the text is clipped `22 x` that ratio short of it. At the default
  font this is 18 and 22, and the box draws exactly the commands it drew before. At 200 % it is 36 and 44: the
  "v" ends 20.7 DIP from the edge and the "^" 14.1 DIP, and the text stops 8 DIP before the arrow. The tests
  check that, at 150 % and 200 %, both margins grow at least as much as the font.
- The slot follows the font even when the application sets `PreferredSize`. A box with a fixed size, such as the
  file and font dialogs' boxes or Mail's, still keeps its arrow clear of the frame. At a larger font its text gets
  less width.
- The arrow stays a glyph rather than a drawn chevron. The default rendering is unchanged, and the glyph keeps
  the font's weight and color. The slot is scaled from the line height and does not measure the glyph. A family
  whose "v" is much wider for its line than the default font's comes closer to the frame. A drawn chevron sized
  from the line would remove that dependency, but it changes the default rendering. It is left as an option.

## Consequences

- With the Light and Dark presets, every scrollbar and every combo box draws exactly as before. The tests pin
  both kinds of scrollbar colors in Light, Dark and a scaled Dark, and the combo box's arrow and text clip at the
  default font. In the high-contrast presets, the scroll view, rich edit and formatting code view now draw the
  text color as the thumb on a track of the surface color (21:1). The list, tree and code editor are unchanged.
- `ApplyTheme` on the scroll view, rich edit and formatting code view now writes `ScrollbarTrack` and
  `ScrollbarThumb`, as it writes every other themed color. Before, it left them alone. An application that colors
  those bars sets the colors after theming, or sets the roles on its palette.
- `Broiler.UI.Standard.Tests` now references `Broiler.UI.TreeView.Standard`, `Broiler.UI.RichEdit.Standard` and
  `Broiler.UI.FormatCodeView.Standard`, so that one test can cover every scrollbar. This affects the tests only.
- Consumer follow-ups:
  - **Broiler.Hosting:** no change is needed. `CreateHighContrastTheme` already maps the roles' sources and sets
    `IsHighContrast`. Its readability test may add `ScrollbarThumbContrast >= 3` for each Windows contrast theme.
    It may also set the two roles explicitly (Window, WindowText) to make the mapping visible.
  - **Broiler.Mail:** no code change. Once Mail is on the first Broiler.UI release that contains this ADR, its
    scroll views take the window text color for the thumb in a contrast theme. These include the form surfaces,
    the status and notice areas, and the message header. The composer body and the reading pane (both
    `StandardRichEdit`) do the same. Screenshots or pixel baselines taken in a contrast theme change where a bar
    shows. `AppearanceController.Apply` sizes combo boxes from the font with an explicit `PreferredSize`. That
    sizing still works, and the arrow slot follows the font regardless, so Mail's account and settings choices
    keep their arrows clear of the frame at 200 %. ADR 0029 lets Mail drop that sizing. This ADR adds nothing to
    remove.
- Not covered:
  - The Light and Dark presets keep thumbs below 3:1 on their tracks. These are both the translucent bars (1.94
    and 2.30) and the role colors (2.43 and 2.21). Raising them changes a preset and is left as a follow-up. A
    theme can already set the roles.
  - Scrollbars have no hover, pressed or disabled look in any control.
  - A combo box narrower than its arrow slot draws its arrow over its left edge, as it did at the default font.
