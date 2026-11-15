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
  (e.g. `flat_offers`, `job_offers`); platform tables live in the `platform` schema.
- Local development runs PostgreSQL in Docker Compose; tests use Testcontainers
  with the same image version.
- PostgreSQL features (e.g. `jsonb`, enums, `timestamptz`) may be used freely.

## Alternatives considered

- **SQL Server:** strong .NET integration, but heavier to run at home and less familiar here.
- **SQLite:** zero setup, but weaker concurrency, typing and schema support for a
  long-running multi-module app.

## Consequences

- One server to run and back up.
- SQL is PostgreSQL-specific; switching databases later would mean rewriting queries.
