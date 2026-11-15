# 0007. Module contract

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

ADR 0002 requires independent modules that are switched on or off by configuration,
and a platform that contains no module-specific code. We need the smallest contract
that lets the platform host a module in the backend and show it in the frontend.

## Decision

### Backend

Every module has one public class implementing `IModule` from `Bastion.Platform.Abstractions`:

```csharp
public interface IModule
{
    string Name { get; }          // "FlatOffers": configuration section and identifier
    string RoutePrefix { get; }   // "flat-offers": URL segment for API and UI routes
    IReadOnlyList<string> DependsOn => [];  // names of modules whose Contracts it uses

    void AddServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

- The Host keeps an explicit list of all modules (no assembly scanning):

```csharp
IModule[] modules = [new FlatOffersModule(), new JobOffersModule()];
```

- A module is enabled with `Modules:<Name>:Enabled` in configuration. For each enabled
  module the Host calls `AddServices` and `MapEndpoints`; a disabled module is never called.
- At startup the Host checks `DependsOn`: if an enabled module depends on a disabled
  or unknown module, the application fails to start with a clear error.
- `AddServices` receives only the module's own configuration section (`Modules:<Name>`).
- `MapEndpoints` receives a route group already prefixed with `/api/<RoutePrefix>`,
  so a module cannot register routes outside its own prefix.
- Cross-cutting concerns are applied by the platform to every module's route group,
  so modules do not have to remember them: OpenAPI tag with the module name, logging
  scope with the module name, and authentication once a login exists.
- Health checks and background jobs are registered inside `AddServices`
  (background jobs through the platform API defined in [ADR 0009](0009-background-jobs.md)).
- The Host exposes `GET /api/platform/modules` with the list of enabled modules
  (`name`, `routePrefix`).

### Frontend

Every module exports one definition from `src/Modules/<Module>/Web/index.tsx`:

```ts
export const flatOffersModule: ModuleDefinition = {
  name: "FlatOffers",
  routePrefix: "flat-offers",
  namespace: "flatOffers",          // i18next namespace; its "title" key names the module
  locales: { pl, en },
  routes: [{ path: "", element: <FlatOffersPage /> }],
};
```

- The shell keeps an explicit list of all module definitions (the frontend equivalent of the Host list).
- On start the shell calls `/api/platform/modules` and shows navigation and routes
  only for enabled modules.
- Home page summary cards (SPEC) are added to the definition when the first module has
  something to summarize; a card is a normal module component that calls its module's own API.

### Migrations

- A module's migrations live in `src/Modules/<Name>/Bastion.Modules.<Name>/Infrastructure/Migrations/`.
  The migration script finds them by this convention; the application does not take part.
- Migrations of all modules run regardless of whether a module is enabled. Enabling a
  module later then needs no extra step, and empty tables of a disabled module cost nothing.

## Alternatives considered

- **Assembly scanning / plugin loading:** no list to maintain, but implicit and harder to
  follow; modules live in this repository anyway (SPEC non-goals).
- **Lazy-loaded or separately built frontend modules:** disabled modules would not be
  downloaded, but this adds build complexity for no practical gain in a personal app.
- **Richer contract** (module-defined menus, permissions, settings pages): not needed yet;
  the contract grows only when a module needs it.

## Consequences

- Adding a module means one line in the Host list and one line in the shell list.
- The frontend bundle contains code of disabled modules; they are only hidden.
- Module names are written in both the backend and the frontend. An integration test
  compares `/api/platform/modules` with the shell's module list, so a typo fails CI.
- Background jobs and communication between modules are not part of this contract yet;
  they are added in their own ADRs when a module needs them.
- The platform knows modules only through `IModule` and `ModuleDefinition`.
