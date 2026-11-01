# Browser clipboard reads and context-menu availability

Opening StandardEdit or StandardRichEdit context menus previously called
IUiClipboardHost.TryGetText to decide whether Paste should be enabled. In browser
hosts using BrowserClipboard this starts navigator.clipboard.readText and replays
Paste when it completes. A menu availability probe could therefore open the
browser clipboard UI and insert clipboard text without choosing Paste.

IUiClipboardHost.CanQueryTextSynchronously defaults to true for existing native
hosts. Browser hosts return false. Menu construction then checks only editability
and capacity, leaving Paste enabled until an explicit operation validates the
clipboard. Native hosts retain their existing content-sensitive availability.
Actual Paste and trusted keyboard clipboard events retain their existing paths.

Writer and Cells BrowserCanvasUiHost opt out of synchronous clipboard queries.
Publish the UI cohort (including Broiler.UI, Broiler.UI.Edit.Standard and
Broiler.UI.RichEdit.Standard), update the app UI package references to that
coherent cohort, rebuild/publish the web apps, and deploy the website. Publishing
Hosting alone cannot replace controls already bundled inside a WASM release.

Regression coverage verifies that opening menus with clipboard text performs no
reads and changes no document text; choosing Paste reads once and inserts text.
The browser native contextmenu suppression remains scoped to the app surface.
