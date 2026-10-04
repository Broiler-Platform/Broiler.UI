# ADR 0032 - Focus rings on the fill they are drawn on, and focus hand-off from a scroll stop

**Status:** Proposed  
**Date:** 2026-10-05

## Context

Broiler.Mail's adoption of the roadmap integration build (UI 0.1.0-preview.18 with ADRs 0028 to 0031,
Hosting 0.1.0-preview.7) left two defects that start in Broiler.UI.

- **Rings that vanish on their own fill.** `StandardButton` and `StandardToggleButton` stroke their focus
  ring 2 DIP inside their bounds, on their fill, in `FocusRing`. Which fill that is depends on the state:
  the surface at rest, the state fill when a secondary button is hovered (ADR 0029), the accent on a
  default button, the state fill on a checked, indeterminate or pressed toggle button. The ring took no
  account of it. Broiler.Hosting's system contrast palette (`WindowsTheme.CreateHighContrastTheme`) maps
  `Accent`, `AccentSoft`, `StateFill` and `FocusRing` to the one Highlight color, so in every Windows 11
  contrast theme the ring was drawn in Highlight on Highlight (1:1): on a default button (Send, Save
  account), on a hovered secondary button (Mail's toolbar buttons and the Cc/Bcc toggle once the pointer
  rests on them) and on a checked toggle button. The presets had the same defect on default buttons:
  Light's ring is its accent (1:1), and Dark's and the high-contrast presets' read at 2.27, 1.87 and
  1.49:1 on theirs. Mail worked around the default buttons with `DefaultButtonFocus`, which sets their
  `FocusRing` to the label color; it could not fix a hovered secondary button, because one `FocusRing`
  value cannot serve its surface and its state fill.
- **A scroll stop that keeps focus after it stops being one.** `StandardScrollView.FocusWhenScrollable`
  (ADR 0028) makes a read-only scroll view a keyboard stop while it has something to scroll and nothing
  inside it can take focus. When a layout ended that while it had focus (its content shrank, the window
  grew, or a control appeared inside it), the view kept focus with `CanFocus` false and drew no ring. The
  keys had nothing to act on, and nothing on screen showed where focus was. Mail could not repair it: the
  stop changes during layout, after any model refresh Mail hooks, and a window resize reaches no Mail
  code at all.

`StandardTabView` (ADR 0031) and `DefaultListItemPresenter` (ADR 0029, in high contrast) already picked
their ring color against the fill under it.

## Decision

### One rule: the ring is picked against the fill it is drawn on

