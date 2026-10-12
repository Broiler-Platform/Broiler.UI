# ADR 0035 - Printing: a host capability, an owner-drawn print dialog, and a preview that replays pages

**Status:** Proposed  
**Date:** 2026-10-12

## Context

No Broiler application could print. The pieces a print path needs were scattered: the
Documents CLI laid documents out on pages and rasterized them, Broiler.Graphics could rasterize
at any resolution, and the platforms each have a spooler with a different shape - the Windows
spooler and GDI device contexts, CUPS on Linux, and Android's print framework, which brings its
own dialog and preview and asks the document for pages itself. ADR 0018 already required that a
print path need not reference Broiler.UI; the controls here are the part of printing that is UI.

The page itself is not a UI concept. Broiler.Graphics now carries `Broiler.Graphics.Paging`:
paper sizes in points, a page setup, frozen pages (`BPage`) that carry their own image pixels,
page sources, and `IBPagedDocument`, something that can be laid out again for any page setup.
Broiler.Documents.Pagination produces those pages for rich-text documents.

## Decision

1. **Printing is an optional host capability.** `IUiPrintHost` sits beside `IUiClipboardHost`
   and `IUiImageHost` in `Broiler.UI`, found with `Session.Host is IUiPrintHost`. It lists
   printers, reports a printer's capabilities, and prints a `UiPrintJob`. Everything that crosses
   it is platform-neutral: names, `UiPrinterCapabilities`, `UiPrintSettings`, and pages in points.
   The platform implementations live in Broiler.Hosting, as the clipboard's do.
2. **The print dialog is owner-drawn, on every platform that shows one.** `Broiler.UI.PrintDialog`
   and its standard implementation follow the file and font dialogs rather than wrapping
   `PrintDlgEx`: Linux has no print dialog without GTK or a portal, the WebAssembly host has none,
   and an owner-drawn dialog can show the actual pages the job will print. The dialog owns the
   platform-independent decisions - the default printer, fitting the settings to the printer
   (copies, colour, two-sided), re-paginating when the page setup changes - so an implementation
   only draws them.
3. **A platform with its own print UI is not given ours.** A host reports
   `ShowsSystemPrintDialog`, and `UiPrintDialog.PrintAsync` hands it the document without showing
   a Broiler dialog. Android is that host.
4. **The preview is a replay, not a picture.** `Broiler.UI.PrintPreview` draws each page's own
   commands under a scale transform, so it is sharp at any zoom, costs no rasterization, and shows
   exactly what the job sends. Page images go through the session host's `IUiImageHost` once each.
5. **A job prints what was previewed.** `UiPrintJob` carries the pages the dialog laid out, so a
   host does not lay the document out a second time and cannot print something other than what the
   user saw.

## Consequences

- Four packages: `Broiler.UI.PrintPreview` (Content) and `Broiler.UI.PrintDialog` (Shell), each
  with a `.Standard` implementation, all in `Broiler.UI.All`.
- The runtime libraries move to Broiler.Graphics 0.1.0-preview.13, which brings the page model.
- Pagination runs on the UI thread when the page setup changes. That is acceptable for the
  documents Writer opens today; a long document will want it moved off the thread.
- Ctrl+P is still each application's: Broiler.UI has no key-gesture binding, and
  `StandardMenu` neither reads `UiMenuItem.Accelerator` nor invokes `UiMenuItem.Command`.
- Not decided here: page setup as a dialog of its own, printing a selection, and a vector print
  path. The pages are vector already; whether a host rasterizes them or replays them for its
  printer is the host's choice.
