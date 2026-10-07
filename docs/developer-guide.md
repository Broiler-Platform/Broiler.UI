# Broiler.UI Developer Guide

This guide is for engineers developing, testing, and maintaining `Broiler.UI`. It documents the architectural principles, repository topology, build and test workflows, package management, and CI/CD release pipeline.

For an introduction to using Broiler.UI in applications, see the [README](../../README.md).

---

## 1. Architectural Principles

Broiler.UI is designed around strict separation of concerns, platform neutrality, and granular modularity:

1. **Platform Neutrality**: UI runtime assemblies never reference platform backends (such as Win32, HWND, COM, Direct2D, OpenGL, or WPF). Rendering delegates exclusively through `Broiler.Graphics` core, and input delegates through `Broiler.Input` abstractions.
2. **One Assembly Per Control Type**: Every control family defines an abstraction project (`Broiler.UI.<Control>`) and a Standard implementation (`Broiler.UI.<Control>.Standard`). Applications reference only the controls they need, keeping dependency closures minimal (see ADR [0001](adr/0001-ui-root-and-per-type-assembly-rule.md)).
3. **Abstraction / Implementation Separation**: An abstraction project never references its implementation or `Broiler.UI.Standard`. Standard implementations depend on their abstraction and on `Broiler.UI.Standard`.
4. **Owner-Drawn Chrome & Breakout**: Top-level secondary windows and dialogs break out into native OS windows by default if supported by the host (`IUiWindowHost`), while retaining owner-drawn window chrome (`IUiWindowChromeHost`) so window appearance remains consistent across platforms (ADRs [0025](adr/0025-host-window-breakout.md), [0026](adr/0026-owner-drawn-window-chrome.md)).
5. **Enforced Topology**: Architecture and boundary rules are verified by unit tests in `Broiler.UI.Tests` and the `check-component-graph.sh` script on every build.

---

## 2. Repository Layout

```text
Broiler.UI/
├── .github/workflows/          # GitHub Actions CI and Publish workflows
├── docs/                       # Architecture Decision Records (ADRs) and roadmap
├── eng/                        # Build scripts and packaging metadata
│   ├── Broiler.Dependencies.props # Central package version pins for Broiler dependencies
│   ├── Broiler.Packaging.props    # Shared NuGet packaging metadata
│   ├── pack.ps1                   # Packs and validates all shipping packages
│   ├── resolve-preview-version.mjs # Resolves the next preview version against NuGet.org
│   ├── run-tests.ps1              # Test runner generating TRX test reports
│   └── verify-feed.ps1            # Validates consumer restore against NuGet.org
├── scripts/
│   └── check-component-graph.sh   # Verifies single project per output assembly
├── src/
│   ├── Foundation/
│   │   ├── Broiler.UI/            # Neutral UI root (session, tree, layout, routing, host contracts)
│   │   └── Broiler.UI.Standard/   # Shared Standard control infrastructure (theming, painting)
│   ├── Abstractions/              # Control contracts grouped by family (Shell, Layout, Content, etc.)
│   ├── Implementations/Standard/  # Standard implementations for each contract
│   ├── Integrations/              # Optional ecosystem integrations (e.g. RichEdit.Rtf)
│   ├── Bundles/                   # Meta-packages (Broiler.UI.All)
│   ├── samples/                   # Platform hosts and galleries (Win32, Linux, RichEdit, WebAssembly)
│   └── tests/                     # xUnit test suites grouped by domain
├── Broiler.UI.slnx             # Solution over all projects
├── Directory.Build.props       # Root build properties and compiler configurations
├── HUMAN_REVIEW.md             # Attributable human sign-off status
├── NuGet.config                # NuGet restore configuration (NuGet.org only)
└── README.md                   # End-user introduction and package list
```

---

