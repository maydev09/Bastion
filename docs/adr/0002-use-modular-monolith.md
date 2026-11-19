# 0002. Use a modular monolith

- **Status:** Accepted; partly superseded by [0017](0017-module-layers-as-projects.md)
- **Date:** 2026-11-15

## Context

Bastion is a personal headquarters that hosts many unrelated feature areas
(the first two are housing and job offers; later ones need not be scrapers).
It is built and run by one maintainer, so it must be simple to develop, deploy and
operate. At the same time, modules must stay independent: adding, changing or
removing one module must never break another.

## Decision

Bastion is a **modular monolith**: one deployable backend process and one web
frontend, internally split into strictly independent modules.

### Module independence
- Each module is one business capability with its own logic, data, API endpoints,
  background jobs and UI pages.
- A module works completely on its own. The application starts and runs with any
  subset of modules enabled, including only one or none.
- Modules never reference another module's code. The only shared code is the
  platform (module contract, shared services) and technical building blocks
  (e.g. a scraping toolkit). Neither contains business logic of any module.
- If modules ever need to cooperate, they do it only through the other module's
  public contracts (a separate `Contracts` project with interfaces, DTOs and
  integration events). Integration events are handled asynchronously, in separate
  transactions. Domain types and domain events never leave their module.
- Module internals are `internal` by default; only the module's registration entry
  point and its contracts are `public`.

### Data
- Each module owns its own database schema, its own data access context and its own
  migrations.
- No module reads or writes another module's tables. No cross-module joins,
  foreign keys or transactions.

### Enabling and features
- A whole module is enabled or disabled in configuration. A disabled module registers
  nothing: no services, endpoints, background jobs, navigation entries or UI pages.
- Feature flags are a separate, finer mechanism used inside a module to hide
  unfinished or optional functionality (see ADR 0001). The flag mechanism is chosen
  in its own ADR.

### Frontend
- One React application (no micro-frontends); modules are separated by folders, not deployments.
- Each module's UI lives in its own folder (`src/Web/Modules/<Module>`).
- Module folders never import from each other; they use only the shell and the shared UI library.
- The shared UI library (`src/Web/Shared/ui`) holds generic, reusable components
  (buttons, inputs, dropdowns, tables, dialogs, layout) so all modules look and behave
  the same. It is the frontend equivalent of a building block: no business logic,
  no knowledge of any module, never imports from `modules/`.
- A component used by only one module stays in that module. It moves to the shared
  library only when it is generic and a second module needs it.

### Enforcement
- Architecture tests (e.g. NetArchTest or ArchUnitNET) fail the build when a module
  references another module's internals, or when the platform references a module.
- A lint rule enforces the same boundaries in the frontend.

### Layout

The solution and folder layout is defined in ADR 0003, the module contract in ADR 0007.

## Alternatives considered

- **Microservices:** strong isolation, but separate deployments, networking and
  observability are far too much overhead for one maintainer and one machine.
- **Classic layered monolith** (one Domain/Application/Infrastructure for everything):
  simple at first, but unrelated features end up coupled through shared models and
  tables, and a module cannot be switched off cleanly.
- **Separate applications per tool** (the legacy approach): full isolation, but every
  tool repeats the plumbing and there is no single place to manage everything.

## Consequences

- One process, one deployment, one database server: simple to run at home.
- Boundaries are enforced by tests rather than by discipline alone.
- Some code may look duplicated between modules; that is accepted to keep them independent.
  Shared technical code is extracted into a building block only when a second module needs it.
- If a module ever needs to scale or run separately, its clean boundaries allow it to be
  extracted into its own service.
