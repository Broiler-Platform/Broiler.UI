# Broiler.UI

[![CI](https://github.com/Broiler-Platform/Broiler.UI/actions/workflows/ci.yml/badge.svg)](https://github.com/Broiler-Platform/Broiler.UI/actions/workflows/ci.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://github.com/Broiler-Platform/Broiler.UI/blob/main/LICENSE)

Broiler.UI is the platform-neutral retained-mode UI component for Broiler application
chrome and general-purpose widgets. It owns the neutral UI root, the shared Standard
control infrastructure, and one contract/implementation pair per control type — each in
its own assembly, so an application takes only the controls it uses.

Controls draw through the platform-neutral `Broiler.Graphics` core and take input through
the `Broiler.Input` abstractions. No UI runtime assembly references a native backend.

> **Preview release.** `0.1.0-preview.1` is the first published preview. Public APIs and
> behaviour are not frozen and may change before `1.0`. Substantial implementation work
> was AI-assisted, and human-review approval is revision-scoped — consult
> [HUMAN_REVIEW.md](HUMAN_REVIEW.md), which is currently `PENDING`, before describing a
> checkout as approved. See the [roadmap](docs/roadmap.md) for what is still open.

## Installation

Preview packages need an explicit prerelease opt-in:

```bash
dotnet add package Broiler.UI --prerelease
```

`Broiler.UI` is the neutral root: element tree, session, layout, input routing, and host
contracts. It contains no controls. Add the contract package for each control type you
use, plus the matching `.Standard` implementation:

```bash
dotnet add package Broiler.UI.Button.Standard --prerelease
```

An implementation package depends on its own contract package and on
`Broiler.UI.Standard`, so a single `.Standard` reference pulls in everything that control
needs. To take the whole toolkit at once:

```bash
dotnet add package Broiler.UI.All --prerelease
```

### Consuming Broiler packages from GitHub Packages

`NuGet.config` in the repository root pins two sources — nuget.org and the
Broiler-Platform GitHub Packages feed — and clears whatever the machine has configured,
so a restore resolves identically everywhere. Package source mapping sends `Broiler.*` to
either feed and everything else to nuget.org only.

That mapping is load-bearing. GitHub Packages requires authentication **even for public
packages** and answers `401` to an anonymous request, so an unmapped source would be
queried for every package and break the restore. Because this repository takes its
Broiler dependencies through the submodules as project references, nothing queries that
feed today and no credentials are needed to build.

To actually pull `Broiler.*` from GitHub Packages you need a personal access token with
the `read:packages` scope. Put it in your **user-level** config, never in the committed
one:

```bash
dotnet nuget update source broiler-github --username <github-user> --password <pat> --store-password-in-clear-text --configfile "$APPDATA/NuGet/NuGet.Config"
```

In GitHub Actions use `secrets.GITHUB_TOKEN` rather than a personal token.

## Packages

56 packages, all `net10.0`. Every one ships XML documentation and a `.snupkg` symbol
package, and is built deterministically with SourceLink.

| Package | Role |
| --- | --- |
| `Broiler.UI` | Neutral root: element tree, `UiSession`, layout protocol, input routing, host and accessibility contracts. No controls. |
| `Broiler.UI.Standard` | Shared Standard-control infrastructure — theme tokens, visual states, painting and service plumbing. Exposes no concrete control. |
| `Broiler.UI.All` | Meta-package: every contract and its Standard implementation. Dependencies only, no assembly. |

Each control type ships as a contract package and a `.Standard` implementation
(`Broiler.UI.Button` and `Broiler.UI.Button.Standard`, and so on):

| Family | Controls |
| --- | --- |
| Shell | `Window`, `Dialog`, `Tooltip`, `FileDialog`, `FontDialog` |
| Layout | `Panel`, `ScrollView`, `Splitter`, `TabView` |
| Content | `Label`, `ImageView`, `ProgressBar` |
| Commands | `Button`, `ToggleButton`, `Toolbar`, `Menu` |
| Value and selection | `CheckBox`, `RadioButton`, `Slider`, `ListView`, `ComboBox`, `TreeView` |
| Text | `Edit`, `CodeEditor`, `RichEdit`, `FormatCodeView` |

`Broiler.UI.RichEdit.Rtf` sits outside the pairing: it is an optional integration that
adds RTF load and save to `Broiler.UI.RichEdit` through `Broiler.Documents.Rtf`.

The rich-text and formatting-code packages depend on `Broiler.Documents` as well as
`Broiler.Graphics`, so that component has to be on the feed you restore from.

### Dependency direction

```text
Broiler.UI.<Control>.Standard -> Broiler.UI.<Control> -> Broiler.UI -> Broiler.Graphics
                              -> Broiler.UI.Standard  -> Broiler.UI -> Broiler.Input[.Keyboard|.Mouse|.Pen|.Text|.Touch]
```

`Broiler.UI` references only the platform-neutral `Broiler.Graphics` core and the neutral
`Broiler.Input` abstractions. `Broiler.UI.Standard` holds shared infrastructure only and
exposes no public concrete controls; type-specific controls live in their own `.Standard`
assemblies. An abstraction never references an implementation.

## Graphics boundary

Broiler.UI standard controls draw through the platform-neutral `Broiler.Graphics` core.
UI runtime assemblies must not reference `Broiler.Graphics.Windows`, Direct2D, Win32,
WPF, WinForms, COM, HWND, or any other native UI backend. Applications compose the
selected Graphics backend outside Broiler.UI.

This is enforced, not just documented: `Broiler.UI.Tests` walks every project in `src/`
and fails the build on a platform-specific reference, a project in the wrong directory,
an implementation reference from an abstraction, or a native handle on a public surface.

## Repository layout

```text
src/Foundation/                  Broiler.UI and Broiler.UI.Standard
src/Abstractions/<family>/       one contract assembly per control type
src/Implementations/Standard/    one Standard implementation per contract
src/Integrations/                optional host integrations (RichEdit RTF)
src/Bundles/                     the Broiler.UI.All meta-package
src/tests/                       xUnit suites, grouped by family
src/samples/                     Win32, Linux, WebAssembly, and RichEdit sample hosts
eng/                             vendored packaging metadata and package icon
docs/                            roadmap and ADRs
.github/workflows/               CI and publish pipelines
Broiler.Graphics/                submodule; the neutral rendering core
Broiler.Input/                   submodule; keyboard, mouse, pen, text, and touch
Broiler.Documents/               submodule; the rich-text document model, RTF, format codes
Broiler.UI.slnx                  solution over every project in src/
```

Cross-component dependencies are git submodules at the repository root, so every project
reference resolves inside a checkout of this repository. Two of them carry submodules of
their own that this component needs: `Broiler.Graphics` takes `Broiler.Media` for the
image abstraction, and `Broiler.Documents` takes its own `Broiler.Graphics` (which in
turn takes `Broiler.Media`). Initialise those by name rather than recursing — `--recursive`
would walk the `Media -> Graphics` cycle and fetch `Broiler.Documents`' unused
`Broiler.DOM` as well.

That leaves two checkouts of `Broiler.Graphics` on disk. Restore deduplicates them by
package identity and resolves everything against this repository's own checkout, so
exactly one `Broiler.Graphics.dll` reaches any package. Both submodules pin the same
Graphics commit; keep them in step when either is bumped.

## Building and testing

Clone with submodules, or initialise them in an existing checkout:

```bash
git clone --recurse-submodules https://github.com/Broiler-Platform/Broiler.UI.git
```

```bash
git submodule update --init
git -C Broiler.Graphics submodule update --init Broiler.Media
git -C Broiler.Documents submodule update --init Broiler.Graphics
git -C Broiler.Documents/Broiler.Graphics submodule update --init Broiler.Media
```

All four lines are load-bearing. A project reference to a directory that is not there is
only a *warning* during restore; the build then fails on missing types, which is a
confusing way to discover an uninitialised submodule.

The solution defines six configurations. `Debug`/`Release` build every packable assembly
and every test suite. The `-Windows` and `-Linux` variants add the sample host for that
platform and select the matching runtime identifier; they build the same neutral set
otherwise.

```bash
dotnet build Broiler.UI.slnx -c Release
```

```bash
dotnet test Broiler.UI.slnx -c Release
```

Tests are xUnit suites, so `dotnet test` discovers them directly. Alongside the
behavioural suites, `Broiler.UI.Tests`, `Broiler.UI.Standard.Tests`, and
`Broiler.UI.Toolbar.Tests` carry the architecture and topology tests that pin the
repository layout and the exact project-reference set of the foundation assemblies —
they fail if a directory moves without the rules moving with it.

## Samples

```bash
dotnet run --project src/samples/Linux/Broiler.UI.Linux.Demo -c Release-Linux -- --window --input --interactive
```

`Broiler.UI.Linux.Demo` hosts standard controls through `Broiler.Graphics.Linux.OpenGL`
and can bridge first-round keyboard/mouse input from evdev when an X11 window has focus.
Windows-only camera and microphone previews stay outside this Linux pass.

`Broiler.UI.WebAssembly.Demo` builds under every configuration and vendors the canonical
Canvas 2D replay module from `Broiler.Graphics.WebAssembly`.

```bash
dotnet run --project src/samples/RichEdit.Win32/Broiler.UI.RichEdit.Win32.Demo -c Release-Windows
```

`Broiler.UI.RichEdit.Win32.Demo` hosts the rich-text editor on Direct2D and builds under
the `-Windows` configurations.

`Broiler.UI.Win32.Demo` is the one exception: it is **excluded from every configuration**
and does not currently build. Its ADR 0025 break-out host needs secondary-window support
from `Broiler.Graphics.Windows` — an `OwnsMessageLoop` window option plus `Show`, `Close`,
`SetTitle`, and a `Closed` event on `Direct2DWindow` — which that backend does not expose
yet. No package depends on it, so the published set is unaffected, and the exclusion
carries its reason as a comment in `Broiler.UI.slnx`.

## Packaging

Every Broiler.UI package is a plain `net10.0` library, so one pack covers the whole set:

```bash
dotnet pack Broiler.UI.slnx -c Release -o ./artifacts
```

Test and sample projects never pack. `eng/Broiler.Packaging.props` is a vendored copy of
the suite-wide packaging metadata and holds the version, which stays in lockstep across
Broiler components during preview — edit the canonical file and re-run the sync script
rather than editing the copy.

## Continuous integration and releases

`.github/workflows/ci.yml` builds and tests three legs on every push and pull request —
`Release` and `Release-Linux` on Ubuntu, `Release-Windows` on Windows — checking out the
submodules and initialising the nested ones by name, and attaches the packed packages to
each run.

`.github/workflows/publish.yml` publishes. Run it manually to choose a feed (GitHub
Packages or nuget.org); it defaults to a dry run that packs and attaches the packages
without pushing. Pushing a `v*` tag publishes to nuget.org, and the tag must match the
version in `eng/Broiler.Packaging.props`, which stays the source of truth for the suite
version. Publishing to nuget.org needs a `NUGET_API_KEY` repository secret; GitHub
Packages uses the built-in `GITHUB_TOKEN`.

The published packages depend on `Broiler.Graphics`, `Broiler.Input.*`, and — for the
rich-text and formatting-code family — `Broiler.Documents.*`, all at the same suite
version, which have to be on the target feed for them to restore.

## Preview status

This is first-preview software, and the warnings recorded in
[HUMAN_REVIEW.md](HUMAN_REVIEW.md) apply to any published preview:

- The component is preview software and is neither fully optimized nor final.
- Public APIs and behaviour may change while the global refactoring continues.
- Text editing, IME, clipboard, and password/privacy handling have not been reviewed
  against an attributable human sign-off; `HUMAN_REVIEW.md` is `PENDING`.
- Accessibility semantics, keyboard-only operation, and screen-reader behaviour have no
  recorded evidence yet. Do not rely on them for an accessibility conformance claim.
- Rendering and resource ownership under large or adversarial element trees has not been
  fuzzed or load-tested.
- No dedicated fuzzing campaign, SAST report, dependency scan, or independent security
  audit is recorded. This is not a production security audit.

Broiler.UI is an independent Broiler component. It is not part of, maintained by, or
endorsed by HTML Renderer or Yantra JS.

## Documentation

- [Current roadmap](docs/roadmap.md)
- [ADR index](docs/adr/README.md)
- [Human-review record](HUMAN_REVIEW.md)

Completed implementation-phase records are not maintained as current documentation.

## License

Broiler.UI is licensed under the [Apache License 2.0](LICENSE). Third-party material, if
present, retains the license identified with that material. The license provides the
software on an "AS IS" basis, without warranties or conditions.
