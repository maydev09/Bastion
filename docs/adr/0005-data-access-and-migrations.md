# 0005. Data access with Dapper and migrations with dbmate

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Modules need to read and write their own data (ADR 0002). Business logic must not
depend on the database (ADR 0004). We want to see exactly which SQL is executed, avoid
ORM "magic" such as change tracking and generated queries, and use tools that are
fully open source without paid editions or restrictive licenses.

## Decision

### Data access
- Data access uses **Dapper** with plain SQL.
- SQL lives only in `Infrastructure/`, in repository classes that implement the interfaces
  defined in `Application/Abstractions/` (e.g. `OfferRepository : IOfferRepository`).
- Repositories have methods named after what the use cases need. No generic `IRepository<T>`.
- A module's SQL only touches its own schema.
- Column names use `snake_case`; Dapper maps them to PascalCase properties
  (`MatchNamesWithUnderscores`).
- Repositories are tested against a real database in a container (Testcontainers), not mocks.

Example:

```csharp
// Infrastructure/Repositories/OfferRepository.cs
internal sealed class OfferRepository(NpgsqlDataSource db) : IOfferRepository
{
    public async Task<Offer?> Get(int id)
    {
        await using var connection = await db.OpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<Offer>(
            "SELECT * FROM housing.offer WHERE id = @id", new { id });
    }
}
```

### Migrations
- Schema and data migrations are plain `.sql` files run by **dbmate** (MIT license).
- Each module has its own migrations folder (`Infrastructure/Migrations/`) and its own
  migrations table, so modules migrate independently.
- Migrations run as a separate step before the application starts (Docker Compose, CI),
  not inside the application. Every migration runs in a transaction.
- Every migration has a `migrate:down` section where reverting is possible.
  `down` is meant for local development; in shared environments mistakes are fixed
  with a new migration.
- An applied migration is never edited.
- Values derived by business rules are calculated by the domain and stored in regular
  columns. No generated columns (identity primary keys are allowed, ADR 0014).
- Changes to existing data (backfills) are part of a regular SQL migration:
  add the column, update existing rows, then add constraints.
- There is no dry-run mode; instead CI applies all migrations to a fresh database
  on every pull request.

Example:

```sql
-- migrate:up
ALTER TABLE housing.offer ADD COLUMN price_per_m2 int;
UPDATE housing.offer SET price_per_m2 = price_base / NULLIF(area, 0);

-- migrate:down
ALTER TABLE housing.offer DROP COLUMN price_per_m2;
```

## Alternatives considered

- **EF Core:** less code for CRUD and built-in migrations, but hidden SQL, change tracking
  pitfalls and more framework behavior to keep in mind.
- **Generic repository over Dapper:** forces every entity into the same CRUD shape;
  use-case-specific repository methods are clearer.
- **Liquibase** (used by the legacy projects): since 5.0 licensed under FSL, which is not
  an OSI open source license.
- **Flyway:** Apache 2.0 and an industry standard, but undo and dry-run are paid features.
- **Atlas:** open-core; key features such as down migrations require the paid edition.
- **DbUp:** MIT, but runs inside the application and has no down migrations.
- **Sqitch:** MIT with revert and verify scripts, but heavier (Perl) and less common.

## Consequences

- Every query and every migration is explicit SQL under our control.
- More hand-written SQL for simple CRUD and updates.
- SQL mistakes surface at runtime, so integration tests on a real database are required.
- dbmate is less widespread than Flyway or Liquibase. Migrations are plain SQL files,
  so switching tools later is cheap.
