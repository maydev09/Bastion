# 0003. Solution structure and platform versions

- **Status:** Accepted; partly superseded by [0017](0017-module-layers-as-projects.md)
- **Date:** 2026-11-15

## Context

ADR 0002 defines Bastion as a modular monolith. We need a concrete .NET solution
layout that follows established industry practice for modular monoliths in .NET
instead of inventing our own, and we want current, supported runtime and frontend versions.

## Decision

### Versions
- **.NET 10** (LTS, support ends 2028-11-14). The SDK is pinned in `global.json` with
  `rollForward: latestFeature`.
- .NET 11 (released 2026-11-10, same end of support) is not yet in the distribution
  packages of the development machine. We move to it once it is, so tools stay installed
  and updated by the package manager.
- **React 19** (latest stable major) with TypeScript; the rest of the frontend stack
  is in [ADR 0011](0011-frontend-stack.md).
- When a new stable major version is released, we upgrade to it.

### Solution
- One solution file in the XML format: `Bastion.slnx`.
- Shared build settings in `Directory.Build.props` (nullable enabled, warnings as errors, analyzers).
- Central Package Management in `Directory.Packages.props`: every package version is defined once.
- Code style in `.editorconfig`.

### Projects

```text
src/
  Host/Bastion.Host                          ASP.NET Core app; the only composition root
  Web/App                                    React shell: navigation, routes, i18n setup
  Web/Shared                                 shared UI library, typed API client
  Web/Modules/<Module>                       the module's UI
  Platform/Bastion.Platform.Abstractions     module contract and shared service interfaces
  Platform/Bastion.Platform                  shared service implementations
  BuildingBlocks/Bastion.BuildingBlocks.*    technical libraries, created when needed
  Modules/<Module>/Bastion.Modules.<Module>              the whole module
  Modules/<Module>/Bastion.Modules.<Module>.Contracts    public API, only when needed
tests/
  Bastion.ArchitectureTests
  Bastion.Modules.<Module>.Tests
  Bastion.IntegrationTests
```

- **One project per module.** Inside it, Clean Architecture layers as folders:
  `Domain/`, `Application/` (vertical slices), `Infrastructure/`, `Api/` (see ADR 0004). Everything is `internal` except the module's entry point class,
  so the compiler protects the module boundary.
- All frontend code is in `src/Web`: one npm project with its tooling at the repository
  root (`package.json`, `vite.config.ts`); imports use the aliases `@shared/*` and `@modules/*`.
  A module's backend and UI share its name (`src/Modules/Housing`, `src/Web/Modules/Housing`).
- Only the Host references modules. It enables the modules listed in configuration.
- First two modules: `Housing` and `JobOffers`.

## Alternatives considered

- **Several projects per module** (Domain / Application / Infrastructure / Api):
  common in Clean Architecture templates, but layers must then be `public` to reach
  each other, which weakens module encapsulation, and the project count quadruples.
- **Module UI inside the module folder** (`src/Modules/<Module>/Web`): a module would be one
  folder, but frontend tooling needs extra aliases and paths, and the UI is scattered across
  the tree. Module independence comes from lint rules and configuration, not from folders.
- **One solution per module:** harder to build, test and refactor the whole system together.
- **.NET 11 now, installed with `dotnet-install.sh`:** newest, but installed and updated
  by hand outside the package manager, only to save one later upgrade.

## Consequences

- Few projects, fast builds, strong encapsulation through `internal`.
- Layering inside a module relies on folders; architecture tests can enforce it if needed.
- Upgrading to each new major version is a recurring, planned task.
