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
  appears that the simple solution cannot cover (example: Quartz.NET is
  the upgrade path for background jobs).
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
  Modules build screens from the components in `src/Web/Shared/ui/` and never import Base UI or
  TanStack Table themselves; colors and spacing are only the tokens in `src/Web/App/global.css`
  (Noctalia palette, each color a `light-dark()` pair; the theme follows the system unless the
  user picks one in the top bar). A list keeps its view (page, page size, sort order, filters) in the URL
  query, so a copied link opens the same view; the URL keys are the API's query parameters, and sorting,
  paging and filtering happen in the backend.
- Audit and logs: log only through `ILogger<T>` (Serilog is configured in the Host only);
  run events via `JobContext`. Every fetched page is stored
  as a raw capture before parsing, through the module's `RawCaptureService`; each portal is an
  `IOfferSource` picked from the `OfferSources` registry, never with a switch.
- Background jobs: a job is an `IJob` in the
  module's Infrastructure, registered in the module root with `AddJobs(schema, moduleSection).Add<TJob>(name)` (PascalCase name)
  and mapped with `MapJobs(schema)`; the module's migrations create `<schema>.job_run`. An optional
  schedule in `[Modules.<Module>.Jobs.<name>]` is either `DailyAt` (in the time zone from `[Jobs] TimeZone`)
  or an interval `Every`.
  An expected failure throws `JobFailedException` with a code that the UI translates.
