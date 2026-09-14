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

- Split the 3,029-line `StandardRichEdit` by responsibility: layout and hit
  testing, document painting/image ownership, and input/IME/scrolling. Extract
  behavior into testable collaborators where state can be clearly owned; file
  splitting alone will not reduce the coupling.
- Move file-system enumeration out of `StandardFileDialog`'s synchronous input
  path. `RefreshDirectoryEntries` enumerates and sorts all entries before
  updating controls. Large or network directories can block navigation. A
  cancellable directory provider would also make error/loading states testable.
- Completed: shared test SDK/xUnit references now live in
  `src/tests/Directory.Build.props`, which imports the component build defaults.
  Broiler dependency pins now live in `eng/Broiler.Dependencies.props`, retaining
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
