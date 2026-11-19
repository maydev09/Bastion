# AGENTS.md

Operating instructions for coding agents working in this repository. Read this file before every task.

## Project

Bastion is a personal headquarters: a modular platform (C# / .NET backend, React web frontend)
that hosts independent modules behind one web UI. The first two modules (housing,
job offers) replace older private scrapers; later modules need not be scrapers.

## Non-negotiables

1. **Never fabricate.** Do not invent file paths, APIs, command output or test results. Read the file or run the command.
2. **Disagree when the premise is wrong.** Say what is wrong before acting on it.
3. **Stop when genuinely ambiguous.** If two interpretations lead to materially different changes, ask first.
4. **Touch only what the task requires.** No drive-by refactors or formatting sweeps.
5. **Verify before saying done.** Run the build and relevant tests; report failures honestly.
6. **Do not commit or push unless the maintainer explicitly asks.**
7. **Do not break the architecture rules below.** If a task seems to require it, stop and ask.

## Focus on the core problem

Spend effort only on what solves a real problem the maintainer has today
(YAGNI: "you aren't gonna need it").

- Build for one user on one instance, not for a million users. No login screen,
  user management, multi-tenancy, horizontal scaling or "enterprise" infrastructure.
  No user concept in the data model (no `user_id` columns) until a future ADR adds users.
- Before adding anything, ask: does it help collect, browse or act on the data now?
  If not, leave it out and note it as a possible later idea instead.
- When .NET or ASP.NET Core supports something out of the box, use it the way the Microsoft
  documentation shows (options validation, minimal API validation, `IHttpClientFactory`,
  `MapStaticAssets`). Custom code needs a really good reason,
  explained to the maintainer first, and then it extends the built-in concept instead of replacing it.
- Start with plain .NET and simple code. Adopt a bigger tool only when a real need
  appears that the simple solution cannot cover (example: ADR 0009 keeps Quartz.NET
  as the upgrade path for background jobs).
- The UI must still be usable by a person without technical knowledge: simple for one
  user does not mean only understandable by developers.

## Architecture (modular monolith, see docs/adr/0002-use-modular-monolith.md)

- Every module works on its own; the app runs with any subset of modules enabled.
- A module never references another module's code or data. Cooperation, if ever needed,
  goes only through the other module's `Contracts` project.
- Each module owns its database schema and migrations.
  No cross-module joins, foreign keys or transactions.
- The platform never contains module-specific code. Building blocks contain no business logic.
- A whole module is switched on/off in configuration; feature flags work inside a module.
- A module is a root project with only its `IModule` class plus one project per layer
  (docs/adr/0017-module-layers-as-projects.md). The Host references only module roots.
  Layer types are `public`; architecture tests guard the module boundary.
- The UI holds no business logic; everything goes through the API (docs/adr/0011-frontend-stack.md).
- Frontend: a module's UI lives in `src/Web/Modules/<Module>`; module UIs never import from
  each other or from the shell (`src/Web/App`), only from `src/Web/Shared` (`@shared/*`).
  Stack (Vite, React Router, TanStack Query/Table, Base UI + CSS Modules, no Tailwind, API types
  generated from OpenAPI, one container): docs/adr/0011-frontend-stack.md.
  Never edit `src/Web/Shared/api/schema.ts` by hand; regenerate it.
- Audit and logs: log only through `ILogger<T>` (Serilog is configured in the Host only);
  run events via `JobContext` (docs/adr/0013-audit-logging-and-raw-data.md). Every fetched page is stored
  as a raw capture before parsing, through the module's `RawCaptureService`; each portal is an
  `IOfferSource` picked from the `OfferSources` registry, never with a switch (docs/adr/0013-audit-logging-and-raw-data.md).
- Data and API conventions (docs/adr/0014-data-and-api-conventions.md): `int` identity keys,
  `timestamptz` + `DateTimeOffset` + `TimeProvider`, money as `numeric`/`decimal` with a
  currency column, enums as `integer` (explicit C# values) without `CHECK` and as JSON strings, `ProblemDetails` with a
  `code`, paging via `PageRequest`/`PagedResult<T>`, expected errors as `TypedResults`.
- UI texts only from i18next dictionaries by key; no hardcoded texts. Every new or
  changed text goes to both `pl` (default) and `en` in the same change
  (docs/adr/0015-ui-translations.md).
- Tests: NUnit (not xUnit), integration tests first (Host via `WebApplicationFactory`,
  Testcontainers for the database), NSubstitute for dependencies that reach the outside
  world; Vitest in the frontend: docs/adr/0012-testing.md.
- Test behavior that matters, not implementation details: what a user or caller sees
  (an endpoint result, a parsed offer, a refused request, a page that shows the result).
  A refactor or a changed UI text must not break tests. No test per trivial member,
  no assertions on private structure or exact wording; frontend tests find elements by
  role and compare texts through the translation keys.
- Module contract (`IModule`, `ModuleDefinition`, explicit module lists): docs/adr/0007-module-contract.md.
- Solution layout and versions (.NET 10, React 19): docs/adr/0003-solution-structure-and-versions.md.

## Code style (see docs/adr/0004 and 0005)

- KISS: simple, readable code over abstraction. Avoid boilerplate.
  Find the balance: an abstraction or pattern (interface, factory, wrapper) is welcome
  when it makes the code easier to read or change, and unwelcome when it only adds
  layers and indirection. When unsure, start with the plain version and extract later.
- Write the direct version: code must be understandable at first glance. No clever tricks
  such as finding objects by type (`OfType<T>()`), juggling indexes or inserting/removing
  items in lists built by the framework to keep some default order. Prefer a plain `switch`,
  `if` or an explicit list (e.g. configuration is `Sources.Clear()` followed by the sources
  in order). Ordinary tools for their normal job are fine (`IndexOf(':')` to split a line).
  If the direct version is not possible, explain why before writing a workaround.
- One top-level type per file, and the file is named after the type (`ImportResult.cs`).
  Nested types stay inside their parent.
- Names are short but say what the thing is, and never repeat their parent: the hierarchy
  gives the context (`Bastion/bastion_app password`, not `Bastion/Bastion: password of the
  bastion_app role`; `db-app-password`, not `db-app`). This applies to identifiers, files,
  folders, configuration keys and secret-store entries.
- Module layers are projects `Bastion.Modules.<Module>.<Layer>`: Domain -> nothing; Application -> Domain;
  Infrastructure and Api -> Application; the root -> all layers. Namespaces equal project names.
  Create a layer project only when it has code. Use cases are vertical slices in `Application/<UseCase>/`.
- Application never uses SQL, Dapper, Npgsql or ASP.NET Core; it uses interfaces from
  `Application/Abstractions/`, implemented in `Infrastructure/`.
- Data access: Dapper with plain SQL in Infrastructure repositories (`IOfferRepository` -> `OfferRepository`);
  no EF Core, no generic `IRepository<T>`.
  Migrations are `.sql` files run by dbmate (with `migrate:down`), one folder and schema per module.
  Never edit an applied migration. No generated columns (identity keys are fine); the domain calculates derived values.
  The database guards only the data model: types, `NOT NULL`, keys, foreign keys and uniqueness.
  No `CHECK` constraints: rules on values (e.g. price needs a currency) are validated in C#, and code
  that writes an enum column passes the C# enum type, never a bare number.
  An enum has only the values the code uses now; add a value when a feature needs it.
- A domain entity has the same name as its table, and its properties the same names as the
  columns (`housing.offer` -> `Offer`, `area_m2` -> `AreaM2`). A different name or shape needs
  a really good reason; explain it to the maintainer before writing it.
- Handlers are plain classes called by endpoints. No MediatR, CQRS frameworks or AutoMapper.
- No project name in identifiers (`UseConfigurationSources`, not `UseBastionConfiguration`); the
  `Bastion.*` namespaces already give the context.
- Configuration keys are never typed as strings in code: each section has an options
  class with `public const string Section` and code binds through it.
- Private fields are `_camelCase`, never accessed with `this.`; prefer primary constructors.
  Enforced by `.editorconfig` in the build.
- Comments only where the code is hard to follow (scraping details such as where data sits
  in a page, protocol rules, non-obvious workarounds). No XML doc comments that repeat the
  name of a type or member.

## Language

- This is a public repository. All code, identifiers, comments, commit messages,
  pull requests, issues and documentation are written in English.
- Conversation can be in English or Polish; reply in the language of the message.

## Documentation

- Planning documents (read them when a task touches requirements, scope or priorities):
  - `docs/SPEC.md`: what the system must do, non-goals, acceptance criteria.
  - `docs/ROADMAP.md`: phases in order, with exit criteria.
- Open doubts and undecided questions go to `Unresolved questions` in `docs/SPEC.md`
  (one bullet: the question, known options, when it must be decided). When a question is
  answered, remove the bullet and record the answer in an ADR or in the spec text.
- Architecture decisions are recorded as ADRs in `docs/adr/`
  (`NNNN-short-title.md`, based on `docs/adr/template.md`). An ADR is only for a decision that
  is hard to reverse or has real alternatives worth recording; conventions and small rules go
  to `AGENTS.md`. When a decision only refines an existing ADR, propose updating that ADR
  instead of writing a new one.
- Read the relevant ADRs before changing architecture. Propose a new ADR
  instead of silently changing an accepted decision.

## Keeping these files up to date

Update the matching file in the same change that makes it outdated:

| When this changes                                      | Update                         |
|--------------------------------------------------------|--------------------------------|
| How we work: rules, workflow, commands, conventions    | `AGENTS.md`                    |
| What the system does, non-goals, acceptance criteria   | `docs/SPEC.md`                 |
| A decision that is hard to reverse is made or reversed | new ADR in `docs/adr/`         |
| A convention or small rule is added                    | `AGENTS.md`                    |
| A phase starts, finishes or changes scope              | `docs/ROADMAP.md`              |
| A new build, test or run command exists                | `Commands` in `AGENTS.md`      |

- Do not duplicate content: `AGENTS.md` keeps short rules and links to ADRs for the reasons.
- At the end of every task, check this table and say which files you updated,
  or propose the update if it needs the maintainer's decision.

## Git workflow

- Trunk-based development: `master` is the trunk and must always build and pass tests.
- Never commit directly to `master`. Work on short-lived branches cut from `master`:
  `feature-<short-name>`, `fix-<short-name>`, `docs-<short-name>`, `chore-<short-name>`.
- Merge back into `master` through a pull request; delete the branch afterwards.
  The `master` ruleset requires the CI jobs `backend`, `frontend` and `api-types` to pass
  and the branch to be up to date with `master`.
- Unfinished functionality is merged behind a feature flag, never left on a long-lived branch.
- Rationale: docs/adr/0001-use-trunk-based-development.md.
- Keep changes small and focused; one concern per commit.
- Use Conventional Commits (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`).

## Data and privacy

- Never commit secrets, `.env` files or real scraped data. Secrets are Docker secrets read
  from `/run/secrets`, never TOML values or container env vars (docs/adr/0010-secrets.md).
  Test samples contain no personal data (e.g. seller names, phone numbers).
- Scraped portals are named by their real names in code (enums, scraper classes).
- Scraping rules (docs/adr/0016-scraping-rules.md): every request goes through the
  scraping toolkit (robots.txt check, one request at a time per host, backoff on
  429/503). Never store personal data of private persons (raw pages are stored anonymized
  by the portal's `IOfferSource.Anonymize`, docs/adr/0013-audit-logging-and-raw-data.md); store the link to the
  portal's offer or contact page instead. Scrapers never log in to a portal.
- Before writing a scraper for a new portal, research its terms of service, `robots.txt`,
  account requirements, personal data and anti-bot measures, and record the facts in
  `docs/portals/<portal>.md` (checklist in ADR 0016). No review, no scraper.
- Normalize scraped data so offers from different portals can be compared and filtered
  together. Design every scraper, even the first one for a portal, around the module's own
  model, not around the portal's format:
  - each closed set of values (currency, property type, transaction, later city or
    district) is a domain enum or type; portal values are mapped to it only in the parser;
  - values that belong together are one type (price and currency are `Money`);
  - when a field is useful for filtering (e.g. location, rooms, area, extra costs), propose
    storing it in a normalized form instead of leaving it only in the raw capture;
  - an unknown value in a key field skips the offer; in an optional field it leaves the field
    empty. The raw capture is kept, so re-parsing fills it once the parser knows the value.
- Never hardcode user-specific preferences (search criteria, cities, price ranges,
  filters) in code or configuration files. They are data, managed by the user through the UI.
  Code and configuration only hold technical settings (e.g. delays, timeouts, URLs).
- VPN is mandatory for live scraping (see `docs/SPEC.md`, Security and privacy).
  Before any request to a scraped portal (running a live scrape, `curl`, a browser),
  check that traffic goes through a tunnel (`ip route get <host>` shows a `wg*`/`tun*`/`proton*`
  device); if not, stop and ask.
  Never set `Scraping:RequireVpn` to `false` in committed configuration or remove the check.
  Offline runs from sample files need no VPN.

## Commands

- Build: `dotnet build Bastion.slnx`
- Test: `dotnet test Bastion.slnx` (needs Docker: database tests use Testcontainers)
- Database: `scripts/dev-up.sh` starts PostgreSQL and applies all migrations (passwords from
  the local secret store) and stores the backend's connection string in the .NET user secrets. Migrations: `src/Modules/<Module>/Bastion.Modules.<Module>.Infrastructure/Migrations/`,
  `YYYYMMDDHHMMSS_description.sql` with `-- migrate:up` / `-- migrate:down`; schema = module
  folder in snake_case. Never edit an applied migration.
  `scripts/dev-reset-db.sh` deletes the local database and raw captures and recreates the database; it
  destroys all local data, so run it only when the maintainer asks.
- Run: `dotnet run --project src/Host/Bastion.Host` or Rider (http://localhost:5080); in Development the
  connection string comes from the .NET user secrets. Without it the backend stops at startup.
- Frontend (repository root): `npm install`, `npm run dev` (http://localhost:5173, proxies `/api`
  to the backend), `npm run build` (into the Host's `wwwroot`), `npm run lint`, `npm test`.
- API types: `dotnet build` writes `src/Web/Shared/api/openapi.json`; then `npm run api`
  regenerates `src/Web/Shared/api/schema.ts`. Commit both.
- npm packages: `.npmrc` blocks install scripts, saves exact versions and installs only
  versions older than 7 days (`min-release-age`). Never override these flags; CI installs
  with `npm ci` and checks registry signatures. Add a package only when it is really needed.
- Playwright Firefox (needed for live scraping): `scripts/install-playwright-firefox.sh`.
- Server: `scripts/server-setup.sh` (as root, repository cloned to `/opt/bastion`) installs Docker,
  turns on automatic Debian security updates, creates the encrypted secrets and the `bastion`
  systemd service (`deploy/`). Idempotent; run it again after changes.
  The server runs the image CI published for the commit checked out in `/opt/bastion`
  (`ghcr.io/maydev09/bastion:<commit>`): deploy = `git pull` + `systemctl restart bastion`,
  rollback = `git checkout <commit>` + restart. Run Compose there only through `deploy/compose.sh`.
- Image: `docker build -t bastion .` (frontend, backend and Playwright Firefox in one image, ADR 0011).
- CI (`.github/workflows/ci.yml`) runs the backend build, migrations on a fresh database and tests, frontend lint, tests and
  build, and fails when the committed OpenAPI document or `schema.ts` is out of date.
  The `image` job builds the Docker image on pull requests and publishes it on master.
