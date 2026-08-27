# ADR 0026 - Owner-Drawn Window Chrome and Break-Out by Default

**Status:** Approved
**Date:** 2026-08-27

## Context

ADR [0025](0025-host-window-breakout.md) let a logical subwindow break out into its own
native top-level window, but made it **opt-in**: an application had to call
`UiWindow.BreakOut()` explicitly, so an owned window or a dialog stayed a logical
subwindow rendered inside its owner's viewport unless someone asked otherwise. That is
the wrong default for the windows users actually manage. A dialog is a window: it should
be movable onto another monitor, reachable from the taskbar, and orderable by the window
manager, without every call site opting in.

Break-out also exposed a second problem. `StandardDialog` already drew its own title bar
— it had to, because a logical subwindow has no platform frame — and the Win32 host
created the break-out window with the ordinary overlapped frame. A broken-out dialog
therefore showed **two title bars**: the owner-drawn one it kept, and the native one the
window manager added. The main window had the reverse problem: it had a native title bar
and no owner-drawn one, so the same window looked different depending on where it was
hosted, and the framework had no say over its title, icon, or system buttons.

Broiler.UI must stay platform-neutral and never expose native handles (ADR 0002), so it
can neither create a frameless window itself nor drive one.

## Decision

**Break-out is the default.** `UiWindow.BreakOutMode` defaults to
`UiWindowBreakOutMode.Automatic`: an owned window breaks out when it is opened and a
dialog when it is presented, whenever the session host offers `IUiWindowHost`. A host
without the capability is unaffected — the window stays logical, exactly as before.
`UiWindowBreakOutMode.Manual` restores the opt-in behaviour per window, and popups,
menus, and tooltips use it, because a transient overlay positioned against its owner
would flash as a native window and steal activation.

**The UI owns the chrome.** A new optional host capability, `IUiWindowChromeHost`, lets
the framework draw and drive a native window's title bar without touching a handle:

- The host reports `Chrome` (`Owner` when it has suppressed its platform frame,
  `System` when it has not), `IsResizable`, and `WindowState`, and raises
  `WindowStateChanged` when the window manager changes the state behind the framework's
  back.
- The framework calls `SetWindowState`, `SetTitle`, `SetIcon`, `RequestClose`,
  `BeginMoveDrag`, and `BeginResizeDrag`. Moving and resizing are *handed to* the window
  manager rather than simulated from pointer deltas, so snapping, shake, and the drag
  loop keep behaving natively.
- The capability trades only in `string`, `BPixelBuffer`, `UiHostWindowState`,
  `UiWindowEdge`, and events. `UiHostWindowRequest` carries the requested chrome, so a
  broken-out window asks for a frameless host window.

`UiWindow` resolves who draws what through `UiWindowChrome.Auto` (the default):
owner-drawn chrome for a logical subwindow, which the framework renders in full; for a
root window, owner-drawn only when its host reports `UiHostWindowChrome.Owner`. A host
that keeps its platform title bar gets no second one painted underneath it, which is the
rule that removes the double title bar in both directions. `UiWindowChrome.Owner` and
`UiWindowChrome.None` force the decision.

`UiWindow` gains `Icon`, `CanMinimize`, `CanMaximize`, `CanClose`, `Minimize`,
`Maximize`, `Restore`, `ToggleMaximize`, `BeginMoveDrag`, and `BeginResizeDrag`.
`UiWindowChromeLayout` and `UiWindowChromeController` (in the window contract assembly)
lay the bar out, hit-test it, and run its commands, so every control family behaves
identically; the `.Standard` implementations only paint, through
`StandardWindowChromePaint`.

## Consequences

- Broiler.UI still exposes no native handles; the
  `Runtime_Assemblies_Do_Not_Expose_Native_Handles_Or_Windows_Types` architecture test
  continues to hold for the new Foundation types.
- The default changed. An application that wants a dialog inside its owner has to say so
  with `BreakOutMode = UiWindowBreakOutMode.Manual`. Hosts without `IUiWindowHost` see no
  behaviour change at all.
- A window with no chrome-capable host draws no system buttons: minimize and maximize
  have nothing to act on. Close is still drawn for a logical subwindow, which the
  framework can close itself.
- The Win32 host realizes frameless chrome through `BWindowChrome.Owner` on
  `Broiler.Graphics.Windows`, which answers `WM_NCCALCSIZE` with the whole window rect
  and hit-tests the resize border itself. The overlapped style is kept — the caption is
  what makes Aero snap, the drop shadow, and the minimize/restore animations work — and
  only its *drawing* is suppressed.
- `Broiler.UI.Win32.Demo` builds again: the secondary-window support ADR 0025 needed
  (`OwnsMessageLoop`, `Show`, `Close`, `SetTitle`, `Closed`) now exists in
  `Broiler.Graphics`, alongside the chrome, window-state, and drag surface this ADR adds.
