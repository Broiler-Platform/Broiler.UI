# Component review — 2026-09-13

Reviewed the build and dependency topology, CI/release workflows, session/tree
lifecycle, RichEdit layout, and representative standard controls. This is a
targeted engineering review, not the revision-scoped human approval in
`HUMAN_REVIEW.md`; that file remains unchanged.

## Runtime findings — fixed in the follow-up

All three findings below are now addressed. Root and child removal share
session-owned subtree cleanup, including caret, focus, capture, modal, pointer,
and touch state. Cancelled touch contacts keep no element reference and remain
cancelled until release, including when a handler detaches itself. Session
dispatch also rejects detached or disposed targets.

Already-attached roots must be explicitly removed before insertion as children;
disposed elements are also rejected by `AddRoot`. RichEdit tab and indent setters
invalidate layout and request rendering, and the layout cache now uses a single
settings snapshot including those values. The descriptions below record the
original failure modes.

1. **High: detached children retain session input state.**
   `UiElement.RemoveChild` detaches the subtree without clearing the session's
   focus, capture, modal stack, or touch routes. `UiSession.RemoveRoot` implements
   some of this cleanup separately. A focused, captured child that is disposed
   remains the dispatch target: a local repro confirmed that the next mouse
   event throws `ObjectDisposedException`. Extract a session-owned subtree
   detachment operation and use it for both roots and children, including
   pointer/touch state and host caret cleanup.
   Sources: `src/Foundation/Broiler.UI/Elements/UiElement.cs` (`RemoveChild`),
   `src/Foundation/Broiler.UI/Session/UiSession.cs` (`RemoveRoot`, `DispatchToTarget`).

2. **High: a root can also become a child in the same session.**
   `UiElement.InsertChild` accepts a child with no parent when its session matches
   the parent's session. An existing root satisfies that condition and remains
   in `UiSession.Roots` after insertion. A local repro with two roots confirmed
   that inserting one beneath the other renders it twice in a single frame.
   Reject already-attached children, or provide an explicit reparent operation
   that updates root membership and session state atomically.
   Source: `src/Foundation/Broiler.UI/Elements/UiElement.cs` (`InsertChild`).

3. **Medium: RichEdit layout caching omits mutable layout settings.**
   `TabStopWidth` and `IndentWidth` are automatic properties, while `EnsureLayout`
   caches only document identity, content width, zoom, and font. Changing tab
   spacing after the first render retains old line breaks even when another
   frame is explicitly rendered. A warm editor changed from tab width 8 to 80
   produced different wrapping from a fresh editor at 80 with identical text and
   bounds. Use invalidating setters and one explicit layout-settings snapshot.
   Source: `src/Implementations/Standard/Text/Broiler.UI.RichEdit.Standard/StandardRichEdit.cs`
   (`TabStopWidth`, `IndentWidth`, `EnsureLayout`).

## Refactoring opportunities

- Completed: `StandardRichEdit` (3,110 lines by then) is split into internal
  collaborators that each own one kind of state. `RichEditLayout` owns the
  lines, cells, and list decorations and answers every measurement and hit test
  in content space; `RichEditImageCache` owns backend picture handles;
  `RichEditPainter` paints from an explicit per-frame input; `RichEditScroller`
  owns the scroll offset, thumb drag, and touch-scroll gesture; and
  `RichEditViewport` maps content space into the control. Text shaping, word
  boundaries, and double-click detection are small helpers. The control keeps
  its public surface, input routing, and layout coordination; its public API is
  unchanged. Selection painting and right-click hit testing now share one
  selection-span measurement instead of two copies that had drifted apart.
- Completed: `StandardFileDialog` lists folders through
  `IUiFileDialogDirectoryProvider` (`Broiler.UI.FileDialog`) and never waits for
  it. A late listing shows the folder as loading, a failed one shows its error
  with the way up still offered, and a superseded or abandoned listing is
  cancelled and ignored. Sorting and filtering rearrange the last listing without
  reading the folder again, and navigation no longer re-reads the places.
  `UiFileSystemDirectoryProvider.Synchronous` is the default and lists on the
  calling thread as before. `UiFileSystemDirectoryProvider.Background` lists on
  the thread pool, but its result can only return through a session dispatcher
  that marshals `Post` onto the UI context, which `ImmediateUiDispatcher` does
  not.
- Completed: `StandardQueuedUiDispatcher` (`Broiler.UI.Standard`) is that
  dispatcher. It belongs to the thread that creates it. `Post` queues from any
  thread and never runs anything inline, and it wakes the host once per batch.
  `Drain` runs only on the owner thread and only what was queued when it began,
  so a re-posting callback cannot keep a drain going. A nested drain does
  nothing, and a callback that throws leaves the rest queued and asks for
  another drain. Both Win32 demo hosts wake through
  `Direct2DWindow.PostToUiThread` and also drain before each frame. The Linux
  demo host drains on every tick of its render loop; that loop used to resume on
  arbitrary pool threads, and a small synchronization context now keeps it on the
  dispatcher's thread. No demo hosts a file dialog, and `StandardFileDialog`
  still defaults to the synchronous provider.
- Completed: shared test SDK/xUnit references now live in
  `src/tests/Directory.Build.props`, which imports the component build defaults.
  Broiler dependency pins now live in `Directory.Packages.props`, retaining
  the intentional differences between runtime and sample versions. Projects
  still declare their own dependencies, preserving per-control assembly boundaries.

## CI/CD changes implemented

- Adopted the sibling components' reusable CI, preview-version resolver and its
  tests, verified packaging, and destination-feed consumer restore scripts.
