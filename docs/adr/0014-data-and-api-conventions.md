# 0014. Data and API conventions

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Every module creates tables and endpoints. Some choices touch all of them and are
expensive to change once data and frontend code exist: key types, time and money,
enums, the JSON shape, errors and paging. We fix them once, before the first module.

## Decision

### Database
- **Primary keys:** `integer GENERATED ALWAYS AS IDENTITY`, `int` in C#.
- **Time:** `timestamptz` everywhere, never `timestamp` without time zone.
  PostgreSQL stores the exact instant (normalized to UTC); C# uses `DateTimeOffset`.
  Values read from the database have offset `+00:00`; the UI shows them in the user's
  time zone (`Europe/Warsaw` by default). Code takes the current time from
  `TimeProvider`, never `DateTime.Now`, so time is testable.
- **Money:** `numeric(12,2)` in the database, `decimal` in C#, never `float`/`double`.
  The currency is stored in its own column (ISO code, e.g. `PLN`, `EUR`), because
  sources publish prices and salaries in several currencies.
- **Enums:** stored as `text` with a `CHECK` constraint listing the allowed values,
  not as native PostgreSQL enums (their values cannot be removed or renamed easily).
  The text is the C# enum member name (`'Interested'`).

```sql
status text NOT NULL CHECK (status IN ('Indexed', 'Interested', 'NotInterested'))
```

### API
- JSON uses camelCase property names. Enums are serialized as strings (`"Interested"`,
  not `2`), so the generated TypeScript types (ADR 0011) are string unions.
- **Errors** use `ProblemDetails` (RFC 9457), built into ASP.NET Core. Expected errors
  carry a stable `code` (e.g. `flatOffers.offerNotFound`) that the frontend translates
  (ADR 0015); the `title`/`detail` text is for developers only.
- **Paging:** list endpoints take `?page=1&pageSize=50` (1-based, maximum page size
  enforced) and return `{ items, page, pageSize, totalCount }`. The shared
  `PageRequest` and `PagedResult<T>` types live in `Bastion.Platform.Abstractions`.

### Handlers
- Expected outcomes (not found, invalid input, conflict) are returned as results
  (`TypedResults.NotFound()`, `TypedResults.ValidationProblem(...)`), not thrown.
  Exceptions are only for unexpected failures; the platform turns them into a `500`
  `ProblemDetails` and logs them.
- Validation is plain code at the start of the handler. No validation library.

## Alternatives considered

- **`bigint` or UUID v7 keys:** more headroom or globally unique IDs; `int` is enough
  for a single-user application and easier to read.
- **`timestamp` without time zone / `DateTime`:** ambiguous instants and the classic
  "which time zone is this?" bugs. Keeping the original offset of each value would need
  a second column, and we do not need it.
- **Native PostgreSQL enums:** strict and compact, but painful to change.
- **Exceptions for expected errors:** shorter handlers, but hidden control flow.
- **FluentValidation:** useful for large, reused rules; overkill for small requests.

## Consequences

- Same shapes everywhere: frontend code for errors, paging and enums is written once.
- Changing an enum means a migration that updates the `CHECK` constraint.
- Dapper may need a small type handler to read `timestamptz` as `DateTimeOffset`;
  this is checked when the first store is written.
