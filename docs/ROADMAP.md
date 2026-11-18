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

- **Status:** Done (2026-11-17, PR #7)
- **Outcome:** The housing smoke test becomes a real import: basic offer data is
  saved and listed in the UI.
- **Included work:** Offer table with basic fields, unique per source and external id;
  saving new offers and updating known ones; a simple offer list with paging;
  module layers split into projects (ADR 0017); raw captures of every fetched page with
  re-parsing and retention (ADR 0013); built-in .NET features instead of custom code.
- **Exit criteria:** Running the import twice stores each offer once; the list shows
  the stored offers.

## Phase 4: First deployment to a home server

- **Status:** Planned
- **Outcome:** Bastion runs on a small home-lab server, not only on the developer's desktop.
  Hardware limits show up early, while the design can still change.
- **Included work:** Docker Compose started by systemd (SPEC); the VPN check inside Docker;
  secrets on the server; resource use measured during an import. Server setup and backups
  are decided at the start of the phase.
- **Exit criteria:** the server starts Bastion after a reboot; an import through the VPN
  works there; memory, CPU and disk use during an import are measured.

## Phase 5: Background jobs

- **Status:** Planned
- **Outcome:** Long work runs in the background. The user starts an import, watches a live
  counter and sees the result (added, updated, unchanged offers) without reloading the page.
- **Included work:** jobs as a building block with runs in the module's schema (ADR 0018);
  the import as a job reading several listing pages, stopping at known offers or at a page
  limit from configuration; live status through SignalR; raw data retention as a daily job.
- **Exit criteria:**
  - An import runs in the background and its result appears without reloading the page;
    after a reload during the run the counter continues.
  - An import never reads more pages than the configured limit.
  - A failed import (e.g. without a VPN) shows why.
  - A second start of a running job does not start a second run; a restart during a run
    leaves it `interrupted`.
  - Retention runs on its own.

## Phase 6: Offer details

- **Status:** Planned
- **Outcome:** Offers carry what only their own page shows, so the user can judge an offer
  without opening the portal.
- **Included work:** detail pages of new and changed offers, a limited number per run
  (ADR 0016). Fields and storage are decided at the start of the phase from real pages.
- **Exit criteria:** set at the start of the phase.

## Phase 7: Job schedules

- **Status:** Planned
- **Outcome:** Offers are collected on their own. The user sets in plain terms when each job
  runs ("every hour, 7:00–22:00, Monday–Friday"), pauses it or runs it now.
- **Included work:** Quartz.NET with the schedules stored in the module's tables (new ADR);
  a schedule can later belong to a search profile.
- **Exit criteria:** a schedule changed in the UI survives a restart and the job runs at the
  new times; a paused job does not run; missed runs are not caught up.

## Phase 8: Run history

- **Status:** Planned
- **Outcome:** The user sees what every job did and why a run failed, so jobs and their
  audit form one complete feature.
- **Included work:** a list of runs with a filter for failed ones; run details with a timeline
  of events and links to the raw pages (ADR 0013).
- **Exit criteria:** every run appears with its result; a failed run shows its error and the
  events that led to it.

## Phase 9: Places

- **Status:** Planned
- **Outcome:** Every offer is linked to its place, and the user sees what kind of place it is:
  village or town, and how big.
- **Included work:** a dictionary of places from open data (Wikidata first); the official
  status next to our own size classes with thresholds from configuration.
- **Exit criteria:** set at the start of the phase.

## Phase 10: Filtering offers

- **Status:** Planned
- **Outcome:** The user narrows the stored offers to what matters: place, kind of place,
  price, area, rooms.
- **Exit criteria:** set at the start of the phase.

## Phase 11: Search profiles

- **Status:** Planned
- **Outcome:** The user decides in the UI what is collected (e.g. "flats to rent in Kraków"),
  each search with its own schedule.
- **Exit criteria:** set at the start of the phase.

## Phase 12: Places on the map

- **Status:** Planned
- **Outcome:** The user sees whether an offer is near a city or far from everything.
- **Included work:** OpenStreetMap data; the point of an offer as precise as the portal
  allows; distance to the nearest city; municipality seat.
- **Exit criteria:** set at the start of the phase.

## Phase 13: Surroundings

- **Status:** Planned
- **Outcome:** The user sees how far an offer is from a school, a shop, a gym and similar places.
- **Exit criteria:** set at the start of the phase.

## Later

Known candidates, in no fixed order:
- Triage (statuses, notes, bookmarks), once offers carry the place data that helps decide.
- More portals with grouping of duplicate offers across portals.
- The job offers module.
- New modules that need not be scrapers.
- Re-parse as a background job; raw pages of one run packed into one archive (ADR 0013).