- **`StandardControlPaint.FocusRingColor(ring, fill, label)`** returns `ring` where it stands out from an
  opaque `fill` (3:1, WCAG's minimum for a focus indicator), and otherwise `label`, the color the control
  draws its label in on that fill, which the theme chose to be read there. A fill that is not opaque keeps
  the ring, since what shows through it is unknown.
- **Buttons and toggle buttons** pass the fill and the label they resolve for their current state, the same
  pair they draw: at rest, hovered, pressed, default, checked and indeterminate. The ring is still drawn only
  under keyboard navigation (`IsFocusVisible`), in the same place, 1 DIP thick. A default toggle button's
  2 DIP border is drawn in `FocusRing` as before; it marks the default, not focus.
- **The spin box** draws its focus as its frame, 2 DIP wide, around the field, so the fill it is drawn on is
  the field's fill and the label is the field's text color. A hovered or pressed arrow's fill meets the frame,
  and in a system contrast palette the hover fill is the ring color: the ring would merge with the arrow along
  that edge, and no single ring color reads on both the field and the highlight (white on Aquatic's highlight
  is 1.46:1). While the box has focus, an arrow fill the ring does not stand out from is therefore drawn
  inside the ring, inset by its 2 DIP width and rounded concentrically with the frame, so the field shows
  between them. Where the ring stands out from the arrow fill, the arrow is filled up to the frame as before.
- **One frame in a themed spin box.** The box strips the frame and ring of the edit inside it, so that one frame
  surrounds both halves. `StandardThemeController` themes the tree parent first, so it reached the edit after the
  box and gave them back: every controller-themed spin box drew the edit's own square border (at rest) or ring
  (focused) inside its frame, a second ring that followed no rule, and a line between the field and the arrows.
  The box now strips them again each time it draws, as it already set the edit's background.
- **The tab view** calls the helper for its selected header, with unchanged behavior.
- The rule is not tied to `IsHighContrast`: Light's default button needed it as much as a system palette.

Contrast of the ring before and after (WCAG ratios):

| Palette | Default button: `FocusRing` on `Accent` / `AccentHover` / `AccentPressed` (before) | Drawn now |
|---|---|---|
| Light | 1.00 / 1.23 / 1.71 | `OnAccent` (#FFFFFF): 4.91 / 6.06 / 8.41 |
| Dark | 2.27 / 1.80 / 3.07 | `OnAccent`: 4.74 / 3.76; pressed keeps the ring at 3.07 |
| HighContrastLight | 1.87 / 1.46 / 1.19 | `OnAccent` (#FFFFFF): 11.22 / 14.38 / 17.62 |
| HighContrastDark | 1.49 / 1.29 / 2.33 | `OnAccent` (#000000): 13.11 / 15.12 / 8.40 |

Every other state in the presets keeps `FocusRing`, which already stands out from its fill:

| Preset | `Surface` | `SurfaceAlt` (toggle hover) | `SurfaceDisabled` (pressed secondary) | `StateFill` = `AccentSoft` |
|---|---|---|---|---|
| Light | 4.91 | 4.69 | 4.45 | 4.28 |
| Dark | 7.77 | 6.82 | 6.45 | 6.27 |
| HighContrastLight | 21.00 | 21.00 | 21.00 | 17.71 |
| HighContrastDark | 19.56 | 19.56 | 19.56 | 12.74 |

An unthemed toggle button's fixed light fills keep the ring too (3.94, 4.49 and 4.56:1).

In the palettes Hosting builds, the ring is Highlight wherever Highlight stands out from Window, and the
window text otherwise. On the window color it is kept. On the highlight (a default button, a hovered
secondary button, a checked, indeterminate or pressed toggle button) it is 1:1, and the ring is drawn in the
label color on that fill instead, HighlightText where the palette sets it so (see below); a hovered spin box
arrow is drawn inside the frame instead (above):

| Windows theme | Ring (Highlight) on Window | HighlightText on Highlight |
|---|---|---|
| Aquatic | 11.17 | 7.89 |
| Desert | 7.27 | 7.00 |
| Dusk | 6.80 | 7.33 |
| Night sky | 11.81 | 7.96 |
| Custom, selected text = window text (#000000, #FFFFFF, highlight #1A6FDF, #FFFFFF) | 4.38 | 4.79 (the window text) |
| Custom, selected text = window text, highlight #0000A0 | ring is the window text (Hosting's fallback), 13.93 on the highlight: kept | - |

The label on the highlight is what the palette makes it, so which of these rings show depends on the Hosting
build:

- A default button's label is `OnAccent`, which every Hosting build maps to HighlightText. Its ring shows with
  any of them.
- A hovered secondary button and a checked, indeterminate or pressed toggle button draw their label in
  `StateText`. That is HighlightText only where the palette sets the state pair (`SelectionText`, `StateFill`,
  `StateText`), as Hosting's contrast palette does from its `claude/hc-palette-and-input` change (merged into
  Hosting's `claude/roadmap-integration`, planned as Broiler.Hosting 0.1.0-preview.7). Hosting's main branch
  (380e0e8) sets only `Accent`, `AccentSoft`, `OnAccent` and `FocusRing`, so `StateText` falls back to `Text`, the
  window text. The ring then follows that label and stays as faint as it: 1.46, 1.44, 1.91 and 1.78:1 on the
  highlight for Aquatic, Desert, Dusk and Night sky.

The tests build the palette both ways.

### A scroll stop hands focus on

- Each time a `StandardScrollView` with `FocusWhenScrollable` is drawn (`RenderCore`, after the frame's layout),
  it notes whether it is a stop. When it was one when it was last drawn, is none now, has focus, and is not
  `Focusable` on its own, it posts a hand-off through `UiSession.Dispatcher`.
  - Checking as the view is drawn, not as its layout ends, also catches changes that need no layout: a disabled
    control inside it that is enabled, a child made `Focusable`, and `FocusWhenScrollable` turned off. Turning it
    off now invalidates the view's rendering as well as its semantics, since the ring follows the stop.
- When the post runs it checks again: the view must still have focus and still be no stop. Then focus moves
  to the next tab stop after the view in the focus scope, or to the previous one when none follows. The
  scope, the order and the stops are those of `StandardFocusScope.MoveFocus`: the active modal element or every
  root, `TabIndex` then document order, visible, focusable tab stops not hidden from accessibility.
  - When a control inside the view became focusable, it comes next in document order and takes focus.
  - When no stop exists either way, focus stays.
- **The new focus is scrolled into view only when none of it shows.** When the stop's visible bounds are empty
  (it lies in a part of a scroll view that is scrolled away), it is brought into view the way Tab brings a stop
  into view (`UiElement.BringIntoView`). Its own scroll-view ancestors scroll just far enough, including one that
  also holds the view focus came from. No other scroll view moves.
  - Otherwise focus would land where no ring can be seen, the defect this hand-off removes.
  - A stop that shows in part stays where it is. Its ring shows where focus went, and the hand-off follows a
    change of layout, not the user's navigation, so it must not move a form or a list the user is reading.
- **Nothing is taken from elsewhere.** Focus that is not on the view when it is drawn, or that moved
  before the post ran, is left alone. A view that was never a stop keeps the focus a click on its blank space
  gave it (ADR 0028).
- **A hidden view waits.** A view hidden while it has focus is left to what hid it, as other hidden focused
  elements are. It is not drawn while hidden and keeps the note it made when it was last drawn, so when it is
  shown again as no stop, it hands focus on then.
- **Why posted.** ADR 0005 queues mutations that happen during layout to safe points. A focus change raises
  `FocusChanged` to hosts and handlers, which should not run in the middle of a frame. With
  `StandardQueuedUiDispatcher` the hand-off runs at the next drain, before the next frame, so one frame is
  drawn with focus still on the view and no ring. `ImmediateUiDispatcher`, which runs every post at once, hands
  focus on while the view is drawn, before it decides on its ring. A scroll view that the reveal scrolls shows
  its new offset from the next frame.

### `StandardFocusScope.FindAdjacentStop`

- `FindAdjacentStop(from, direction, scopeRoot = null)` returns the tab stop after (+1) or before (-1) `from`
  among the stops `MoveFocus` moves between, in the same scope. `from` need not be a stop: an element that can
  no longer take focus keeps its place by `TabIndex` and document order. It returns null when `from` is not in
  the scope or no stop lies that way, does not wrap, and focuses and scrolls nothing. The scroll view uses it.
  Hosts with their own Tab order, such as Mail's `MailKeyboardNavigation.NextTabStop`, may too.
- `MoveFocus` is unchanged. From an element that is no stop it still starts again at the first (or last) stop.

## Consequences

- Behavior changes:
  - A keyboard-focused default button rings itself in `OnAccent` wherever `FocusRing` has less than 3:1
    against the accent fill drawn: at rest in every preset, hovered in every preset, and pressed in Light and
    both high-contrast presets.
  - In a palette that uses one color for the ring and a fill (Hosting's system contrast palettes), a focused
    default button rings itself in `OnAccent` (HighlightText). A focused secondary button hovered by the
    pointer, and a focused checked, indeterminate or pressed toggle button, ring themselves in the label color
    drawn on that fill: HighlightText where the palette sets the state pair (Hosting 0.1.0-preview.7 as
    planned), and the window text, as faint as before, where it does not (Hosting's main branch).
  - A focused spin box frames itself in the field's text color when `FocusRing` does not stand out from the
    field. While it has focus, a hovered or pressed arrow whose fill the ring does not stand out from is filled
    inside the ring, rounded, rather than up to the frame. Neither happens in the presets.
  - A spin box themed through `StandardThemeController` no longer draws its edit's square border or ring inside
    its frame.
  - The `FocusRing` properties keep the value set or themed; the color drawn is decided at render time.
  - A focused `FocusWhenScrollable` view that stops being a stop hands focus to the next tab stop (or the
    previous one) through the session's dispatcher when it is next drawn, and brings that stop into view when
    none of it shows. This covers its content shrinking, its viewport growing, a control inside it that can take
    focus (added, enabled or made focusable) and `FocusWhenScrollable` turned off. A view hidden while focused
    hands focus on once it is shown again as no stop.
  - Setting `FocusWhenScrollable` invalidates rendering as well as semantics.
- New API: `StandardControlPaint.FocusRingColor` and `StandardFocusScope.FindAdjacentStop`.
- Unchanged: secondary buttons, toggle buttons, spin boxes and tab views in the presets render as before; ring
  positions and thicknesses are unchanged everywhere.
- Consumer follow-ups:
  - **Broiler.Mail: remove `DefaultButtonFocus`, with the Broiler.UI bump and not before.** Mail pins
    `BroilerUiVersion` 0.1.0-preview.18 in `Directory.Packages.props`, which does not contain this change; with it
    the workaround is still what makes the rings visible. Remove it in the same change that moves
    `BroilerUiVersion` to the first Broiler.UI release that contains this ADR:
    - Delete `src/Broiler.Mail.Application/Views/DefaultButtonFocus.cs` and the
      `DefaultButtonFocus.KeepRingsVisible(session, tokens)` call in `AppearanceController.Theme`, with the
      sentence about it in that method's summary. The toolkit now draws what it drew: Send, Save account and Save
      settings ring themselves in `OnAccent` (HighlightText in the system palettes) wherever the palette's ring is
      below 3:1 on their fill, and other buttons keep the palette's ring.
    - `AppearanceTests.AKeyboardFocusedDefaultButtonShowsItsRingOnItsOwnFill` reads the drawn ring at
      `Inset(Bounds, 2)` in the rest state and passes without the workaround in all seven cases (`OnAccent` for
      Light, Dark, both high-contrast presets, SystemDusk and SystemDesert; `FocusRing` for RingStandsOut, whose
      black ring reads at 4.28:1 on Light's accent). Its comment should credit the toolkit.
    - The Windows tests read the `FocusRing` property, which is now the palette's ring (Highlight, the fill
      itself), so they fail once the workaround is gone and must read the drawn ring from a rendered frame
      instead (focused, focus visible, the `StrokeRoundedRect` at `Inset(button.Bounds, 2)`):
      `SystemContrastTests.TheWindowTakesTheContrastThemesPaletteAndFollowsAColorChange` (both
      `AssertRingStandsOut((save.FocusRing, save.PrimaryBackground))` calls) and
      `EveryWindowsContrastThemeLeavesTheDefaultButtonsFocusRingVisible` (`Assert.Equal(colors.HighlightText,
      send.FocusRing)` and its `AssertRingStandsOut`; `Assert.Equal(colors.Highlight, check.FocusRing)` still holds).
    - The documents that describe the workaround: in `docs/architecture.md`, the appearance paragraph's sentences
      on `DefaultButtonFocus`, and on the hovered secondary button that "waits for Broiler.UI", say instead that
      the toolkit picks each button's ring against the fill it draws; in `docs/component-reuse-review.md`, the
      `DefaultButtonFocus` row goes, or records that the toolkit took it over.
    - One difference from Mail's rule: Mail chose one ring for all three fills of a default button; the toolkit
      chooses per state. Only Dark's pressed default button differs: #7AB7FF at 3.07:1 on #1B5EAF, where Mail drew
      white at 6.43:1. The native ring pixel check (rest state) gives the same colors as Mail's workaround.
  - **Broiler.Mail: the hovered secondary ring.** Mail's open item "a focused secondary button that the pointer
    hovers draws its ring in Highlight on its Highlight state fill" is resolved by this Broiler.UI release together
    with a Hosting build whose contrast palette sets the state pair (0.1.0-preview.7 as Mail's adoption consumes
    it). With Hosting's main-branch palette the ring follows the window-text label and stays at 1.44 to 1.91:1.
  - **Broiler.Mail: the scroll stops.** 'Status and errors', 'Inbox notice' and 'Message header' now hand focus
    on when they stop being stops while focused, at the next drain of Mail's `StandardQueuedUiDispatcher`, and
    bring the next stop into view when none of it shows. Mail needs no code change.
    `FocusNavigation.KeepFocusUsable` should keep its `Focusable: true` filter, which keeps it from handling those
    views a second time; its comment can say that the toolkit hands their focus on. Mail's open item on this is
    resolved by the release.
  - **Broiler.Hosting: release the state pair with or before this.** The hovered-secondary and toggle-state rings
    in the system contrast palettes need the `claude/hc-palette-and-input` palette; a Broiler.UI release with this
    ADR on a Hosting release without it fixes only the default button.
  - Applications that set a button's `FocusRing` to a color chosen for its fill, as Mail did, keep working: the
    color stands out from that fill and is drawn as set.
- Not covered:
  - Buttons and toggle buttons draw a 1 DIP ring whatever the theme's `FocusRingThickness` (2 DIP in the
    high-contrast presets), unlike the tab view and the scroll view.
  - The rule picks between two colors. If an application's label color reads no better on its fill than the
    ring, the label color is still drawn. An unthemed button is such a case: its default label is white, not
    the shared palette's `OnAccent`, so under a shared palette with a light accent its ring is as faint as its
    label (white on Aquatic's highlight, 1.46:1). Themed buttons take `OnAccent`. A palette whose `StateText`
    reads poorly on its `StateFill` (Hosting's main branch, above) is another.
  - A spin box's 1 DIP border at rest merges with a hovered arrow's fill in the system palettes (the window text
    on the highlight is 1.4 to 1.9:1). It is a border, not focus, and its outer edge still bounds the control.
  - `DefaultListItemPresenter` keeps its own form of the rule, in high contrast only, without the opacity check.
  - `MoveFocus` from an element that is no stop still restarts at the first or last stop rather than continuing
    from the element's place; `FindAdjacentStop` gives hosts that place.
  - The scroll view sees a change when it is next drawn. A change that invalidates nothing, such as setting a
    child's `Focusable`, hands focus on with the host's next frame, whatever causes it.
  - A revealed stop taller than its scroll viewport shows its far edge, as with Tab: `MakeVisible` aligns the
    edge that lies outside. Mail's own `FocusNavigation.Reveal` keeps such a control's top in view instead.
