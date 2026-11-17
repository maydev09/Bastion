# 0014. Data and API conventions

- **Status:** Accepted
- **Date:** 2026-11-15, updated 2026-11-17 (lookup tables, enums without CHECK, currency enum and Money)

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
  The currency is stored in its own column, because sources publish prices and salaries
  in several currencies. It is a C# `Currency` enum stored as `integer` like other enums,
  so every source maps to the same values; JSON shows the ISO code (`"PLN"`) through
  `[JsonStringEnumMemberName]`. In the domain an amount and its currency are one type,
  `Money(decimal Amount, Currency Currency)`, spread over two columns (`price`, `price_currency`).
- **Enums:** stored as `integer` holding the C# enum value, without a `CHECK`
  constraint and not as native PostgreSQL enums (their values cannot be removed or
  renamed easily). Enum members have explicit values that never change, so a row
  holds e.g. `status = 2`. C# keeps the values valid: code that inserts or updates the
  column passes the enum type, never a bare number.

```sql
status integer NOT NULL
```

- **Lookup tables:** a value list that will carry its own data, such as offer sources
  (portals), is a table with an `id` and a unique `name`, referenced by a foreign key.
  New settings become new columns of that table. Unlike other tables, the `id` is not
  generated: a migration inserts the rows with fixed ids, and the C# enum has the same
  explicit values (`OtoDom = 1`), so code uses the id directly.

```sql
CREATE TABLE housing.source (id integer PRIMARY KEY, name text NOT NULL UNIQUE);
INSERT INTO housing.source (id, name) VALUES (1, 'OtoDom');
source_id integer NOT NULL REFERENCES housing.source (id)
```

### API
- JSON uses camelCase property names. Enums are serialized as the member name,
  not as the number (`2`), so the generated TypeScript types (ADR 0011) are string unions.
- **Errors** use `ProblemDetails` (RFC 9457), built into ASP.NET Core. Expected errors
  carry a stable `code` (e.g. `housing.offerNotFound`) that the frontend translates
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
- **Enums as `text`:** readable in raw SQL, but renaming a member needs a data migration.
- **Exceptions for expected errors:** shorter handlers, but hidden control flow.
- **FluentValidation:** useful for large, reused rules; overkill for small requests.

## Consequences

- Same shapes everywhere: frontend code for errors, paging and enums is written once.
- Adding or renaming an enum member needs no migration; changing a member's value is not allowed.
- Raw SQL results show numbers, not names; the API still returns enum names as strings.
- Adding a source means a new enum member and a migration that inserts its row.
- Dapper may need a small type handler to read `timestamptz` as `DateTimeOffset`;
  this is checked when the first repository is written.
