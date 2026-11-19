# Bastion roadmap

Bastion grows incrementally: first something small that works end-to-end,
then features are added on top. Every phase ends with a working application.
A phase is complete only when all its exit criteria are met.

## Phase 0: Foundations

- **Status:** Done (2026-11-15)
- **Outcome:** The repository has clear rules for humans and AI, and the decisions
  needed to start coding are recorded.
- **Included work:** AGENTS.md and CLAUDE.md, .gitignore, planning documents,
  first decisions on architecture, data, configuration, jobs, secrets, frontend,
  testing, audit, conventions and translations.
- **Exit criteria:** Every "Unresolved question" in SPEC.md that blocks Phase 1
  is answered.

## Phase 1: Modular skeleton with a scraping smoke test

- **Status:** Done (2026-11-15, PR #3)
- **Outcome:** The modular platform runs end-to-end. Both modules exist in code; the
  housing module can read one live page of offers from OtoDom on demand.
- **Included work:** .NET solution with Host and module contract; modules
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

- **Status:** Done (2026-11-17, PR #7)
- **Outcome:** The housing smoke test becomes a real import: basic offer data is
  saved and listed in the UI.
- **Included work:** Offer table with basic fields, unique per source and external id;
  saving new offers and updating known ones; a simple offer list with paging;
  module layers split into projects (ADR 0017); raw captures of every fetched page with
  re-parsing and retention; built-in .NET features instead of custom code.
- **Exit criteria:** Running the import twice stores each offer once; the list shows
  the stored offers.

## Phase 4: First deployment to a home server

- **Status:** Done (2026-11-19, PR #10, PR #11)
- **Outcome:** Bastion runs on a small home-lab server, not only on a developer machine.
  Hardware limits show up early, while the design can still change.
- **Included work:** Docker Compose started by systemd (SPEC); the VPN check inside Docker;
  secrets on the server; resource use measured during an import. Safe updates without a
  second environment on the server: versioned images, a nightly database backup and one
  on demand before a deployment, and a check of the new image on another machine against a copy of the production data.
- **Exit criteria:** the server starts Bastion after a reboot; an import through the VPN
  works there; memory, CPU and disk use during an import are measured; a production backup
  restored on another machine runs with the image that goes to the server.

## Phase 5: Background jobs

- **Status:** In progress
- **Outcome:** Offers are collected on their own. New offers arrive every few minutes, and a
  nightly full scan keeps the whole listing current. The user can also start a job, watch a
  live counter and see the result (added, updated, unchanged offers) without reloading the page.
- **Included work:** jobs as a building block with runs in the module's schema;
  the whole rental listing of a portal as the collected scope; the import as a job in two runs:
  new offers (frequent, stops at known offers) and a full scan (nightly, around 01:00, before
  the backup, to the last page); offers missing from two complete full scans in a row marked
  as removed (soft delete); schedules from configuration; live status through SignalR; raw data
  retention as a daily job.
- **Exit criteria:**
  - New offers and the full scan run on their own at the configured times.
  - An import runs in the background and its result appears without reloading the page;
    after a reload during the run the counter continues.
  - An import never reads more pages than the configured limit.
  - An offer missing from two complete full scans in a row is marked as removed and comes back
    when it reappears; an interrupted scan marks nothing.
  - A failed import (e.g. without a VPN) shows why.
  - A second start of a running job does not start a second run; a restart during a run
    leaves it `interrupted`.
  - Retention runs on its own.

## Phase 6: Places

- **Status:** Planned
- **Outcome:** Every offer is linked to its place, county and voivodeship, and the user sees
  what kind of place it is: village or town, and how big.
- **Included work:** a dictionary of places from open data (Wikidata first); the official
  status next to our own size classes with thresholds from configuration.
- **Exit criteria:** set at the start of the phase.

## Phase 7: Market statistics

- **Status:** Planned
- **Outcome:** The user compares the rental market across groups (voivodeship, kind of place,
  rooms) in one view and draws conclusions quickly.
- **Included work:** asking price per m² and per month (median and spread); active and new
  offers; time on market; price cuts; area for a given budget; small groups marked as uncertain.
- **Exit criteria:** set at the start of the phase.

## Phase 8: Duplicates and broken offers

- **Status:** Planned
- **Outcome:** Statistics count every flat once and ignore broken offers.
- **Included work:** grouping offers of the same flat; rules for broken offers decided from the
  collected data.
- **Exit criteria:** set at the start of the phase.

## Phase 9: Offer score

- **Status:** Planned
- **Outcome:** Every offer shows how its asking price compares with similar offers
  ("14% below typical").
- **Exit criteria:** set at the start of the phase.

## Phase 10: Offer details

- **Status:** Planned
- **Outcome:** Offers carry what only their own page shows, so the user can judge an offer
  without opening the portal.
- **Included work:** detail pages of new and changed offers, a limited number per run.
  Fields and storage are decided at the start of the phase from real pages.
- **Exit criteria:** set at the start of the phase.

## Phase 11: Filtering offers

- **Status:** Planned
- **Outcome:** The user narrows the stored offers to what matters (place, kind of place, price,
  area, rooms, score) and saves filters for later.
- **Exit criteria:** set at the start of the phase.

## Phase 12: Map and surroundings

- **Status:** Planned
- **Outcome:** The user sees whether an offer is near a city and how far it is from a school,
  a shop, public transport, green areas and similar places.
- **Included work:** OpenStreetMap data; the point of an offer as precise as the portal
  allows; distance to the nearest city; municipality seat.
- **Exit criteria:** set at the start of the phase.

## Phase 13: Place attractiveness

- **Status:** Planned
- **Outcome:** The user sets what matters (cost, market, surroundings, public statistics) and
  sees places ranked by these weights.
- **Included work:** public statistics (GUS) joined with places.
- **Exit criteria:** set at the start of the phase.

## Later

Known candidates, in no fixed order:
- Run history: a list of runs with a filter for failed ones; run details with a timeline of
  events and links to the raw pages.
- Market trends and seasonality, once months of data exist.
- Retention of removed offers.
- Triage (statuses, notes, bookmarks), once offers carry the place data that helps decide.
- More portals with grouping of duplicate offers across portals.
- The job offers module.
- New modules that need not be scrapers.
- Re-parse as a background job; raw pages of one run packed into one archive.