- Data and API conventions: `int` identity keys,
  `timestamptz` + `DateTimeOffset` + `TimeProvider`, money as `numeric`/`decimal` with a
  currency column, enums as `integer` (explicit C# values) without `CHECK` and as JSON strings, `ProblemDetails` with a
  `code`, paging via `PageRequest`/`PagedResult<T>`, expected errors as `TypedResults`.
- UI texts only from i18next dictionaries by key; no hardcoded texts. Every new or
  changed text goes to both `pl` (default) and `en` in the same change
 .
- Tests: NUnit (not xUnit), integration tests first (Host via `WebApplicationFactory`,
  Testcontainers for the database), NSubstitute for dependencies that reach the outside
  world; Vitest in the frontend: docs/adr/0012-testing.md.
- Test behavior that matters, not implementation details: what a user or caller sees
  (an endpoint result, a parsed offer, a refused request, a page that shows the result).
  A refactor or a changed UI text must not break tests. No test per trivial member,
  no assertions on private structure or exact wording; frontend tests find elements by
  role and compare texts through the translation keys.

## Code style (see docs/adr/0005-data-access-and-migrations.md)

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
- Method names start with a verb that says what they do (Clean Code): `BuildChangeMap`, not
  `Changes`; a bool method asks a question (`IsRemoved`, `AreEqual`). Nouns are for types,
  properties and variables.
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

- Planning document (read it when a task touches requirements, scope or priorities):
  `docs/SPEC.md`: what the system must do, non-goals, acceptance criteria.
- Open doubts and undecided questions go to `Unresolved questions` in `docs/SPEC.md`
  (one bullet: the question, known options, when it must be decided). When a question is
  answered, remove the bullet and record the answer in an ADR or in the spec text.
- ADRs in `docs/adr/` (`NNNN-short-title.md`, based on `docs/adr/template.md`) record only
  foundational decisions: hard to reverse and independent of what a module does (workflow,
  architecture, data access, configuration, secrets, frontend, testing, infrastructure).
  While a module is still being shaped, its decisions are not recorded as ADRs: rules go to
  `AGENTS.md`, scope to `docs/SPEC.md`; module ADRs come once the module
  is mature. Conventions and small rules go to `AGENTS.md`. When a decision only refines an
  existing ADR, propose updating that ADR instead of writing a new one.
- Read the relevant ADRs before changing architecture. Propose a new ADR
  instead of silently changing an accepted decision.

## Keeping these files up to date

Update the matching file in the same change that makes it outdated:

| When this changes                                      | Update                         |
|--------------------------------------------------------|--------------------------------|
| How we work: rules, workflow, commands, conventions    | `AGENTS.md`                    |
| What the system does, non-goals, acceptance criteria   | `docs/SPEC.md`                 |
| A foundational decision is made or reversed            | new ADR in `docs/adr/`         |
| A convention or small rule is added                    | `AGENTS.md`                    |
| A new build, test or run command exists                | `Commands` in `AGENTS.md`      |
| Server setup, update, backup or restore steps          | `README.md`, `docs/disaster-recovery.md` |

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
- Scraping rules: every request goes through the
  scraping toolkit (robots.txt check, one request at a time per host, backoff on
  429/503). Never store personal data of private persons (raw pages are stored anonymized
  by the portal's `IOfferSource.Anonymize`); store the link to the
  portal's offer or contact page instead. Scrapers never log in to a portal.
- Before writing a scraper for a new portal, research its terms of service, `robots.txt`,
  account requirements, personal data and anti-bot measures, and record the facts in
  `docs/portals/<portal>.md`. No review, no scraper.
- Normalize scraped data so offers from different portals can be compared and filtered
  together. Design every scraper, even the first one for a portal, around the module's own
  model, not around the portal's format:
  - each closed set of values (currency, property type, transaction, later city or
    district) is a domain enum or type; portal values are mapped to it only in the parser;
  - values that belong together are one type (price and currency are `Money`);
  - when a field is useful for filtering (e.g. location, rooms, area, extra costs), propose
    storing it in a normalized form instead of leaving it only in the raw capture;
  - an unknown value in a key field skips the offer; in an optional field it leaves the field
    empty. The raw capture is kept, so re-parsing fills it once the parser knows the value;
  - data comes in three tiers. Main attributes (they can rule an offer out, e.g. price, area,
    furnished) are typed columns, shown and filtered one by one. Amenities are `Feature` values
    (listed or not, no "no"), grouped by the domain's `FeatureCategory` for every portal.
    Everything else stays in the raw capture until a feature needs it;
  - a yes/no attribute is the `Answer` enum (yes, no, not stated), never a `bool`; a null column
    means not known yet (e.g. details not fetched). What a portal does not give at all is a fact
    about the portal (`docs/portals/<portal>.md`), not a value stored with each offer;
  - a number a seller types by hand (e.g. build year) is checked for a plausible range in the parser;
    an implausible one goes, as written, to the offer's `rejected_values` under the column name
    (`{"build_year": "195014"}`) and clears the field, also a value stored from an earlier page (unlike
    a missing value, which never clears). An empty value is missing, not implausible. The parser never
    guesses a corrected value.
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
  `scripts/dev-restore-prod.sh <user@server>` replaces the local database and raw captures with a
  snapshot of production taken now (all or nothing); run it only when the maintainer asks.
  In Development `[Jobs] Scheduled = false`: jobs run only when started from the UI.
- Run: `dotnet run --project src/Host/Bastion.Host` or Rider (http://localhost:5000); in Development the
  connection string comes from the .NET user secrets. Without it the backend stops at startup.
- Frontend (repository root): `npm install`, `npm run dev` (http://localhost:3000, proxies `/api`
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
  (`ghcr.io/maydev09/bastion:<commit>`): deploy = `sudo deploy/update.sh` (waits for the CI image of the newest
  `master`, backs up, fast-forwards, runs `server-setup.sh`, waits until the app answers), rollback =
  `git checkout <commit>` + restart. Run Compose there only through `deploy/compose.sh`.
- Backups (ADR 0020): `deploy/backup.sh` dumps the database nightly (`bastion-backup.timer`) and on
  demand (`systemctl start bastion-backup`); `scripts/backup-pull.sh <user@server>` copies the dumps to another machine.
- Image: `docker build -t bastion .` (frontend, backend and Playwright Firefox in one image, ADR 0011).
- CI (`.github/workflows/ci.yml`) runs the backend build, migrations on a fresh database and tests, frontend lint, tests and
  build, and fails when the committed OpenAPI document or `schema.ts` is out of date.
  The `image` job builds the Docker image on pull requests and publishes it on master.
