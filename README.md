# Broiler.UI

[![CI](https://github.com/Broiler-Platform/Broiler.UI/actions/workflows/ci.yml/badge.svg)](https://github.com/Broiler-Platform/Broiler.UI/actions/workflows/ci.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://github.com/Broiler-Platform/Broiler.UI/blob/main/LICENSE)
[![NuGet](https://img.shields.io/nuget/vpre/Broiler.UI.svg)](https://www.nuget.org/packages/Broiler.UI)

Broiler.UI is the platform-neutral retained-mode UI component for Broiler application
chrome and general-purpose widgets. It owns the neutral UI root, the shared Standard
control infrastructure, and one contract/implementation pair per control type — each in
its own assembly, so an application takes only the controls it uses.

Controls draw through the platform-neutral `Broiler.Graphics` core and receive input through
the `Broiler.Input` abstractions. No UI runtime assembly references a native backend.

> **Preview release.** `0.1.0-preview.1` is the first published preview. Public APIs and
> behaviour are not frozen and may change before `1.0`. Substantial implementation work
> was AI-assisted, and human-review approval is revision-scoped — consult
> [HUMAN_REVIEW.md](HUMAN_REVIEW.md), which is currently `PENDING`, before describing a
> checkout as approved. See the [roadmap](docs/roadmap.md) for what is still open.

## Installation

All Broiler.UI packages and external dependencies are published directly to [NuGet.org](https://www.nuget.org/packages?q=Broiler.UI). Preview packages require an explicit prerelease flag:

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

### Package Restore from NuGet.org

Every Broiler component — including `Broiler.Graphics`, `Broiler.Input`, `Broiler.Documents`, and all `Broiler.UI.*` packages — resolves from **NuGet.org**. No personal access tokens, GitHub Packages credentials, or private package feeds are required. The repository's [`NuGet.config`](NuGet.config) configures `https://api.nuget.org/v3/index.json` as the exclusive package source with package source mapping.

## Quick Start

Creating and displaying standard controls with Broiler.UI:

```csharp
using Broiler.UI;
using Broiler.UI.Button.Standard;
using Broiler.UI.Label.Standard;
using Broiler.UI.Panel.Standard;
using Broiler.UI.Window.Standard;

// Create a window with owner-drawn chrome
var window = new StandardWindow
{
    Title = "Broiler Application",
    CanMinimize = true,
    CanMaximize = true
};

// Compose controls inside a panel
var panel = new StandardPanel();

var label = new StandardLabel
{
    Text = "Welcome to Broiler.UI!"
};

var button = new StandardButton
{
    Text = "Click Me"
};
button.Clicked += (sender, args) =>
{
    label.Text = "Button clicked!";
};

panel.AddChild(label);
panel.AddChild(button);
window.Content = panel;
```

## Packages

60 packages, all targeting `net10.0`. Every package includes XML documentation, embedded symbol packages (`.snupkg`) with SourceLink support, and deterministic builds.

| Package | Role |
| --- | --- |
| `Broiler.UI` | Neutral root: element tree, `UiSession`, layout protocol, input routing, host and accessibility contracts. No controls. |
| `Broiler.UI.Standard` | Shared Standard-control infrastructure — theme tokens, visual states, painting and service plumbing. Exposes no concrete control. |
| `Broiler.UI.All` | Meta-package: every contract and its Standard implementation. Dependencies only, no assembly. |

Each control type ships as a contract package and a `.Standard` implementation
(`Broiler.UI.Button` and `Broiler.UI.Button.Standard`, and so on):

| Family | Controls |
| --- | --- |
| Shell | `Window`, `Dialog`, `AboutDialog`, `Tooltip`, `FileDialog`, `FontDialog` |
| Layout | `Panel`, `ScrollView`, `Splitter`, `TabView` |
| Content | `Label`, `ImageView`, `ProgressBar` |
| Commands | `Button`, `ToggleButton`, `Toolbar`, `Menu` |
| Value and selection | `CheckBox`, `RadioButton`, `Slider`, `SpinBox`, `ListView`, `ComboBox`, `TreeView` |
| Text | `Edit`, `CodeEditor`, `RichEdit`, `FormatCodeView` |

`Broiler.UI.RichEdit.Rtf` sits outside the pairing: it is an optional integration that
adds RTF load and save to `Broiler.UI.RichEdit` through `Broiler.Documents.Rtf`.

### Dependency Direction

```text
Broiler.UI.<Control>.Standard -> Broiler.UI.<Control> -> Broiler.UI -> Broiler.Graphics
                              -> Broiler.UI.Standard  -> Broiler.UI -> Broiler.Input[.Keyboard|.Mouse|.Pen|.Text|.Touch]
```

`Broiler.UI` references only the platform-neutral `Broiler.Graphics` core and the neutral
`Broiler.Input` abstractions. `Broiler.UI.Standard` holds shared infrastructure only and
exposes no public concrete controls; type-specific controls live in their own `.Standard`
assemblies. An abstraction never references an implementation.

## Graphics Boundary

Broiler.UI standard controls draw through the platform-neutral `Broiler.Graphics` core.
UI runtime assemblies must not reference `Broiler.Graphics.Windows`, Direct2D, Win32,
WPF, WinForms, COM, HWND, or any other native UI backend. Applications compose the
selected Graphics backend outside Broiler.UI.

This is enforced by architecture tests: `Broiler.UI.Tests` walks every project in `src/`
and fails the build on a platform-specific reference, a project in the wrong directory,
an implementation reference from an abstraction, or a native handle on a public surface.

## Windows, Dialogs, and Chrome

### About Dialog

`StandardAboutDialog` (in `Broiler.UI.AboutDialog.Standard`) displays application metadata
and a scrollable list of loaded Broiler component versions. The galleries open it from
**Help → About controls**. OK or Enter accepts; Escape cancels; the title-bar close button
closes the dialog.

```csharp
var about = new StandardAboutDialog();
about.ProductName = "My Application";          // optional override
await about.ShowModal(mainWindow);
```

The product name and version default to the entry assembly. Component versions are a
snapshot of loaded `Broiler.*` assemblies, using informational version (including prerelease
labels), then file version, then assembly version.

### Secondary Window Break-Out

An owned window or a dialog **breaks out into its own native top-level window by
default** — it is a real OS window the user can move onto another monitor and manage from
the taskbar (ADR [0025](docs/adr/0025-host-window-breakout.md),
[0026](docs/adr/0026-owner-drawn-window-chrome.md)):

```csharp
var dialog = new StandardDialog { Title = "Options" };
await dialog.ShowModal(mainWindow);            // its own OS window where the host allows it
```

Break-out needs the optional `IUiWindowHost` host capability. A host that does not
implement it is unaffected: the window stays a logical subwindow rendered inside its
owner, exactly as before. Per window, `BreakOutMode` opts back out:

```csharp
var inspector = new StandardDialog { BreakOutMode = UiWindowBreakOutMode.Manual };
```

Popups, menus, and tooltips never break out automatically.

### Owner-Drawn Chrome

Broiler.UI draws the title bar itself — title, icon, and the minimize, maximize, and
close buttons — so a window looks the same wherever it is hosted and a broken-out window
never ends up with two stacked title bars:

```csharp
window.Title = "Broiler";
window.Icon = new UiWindowIcon(iconHandle, iconPixels);   // pixels are for the taskbar icon
window.CanMinimize = true;
```

Who actually draws the frame is resolved per host through `UiWindow.Chrome`, which
defaults to `UiWindowChrome.Auto`: owner-drawn for a logical subwindow, and for a
top-level window only when its host reports `UiHostWindowChrome.Owner` from the optional
`IUiWindowChromeHost` capability. A host that keeps its platform title bar gets no second
one painted underneath. `UiWindowChrome.Owner` and `UiWindowChrome.None` force it either
way.

Moves and resizes are delegated to the host platform window manager via `BeginMoveDrag`
and `BeginResizeDrag`, preserving native window snapping and drag dynamics.

## Rich Text, Code Editing, and Formatting

### StandardRichEdit

`StandardRichEdit` provides a full-featured, flow-based rich text editor backed by `Broiler.Documents.Model`:
- **Rich Formatting**: Font families, font sizes, bold, italic, underline, strikethrough, foreground and background colors.
- **Paragraphs & Lists**: Text alignment (left, center, right, justify), paragraph indentation, custom tab stops, bulleted lists, and numbered lists.
- **Tables**: Rich table insertion, column/row manipulation, and nested document formatting.
- **Context Menu**: Full context menu with Cut, Copy, Paste, character formatting, paragraph styles, and list options.
- **Images**: Seamless rendering of pictures from both encoded bytes and pre-decoded raw pixel samples (`BPixelBuffer` via `IUiImageHost.CreateImage`), with cropping and mask application.
- **RTF Support**: Optional RTF file import and export via the `Broiler.UI.RichEdit.Rtf` integration package.

### CodeEditor & Formatting Codes

- `StandardCodeEditor`: High-performance source code editor with line numbering, virtualized scrolling, and syntax tokens.
- `StandardFormatCodeView`: Visual projection of document formatting codes side-by-side with document models, enabling precise inspection and debugging of styling runs.

## Repository Layout

```text
src/Foundation/                  Broiler.UI and Broiler.UI.Standard
src/Abstractions/<family>/       One contract assembly per control type
src/Implementations/Standard/    One Standard implementation per contract
src/Integrations/                Optional host integrations (RichEdit RTF)
src/Bundles/                     The Broiler.UI.All meta-package
src/tests/                       xUnit suites, grouped by family
src/samples/                     Win32, Linux, WebAssembly, and RichEdit sample hosts
eng/                             Vendored packaging metadata, tools, and build props
docs/                            Developer guide, roadmap, and ADRs
.github/workflows/               CI and publish pipelines
Broiler.UI.slnx                  Solution over every project in src/
```

`eng/Broiler.Dependencies.props` holds centralized version pins for external Broiler dependencies. Shared test SDK and xUnit references live in `src/tests/Directory.Build.props`.

## Building and Testing

Clone the repository and build with the .NET 10 SDK:

```bash
git clone https://github.com/Broiler-Platform/Broiler.UI.git
cd Broiler.UI
dotnet build Broiler.UI.slnx -c Release
```

Run tests using the PowerShell test runner or `dotnet test`:

```powershell
./eng/run-tests.ps1 -Configuration Release
```

Alongside the functional tests, `Broiler.UI.Tests`, `Broiler.UI.Standard.Tests`, and
`Broiler.UI.Toolbar.Tests` execute architecture and topology enforcement tests that
validate directory structure, forbidden dependencies, and project boundaries.

## Samples

Run sample hosts across platforms:

```bash
# Win32 control gallery with owner-drawn chrome
dotnet run --project src/samples/Win32/Broiler.UI.Win32.Demo -c Release-Windows

# Win32 RichEdit editor sample
dotnet run --project src/samples/RichEdit.Win32/Broiler.UI.RichEdit.Win32.Demo -c Release-Windows

# Linux OpenGL sample
dotnet run --project src/samples/Linux/Broiler.UI.Linux.Demo -c Release-Linux -- --window --input --interactive
```

## Packaging

Every Broiler.UI library packs into a NuGet package with XML documentation and `.snupkg` symbols. To build, test, and pack:

```powershell
dotnet build Broiler.UI.slnx -c Release
./eng/run-tests.ps1 -Configuration Release
./eng/pack.ps1 -Configuration Release
```

`eng/pack.ps1` verifies package identities, versions, internal dependencies, README, icon, assemblies, XML documentation, and symbol packages.

## Continuous Integration and Releases

- **CI**: Runs on every push to `main` and pull requests. Executes graph checks, builds `Release`, runs all test suites with TRX generation, and packs all 60 NuGet packages.
- **Publishing**: The publish workflow (`publish.yml`) resolves the next preview version against **NuGet.org** using `eng/resolve-preview-version.mjs`, validates consumer restore using `eng/verify-feed.ps1 -Target nuget`, and pushes packages directly to **NuGet.org** using the `NUGET_TOKEN` secret. GitHub Packages is not used.

## Documentation

- [Developer Guide](docs/developer-guide.md): In-depth guide for contributors, covering architecture, testing, and creating controls.
- [Current Roadmap](docs/roadmap.md): Planned work, touch gestures, and release milestones.
- [ADR Index](docs/adr/README.md): Architecture Decision Records (0001–0026).
- [Human-Review Record](HUMAN_REVIEW.md): Revision-scoped human sign-off status.

## License

Broiler.UI is licensed under the [Apache License 2.0](LICENSE).
