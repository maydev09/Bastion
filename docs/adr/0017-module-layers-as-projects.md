# 0017. Module layers as projects

- **Status:** Accepted
- **Date:** 2026-11-17

## Context

ADR 0003 and ADR 0004 put a whole module in one project, with the Clean Architecture
layers as folders and everything `internal`. Architecture tests guarded the dependency
rule between the folders.

Folders do not stop a layer from using another one: a `using` is enough, and the mistake
shows up only when the tests run. Every layer also sees every package of the module, so
Domain could use Dapper or ASP.NET Core without any extra step. As modules grow, we want
the compiler to enforce the layering and each layer to declare its own dependencies.

## Decision

Each module is split into **one project per layer** plus a **root project** that composes them:

```text
src/Modules/Housing/
  Bastion.Modules.Housing/                  root: HousingModule (IModule) only
  Bastion.Modules.Housing.Domain/           entities, enums, business rules
  Bastion.Modules.Housing.Application/      use cases (vertical slices), Abstractions/
  Bastion.Modules.Housing.Infrastructure/   repositories, scrapers, Migrations/
  Bastion.Modules.Housing.Api/              minimal API endpoints
  Bastion.Modules.Housing.Contracts/        public API for other modules, only when needed
```

Project references follow the dependency rule of ADR 0004:

```text
Root           -> Application, Infrastructure, Api
Api            -> Application -> Domain
Infrastructure -> Application -> Domain
```

- Namespaces equal project names (`Bastion.Modules.Housing.Application.ImportOffers`).
- The root project is the module's composition root, like the Host is for the application:
  it registers the services of all layers and maps the endpoints. It holds only the
  `IModule` class. The Host references only root projects.
- Types in layer projects are `public`, because the other layers of the module must reach them.
- The module boundary is guarded by architecture tests on project references:
  a project uses only the layers of its own module that the rule above allows;
  another module only through its `Contracts`; the Host only module roots;
  Domain and Application use no ASP.NET Core, Npgsql, Dapper or building blocks.
- A module creates only the layer projects it has code for. An empty module has a root and,
  for its schema migration, an Infrastructure project.
- Migrations live in `src/Modules/<Module>/Bastion.Modules.<Module>.Infrastructure/Migrations/`;
  the schema is still the module folder in snake_case.

This replaces "one project per module" and "layers as folders" in ADR 0003 and ADR 0004,
"module internals are `internal`" in ADR 0002, and the migrations path in ADR 0007.
The rest of those ADRs stays valid.

## Alternatives considered

- **Keep one project per module with folders** (ADR 0003): fewer projects and `internal`
  protects the module boundary in the compiler, but the layering is only checked by tests
  and every layer sees every package of the module.
- **Layers stay `internal`, opened with `InternalsVisibleTo`** for the module's own projects:
  the compiler would protect the module boundary too, but every new project must be listed
  by hand, and it is an uncommon use of `InternalsVisibleTo` outside tests.
- **Entry point in Infrastructure** (no root project): one project fewer, but Infrastructure
  would then reference Api and mix data access with composing the module.

## Consequences

- The compiler enforces the layering inside a module; each layer lists its own packages.
- Five projects per module with code instead of one; the solution grows with each module.
- Module internals are `public`, so nothing in the compiler stops another module from using
  them once it adds a project reference. The architecture tests catch that reference.
- Adding a module means adding its projects to `Bastion.slnx` and its root to the Host.
