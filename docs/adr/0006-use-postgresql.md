# 0006. Use PostgreSQL

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Modules store their data in their own schemas (ADR 0002) and use plain SQL through
Dapper (ADR 0005), so the SQL dialect is chosen once for the whole system.
Both legacy projects already use PostgreSQL and the maintainer knows it well.

## Decision

- The database is **PostgreSQL 18** (latest stable major), accessed with **Npgsql**.
- One database for the whole application. Each module has its own schema
  (e.g. `housing`, `job_offers`); platform tables live in the `platform` schema.
- Local development runs PostgreSQL in Docker Compose; tests use Testcontainers
  with the same image version.
- PostgreSQL features (e.g. `jsonb`, `timestamptz`) may be used freely; enums are
  stored as `text` with `CHECK`, not native enums (ADR 0014).

### Roles (least privilege)
- `postgres` (superuser): only for the initial setup of the database and roles.
  Never used by the application or by migrations.
- `bastion_owner`: owns the database objects and the schemas; dbmate runs migrations
  as this role. It cannot log in from the application.
- `bastion_app`: the application's role. Only `SELECT`, `INSERT`, `UPDATE`, `DELETE`
  on tables and `USAGE` on sequences in the module and platform schemas. No `CREATE`
  on any schema, `public` included, so the application cannot change the schema.
  Default privileges make new tables accessible to it automatically.
- One application role for all modules keeps setup simple; module data separation is
  enforced by schemas and architecture tests (ADR 0002).
- Each role has its own password, delivered as a Docker secret (ADR 0010).

## Alternatives considered

- **SQL Server:** strong .NET integration, but heavier to run at home and less familiar here.
- **SQLite:** zero setup, but weaker concurrency, typing and schema support for a
  long-running multi-module app.

## Consequences

- One server to run and back up.
- A bug or an injected query in the application cannot drop or alter tables.
- SQL is PostgreSQL-specific; switching databases later would mean rewriting queries.
