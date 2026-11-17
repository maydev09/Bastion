# Bastion roadmap

Bastion grows incrementally: first something small that works end-to-end,
then features are added on top. Every phase ends with a working application.
A phase is complete only when all its exit criteria are met.

## Phase 0: Foundations

- **Status:** Done (2026-11-15)
- **Outcome:** The repository has clear rules for humans and AI, and the decisions
  needed to start coding are recorded.
- **Included work:** AGENTS.md and CLAUDE.md, .gitignore, planning documents,
  ADRs 0001–0015 (architecture, data, configuration, jobs, secrets, frontend,
  testing, audit, conventions, translations).
- **Exit criteria:** Every "Unresolved question" in SPEC.md that blocks Phase 1
  is answered by an accepted ADR.

## Phase 1: Modular skeleton with a scraping smoke test

- **Status:** Done (2026-11-15, PR #3)
- **Outcome:** The modular platform runs end-to-end. Both modules exist in code; the
  housing module can read one live page of offers from OtoDom on demand.
- **Included work:** .NET solution with Host and module contract (ADR 0007); modules
  registered in this order: `Housing`, then `JobOffers` (empty, it only registers);
  React shell with navigation of enabled modules; on the housing page a button
  that fetches one OtoDom results page and shows how many offers were parsed, with
  nothing stored; VPN check before the request; parser tests on a saved sample page;
  architecture tests; GitHub Actions (build, test, lint) required as status checks in
  the existing `master` ruleset.
- **Risks:** Designing the module contract too broadly. Keep it to what this phase needs.
- **Exit criteria:** The smoke test shows parsed offers through a VPN and refuses to run
  without one; disabling a module in configuration removes its pages and endpoints
  and the app still starts; architecture tests and CI pass.

## Phase 2: Database foundations

- **Status:** Done (2026-11-16, PR #5)
- **Outcome:** PostgreSQL runs with separated roles and migrations; the app proves it
  can connect, without storing business data yet.
- **Included work:** PostgreSQL in Docker Compose; separate database roles for
  administration, migrations and the application (ADR 0006); dbmate migrations per
  module schema (ADR 0005); secrets as Docker secrets (ADR 0010); a database smoke
  test in the UI (connected as the application role, schema version); Testcontainers
  setup for tests.
- **Exit criteria:** A fresh setup creates the database, roles and schemas with one
  command; the UI reports a successful connection; the application role cannot
  change the schema; CI runs migrations on a fresh database.

## Phase 3: Storing housing offers

- **Status:** In progress (branch `feature-store-flat-offers`)
- **Outcome:** The housing smoke test becomes a real import: basic offer data is
  saved and listed in the UI.
- **Included work:** Offer table with basic fields, unique per source and external id;
  saving new offers and updating known ones; a simple offer list with paging;
  module layers split into projects (ADR 0017); raw captures of every fetched page with
  re-parsing and retention (ADR 0013); built-in .NET features instead of custom code.
- **Exit criteria:** Running the import twice stores each offer once; the list shows
  the stored offers.

## Later

Planned in detail when Phase 3 is done. Known candidates, in no fixed order:
run history and audit (ADR 0013), search profiles managed in the UI, triage
(statuses, notes, bookmarks), scheduled runs (ADR 0009), more portals per module
with grouping of duplicate offers across portals, the job offers module, and new
modules that need not be scrapers.

Known changes to work built in Phase 3:
- **Raw data retention as a background job** (ADR 0009). Today it runs at the start of every
  import and re-parse, so raw pages with personal data outlive the retention period when
  nothing is imported. It must run on its own schedule, independent of imports.
- **Long operations as background tasks with progress in the UI** (e.g. SignalR), once
  real amounts of data are collected. Today re-parse and import run inside one HTTP request;
  re-parse reads every retained page and saves offers one by one.