- Publish resolves a single unused preview, calls CI, then publishes its exact
  package artifacts. Inputs/secrets pass through environment variables;
  publishing is serialized across refs, and write permission belongs only to
  the publishing job. Duplicate package versions fail.
- CI now runs Release on a single Ubuntu runner, including graph checks and packaging;
  platform-specific configurations remain available for local sample builds.
  Each test suite must produce a fresh, nonempty TRX report. Reports are uploaded
  even on failure.
- Removed broken submodule initialization and obsolete root metadata. Updated
  architecture tests to assert approved package dependencies and local project
  boundaries instead of requiring the removed external project references.
- Kept the browser source demo in its existing standalone solution, outside the
  main package-based solution. Its backend and replay module still require
  source checkouts; browser CI coverage must be restored when those assets ship.
  Sample-only root properties now live in the sample project.
- Updated build, feed, packaging, and release documentation.

Workflow reuse and concurrency were checked against
[GitHub's workflow documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/reusing-workflow-configurations).

## Validation and limits

- Baseline: Release build failed on the browser asset copy; 9 of 619 tests failed
  on obsolete architecture expectations.
- Updated Release, Release-Windows, and Release-Linux configurations build using
  .NET SDK 10.0.400 on Windows. Linux was cross-built, not executed on a Linux host.
- All 616 tests across 11 suites pass. Four obsolete checkout-root checks were
  replaced by one shipping-project dependency check.
- All 58 packages verified with an explicit `0.1.0-preview.999999` test version,
  including internal version propagation, documentation, icons, and symbols.
- Graph checks passed: 69 projects for Release, 70 for Release-Linux, and 71 for
  Release-Windows, each producing distinct assembly names.
- All 5 preview-version tests pass; actionlint and `git diff --check` pass.
- An isolated nuget.org consumer restore correctly failed because external
  Broiler dependencies are not published there. GitHub consumer restore returned
  401 locally; authenticated validation must run in Actions with feed access.
  Cached dependencies supported local builds, so those builds alone do not
  demonstrate a clean authenticated restore.
- Two existing CS8625 warnings remain in test fixtures. No native GUI/browser
  interaction was tested. No workflow was dispatched and no packages were pushed.

The initial CI/CD change left runtime behavior unchanged. The subsequent runtime
fix adds `UiTreeLifecycleTests` and `StandardRichEditLayoutSettingsTests`, covering
13 cases: root/child removal and disposal, caret and modal cleanup, touch-handler
disposal, reattachment, unrelated contacts, root membership, disposed roots, and
tab/indent reflow against a fresh editor.

Follow-up validation: Release build passes and all 629 tests across 11 suites
pass, including the 13 new regression cases. `git diff --check` passes. The two
existing CS8625 test-fixture warnings remain; native GUI interaction was not run.

Dependency cleanup (2026-09-14): evaluated package references and build properties
match the baseline for all 11 test projects. All 60 projects with direct Broiler
package dependencies retain their exact IDs and resolved versions. Release builds
and all 629 tests pass after centralization. The RichEdit decomposition and
asynchronous file-dialog work remain separate follow-ups.

Refactoring follow-up (2026-09-28): the baseline was 657 passing tests across 11
suites. Release builds with the same 27 test-analyzer warnings as the baseline,
and all 733 tests pass, 76 of them new. Before the RichEdit decomposition, an
uncommitted characterization harness recorded 2,120 lines of render lists,
hit-test sweeps, key navigation, wheel, scrollbar, and touch scrolling, IME,
picture, and context-menu results, plus the assembly's public API. After the
decomposition both were byte-identical. New tests cover each RichEdit
collaborator directly and the file dialog's loading, error, cancellation,
stale-result, restart-on-attach, and background-listing paths, the last with a
queued dispatcher on real thread-pool threads.

Two behavior changes are intended. The picture cache no longer records "no
host yet" as a failed decode; an editor laid out before joining a session (for
example by `SetEditorSelection`) previously kept undecoded pictures as fallback
boxes permanently. The file dialog now shows listing errors instead of an empty
folder, always offers `..`, and no longer checks `Directory.Exists` when a
place is clicked; a missing place reports its error instead.

The component graph check passes (71 projects). `Broiler.UI.RichEdit.Win32.Demo`
did not compile at the baseline: it read `RichTextDocument.Start` through an
instance, and that member is static in Broiler.Documents 0.1.0-preview.21. Both
call sites now use `RichTextDocument.Start`, and that break is fixed.
Release-Windows builds with no errors, including both Win32 demo hosts, and the
Linux demo host builds under Release-Linux. No native GUI interaction was run.

Queued dispatcher (2026-09-28): 10 new tests cover ordering and owner-only
access. They also check that the wake fires once per batch, that a re-posting
callback ends its drain, that a nested drain does nothing, and that a throwing
callback leaves the rest queued. A stress test drains 16,000 posts from eight
threads while they are still being made. The file dialog's background-listing
test now runs on the real dispatcher. Release builds (same 27 test-analyzer
warnings), all 743 tests pass, and the component graph check passes. All three
demo hosts build without warnings.

The Linux demo ran offscreen on Windows with the CPU fallback and rendered its
frame. `--artifact-dir` still fails, independently of this change: saving a PNG
needs a codec catalog the demo never registers with `BImageCodecs.Use`. A scratch
harness ran the demo's synchronization context over 200 timer ticks, with awaits
that complete on pool threads and posts from the pool. The loop never left its
thread, and every post ran there. No native window was opened.