## 3. Building and Testing

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.100 or later)
- [Node.js](https://nodejs.org/) (v24 or later, for preview version resolution tests)
- PowerShell 7 (`pwsh`)
- Git

### Build Configurations

`Broiler.UI.slnx` provides six build configurations:
- `Debug` / `Release`: Builds the 60 packable platform-neutral runtime libraries and all test suites. This is the configuration verified in CI.
- `Debug-Windows` / `Release-Windows`: Includes Windows-specific Direct2D sample hosts (`Broiler.UI.Win32.Demo`, `Broiler.UI.RichEdit.Win32.Demo`).
- `Debug-Linux` / `Release-Linux`: Includes the Linux OpenGL sample host (`Broiler.UI.Linux.Demo`).

### Common Commands

```powershell
# Build the neutral solution
dotnet build Broiler.UI.slnx -c Release

# Run the complete test suite
./eng/run-tests.ps1 -Configuration Release

# Run tests directly with dotnet test
dotnet test Broiler.UI.slnx -c Release

# Verify the assembly graph (no duplicate output assemblies)
bash ./scripts/check-component-graph.sh

# Test preview version selection script
node --test eng/resolve-preview-version.test.mjs
```

### Architecture Enforcement Tests

`src/tests/Foundation/Broiler.UI.Tests` contains reflection- and project-walking tests that assert:
- No runtime project references platform-specific assemblies or native libraries.
- No abstraction project references an implementation project.
- No assembly references forbidden namespaces (e.g. Direct2D, Win32, HWND, COM).
- Projects reside in directory paths conforming to the naming rules in ADR [0019](adr/0019-directory-structure-topology.md).

---

## 4. Package Management and Dependencies

### Centralized Dependency Versions

External Broiler dependencies (`Broiler.Graphics`, `Broiler.Input`, `Broiler.Documents`) are defined centrally in [`eng/Broiler.Dependencies.props`](file:///d:/Broiler.UI/eng/Broiler.Dependencies.props):

```xml
<PropertyGroup>
  <BroilerGraphicsPackageVersion>0.1.0-preview.7</BroilerGraphicsPackageVersion>
  <BroilerInputPackageVersion>0.1.0-preview.5</BroilerInputPackageVersion>
  <BroilerDocumentsPackageVersion>0.1.0-preview.21</BroilerDocumentsPackageVersion>
  <BroilerSampleGraphicsPackageVersion>0.1.0-preview.7</BroilerSampleGraphicsPackageVersion>
  <BroilerSampleInputPackageVersion>0.1.0-preview.5</BroilerSampleInputPackageVersion>
</PropertyGroup>
```

Test package versions (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`) are managed across all test projects via [`src/tests/Directory.Build.props`](file:///d:/Broiler.UI/src/tests/Directory.Build.props).

### Feed Configuration

All dependencies are restored directly from **NuGet.org**. `NuGet.config` maps all package patterns to `https://api.nuget.org/v3/index.json`. No personal access tokens or custom credentials are required.

---

## 5. Packaging and Local Validation

Broiler.UI produces 60 NuGet packages on every Release pack:
- 1 neutral root package (`Broiler.UI`)
- 1 shared standard infrastructure package (`Broiler.UI.Standard`)
- 1 bundle meta-package (`Broiler.UI.All`)
- 1 optional integration package (`Broiler.UI.RichEdit.Rtf`)
- 56 control packages (28 pairs of contract + Standard implementation)

### Packaging Process

Run [`eng/pack.ps1`](file:///d:/Broiler.UI/eng/pack.ps1) from PowerShell 7:

```powershell
# Ensure the artifacts directory is clean, then pack:
Remove-Item -Recurse -Force artifacts/*
./eng/pack.ps1 -Configuration Release
```

`pack.ps1` verifies every package for:
1. Exact package version match across all packable projects.
2. Presence of `README.md` and `icon.png` in the package archive.
3. Matching XML documentation file (`lib/net10.0/*.xml`) for IntelliSense.
4. Embedded symbol packages (`.snupkg`) containing SourceLink metadata (excluding dependency-only bundles).
5. Correct internal package dependencies between abstractions and implementations.

### Feed Consumer Verification

To simulate how external applications restore your packages:

```powershell
./eng/verify-feed.ps1 -Target nuget
```

This creates an isolated scratch project referencing every newly built package, configures package source mappings, and executes `dotnet restore` with an isolated package cache against `nuget.org` to guarantee all external dependencies are resolvable.

---

## 6. Continuous Integration & Release Pipeline

Broiler.UI uses GitHub Actions for continuous integration and publishing.

### CI Workflow (`.github/workflows/ci.yml`)

Triggers on push to `main`, pull requests, workflow dispatch, and calls from the Publish workflow.
1. Sets up .NET 10.0.x and Node.js 24.
2. Runs Node.js preview version selection tests (`eng/resolve-preview-version.test.mjs`).
3. Verifies single-assembly-per-name graph (`scripts/check-component-graph.sh`).
4. Builds the solution in `Release` configuration.
5. Runs test suites with `eng/run-tests.ps1` (enforcing nonempty TRX report generation).
6. Uploads test report artifacts.
7. Packs and verifies all 60 packages via `eng/pack.ps1`.
8. Verifies a fresh consumer restore from NuGet.org via `eng/verify-feed.ps1` (the no-push pack dry run).
9. Uploads packages as workflow artifacts.

### Publish Workflow (`.github/workflows/publish.yml`)

Triggers via manual dispatch or push of a `v*` tag:
1. **Version Resolution**: Runs [`eng/resolve-preview-version.mjs`](file:///d:/Broiler.UI/eng/resolve-preview-version.mjs) against `https://api.nuget.org/v3/index.json`. It queries existing versions of all 60 package IDs on NuGet.org and computes the next numerical preview version (e.g. `0.1.0-preview.10`), or validates the specified tag/suffix.
2. **Validation & Packaging**: Invokes `ci.yml` passing the resolved version to build and pack all packages.
3. **Consumer Verification**: Runs `eng/verify-feed.ps1 -Target nuget` to verify package restore.
4. **Push to NuGet.org**: Always pushes (there is no dry-run mode) all `.nupkg` packages to `https://api.nuget.org/v3/index.json` using the `NUGET_TOKEN` secret.

---

## 7. Adding a New Control

When creating a new UI control:
1. **Create Abstraction**: Add `Broiler.UI.<Control>` in `src/Abstractions/<Family>/`. Define interface/base contract (e.g. `IUi<Control>`) and element options. Reference only `Broiler.UI`.
2. **Create Standard Implementation**: Add `Broiler.UI.<Control>.Standard` in `src/Implementations/Standard/<Family>/`. Implement standard rendering, interaction, state transitions, and accessibility semantics. Reference `Broiler.UI.<Control>` and `Broiler.UI.Standard`.
3. **Register in Solution**: Add both projects to `Broiler.UI.slnx`.
4. **Add to Bundle**: Add `<ProjectReference>` for both projects to [`src/Bundles/Broiler.UI.All/Broiler.UI.All.csproj`](file:///d:/Broiler.UI/src/Bundles/Broiler.UI.All/Broiler.UI.All.csproj).
5. **Add Tests**: Add test project `Broiler.UI.<Control>.Tests` in `src/tests/<Family>/`.
6. **Verify Architecture**: Run `scripts/check-component-graph.sh` and `dotnet test` to confirm compliance with architecture tests.
