# 0003. Solution structure and platform versions

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

ADR 0002 defines Bastion as a modular monolith. We need a concrete .NET solution
layout that follows established industry practice for modular monoliths in .NET
instead of inventing our own, and we want the newest stable runtime and frontend versions.

## Decision

### Versions
- **.NET 11** (latest stable release; support ends 2028-11-14, the same date as .NET 10 LTS).
  The SDK is pinned in `global.json`.
- **React 19** (latest stable major) with TypeScript.
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
  Platform/Bastion.Platform.Abstractions     module contract and shared service interfaces
  Platform/Bastion.Platform                  shared service implementations
  BuildingBlocks/Bastion.BuildingBlocks.*    technical libraries, created when needed
  Modules/<Module>/Bastion.Modules.<Module>              the whole module
  Modules/<Module>/Bastion.Modules.<Module>.Contracts    public API, only when needed
tests/
  Bastion.ArchitectureTests
  Bastion.Modules.<Module>.Tests
  Bastion.IntegrationTests
web/                                          React app
```

- **One project per module.** Inside it, Clean Architecture layers as folders:
  `Domain/`, `Application/` (vertical slices), `Infrastructure/`, `Api/` (see ADR 0004). Everything is `internal` except the module's entry point class,
  so the compiler protects the module boundary.
- Only the Host references modules. It enables the modules listed in configuration.
- First two modules: `FlatOffers` and `JobOffers`.

## Alternatives considered

- **Several projects per module** (Domain / Application / Infrastructure / Api):
  common in Clean Architecture templates, but layers must then be `public` to reach
  each other, which weakens module encapsulation, and the project count quadruples.
- **One solution per module:** harder to build, test and refactor the whole system together.
- **.NET 10 LTS:** equally long support at this moment, but older; we prefer the newest stable.

## Consequences

- Few projects, fast builds, strong encapsulation through `internal`.
- Layering inside a module relies on folders; architecture tests can enforce it if needed.
- Upgrading to each new major version is a recurring, planned task.
