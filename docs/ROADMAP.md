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

## Phase 1: Walking skeleton with the flat offers module

- **Status:** Planned
- **Outcome:** The smallest working headquarters: the platform loads the flat offers
  module, which imports offers of one source from a saved sample file, stores them,
  and lists them in the web UI.
- **Included work:** .NET solution with platform host and module contract, React shell
  with navigation, flat offers module with one source in offline mode, local database
  via Docker Compose, architecture tests, GitHub Actions (build, test, lint),
  required as status checks in the existing `master` ruleset.
- **Risks:** Designing the module contract too broadly. Keep it to what this module
  needs now and extend it later.
- **Exit criteria:** A manual import from the sample file shows offers in the UI;
  disabling the module in configuration removes its pages and endpoints and the app
  still starts; architecture tests and CI pass.

## Phase 2: Flat offers, live scraping and history

- **Status:** Planned
- **Outcome:** The flat offers module scrapes the live source and keeps history.
- **Included work:** Live fetching with rate limiting and VPN check, error capture, run history with
  statistics, change tracking with changelog.
- **Exit criteria:** A manual live run stores new offers and records changes of known ones;
  a parsing failure is captured without crashing the app.

## Phase 3: Flat offers, triage and scheduling

- **Status:** Planned
- **Outcome:** The flat offers module fully replaces the legacy flat scraper.
- **Included work:** Statuses, bookmarks, notes, filters and sorting, changelog view,
  scheduled runs through platform background jobs, summary card on the home page.
- **Exit criteria:** All flat offers requirements from SPEC.md work; the legacy flat
  scraper can be retired.

## Phase 4: Job offers module, first source

- **Status:** Planned
- **Outcome:** A second, independent module runs alongside flat offers.
- **Included work:** Job offers module with one source; scraping code shared with flat
  offers is extracted into the scraping building block at this point.
- **Risks:** The module contract may need changes to fit a second module. That is
  expected; this phase is the real test of the modular design.
- **Exit criteria:** Both modules work; each can be disabled without affecting the other;
  no platform code is specific to either module.

## Phase 5: Job offers module, complete

- **Status:** Planned
- **Outcome:** The job offers module fully replaces the legacy job scraper.
- **Included work:** Remaining sources, scheduling, triage, run statistics, duplicate
  detection, summary card.
- **Exit criteria:** All job offers requirements from SPEC.md work; a failure in one
  source does not stop the others; the legacy job scraper can be retired.

## Later

New modules, chosen when Phase 5 is done. They follow the same contract and need
not be scrapers.
