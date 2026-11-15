# 0004. Clean Architecture with vertical slices

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Each module needs an internal structure that keeps business logic independent of the
database and the web framework, while staying simple and fast to work with.
.NET codebases often drift into heavy abstraction and boilerplate (mediator pipelines,
generic repositories, mapping libraries). We want clear layers without that ceremony.

## Decision

Inside each module we use **Clean Architecture layers**, and inside the Application layer
we organize code as **vertical slices** (one folder per use case).

```text
Bastion.Modules.FlatOffers/
  Domain/                  entities, enums, business rules; depends on nothing
  Application/
    ChangeStatus/          one use case: handler + request/response types
    GetOffers/
    Abstractions/          interfaces the use cases need (e.g. IOfferStore)
  Infrastructure/          Dapper SQL stores, migrations, scrapers, external services
  Api/                     minimal API endpoints, one file per use case
  FlatOffersModule.cs      module entry point (registration)
```

Dependency rule:

```text
Api            -> Application -> Domain
Infrastructure -> Application -> Domain
```

- The layers are **folders in one project per module** (ADR 0003), so the whole module
  stays `internal`. Architecture tests enforce the dependency rule between folders.
- Application does not know SQL, Dapper, Npgsql or ASP.NET Core. It talks to the outside
  world through interfaces in `Application/Abstractions/`, implemented in `Infrastructure/`.
- Those interfaces are specific to the module's needs (e.g. `IOfferStore` with the
  methods the use cases call). No generic `IRepository<T>`.
- Handlers are plain classes registered in DI and called directly by the endpoint.
  No MediatR, no CQRS framework, no AutoMapper; mapping is explicit.
- Domain entities get behavior where a real rule exists (e.g. changing a status also
  writes a changelog entry). Tactical DDD patterns (aggregates, value objects, domain
  events, specifications) are not used by default.

Example:

```csharp
// Application/ChangeStatus/ChangeStatusHandler.cs
internal sealed class ChangeStatusHandler(IOfferStore offers)
{
    public async Task<bool> Handle(int offerId, OfferStatus status)
    {
        var offer = await offers.Get(offerId);
        if (offer is null) return false;

        offer.ChangeStatus(status);
        await offers.Save(offer);
        return true;
    }
}

// Api/ChangeStatusEndpoint.cs
internal static class ChangeStatusEndpoint
{
    public record Request(OfferStatus Status);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/offers/{id:int}/status", async (int id, Request request, ChangeStatusHandler handler) =>
            await handler.Handle(id, request.Status) ? TypedResults.NoContent() : TypedResults.NotFound());
}
```

## Alternatives considered

- **Pure vertical slices** (endpoint, logic and SQL in one file): least code, but
  business logic is tied to the database and harder to test in isolation.
- **Separate projects per layer:** compile-time enforcement of the dependency rule, but
  layers must be `public` to reach each other, which weakens module encapsulation,
  and the project count quadruples.
- **Clean Architecture with MediatR:** a common template, but adds indirection and
  boilerplate without solving a problem we have.

## Consequences

- Business logic in Domain and Application can be unit tested without a database.
- Each use case is found in one folder in Application plus one endpoint file in Api.
- One interface per data area is added in Application; this is the accepted cost of the layering.
- A module with genuinely complex rules may adopt richer patterns in its own ADR.
