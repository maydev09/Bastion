# Bastion specification

- **Status:** Draft
- **Last updated:** 2026-11-15

## Problem

- Personal tools tend to be built as separate one-off projects, each with its own
  hosting, database, configuration, UI and conventions. The maintainer already has
  two such legacy projects (a job offers scraper and a flat offers scraper).
- Every new tool repeats the same plumbing work, and there is no single place
  to see and manage everything.

## Vision

Bastion is a personal **headquarters**: one platform that hosts many independent
modules behind one web UI. The platform provides the shared plumbing; each module
brings its own functionality. The two legacy scrapers become the first two modules.
Later modules do not have to be scrapers at all.

## Why it exists

Bastion is for people who are lazy in the good sense: they would rather automate
pointless, repetitive work than do it by hand.

- Today, following flat or job offers means an account on every portal, visiting each one
  regularly and clicking through the same filters. The portals offer little useful automation,
  and every one of them shows the data differently.
- Bastion does that work: it collects offers from many portals into one normalized database
  owned by the user.
- Owning the data is the point. The user has full access to it, can run their own
  analytics on it, and finds what they are looking for faster than on any single portal.
- The machine does the collecting; the human only makes decisions.

## Users

- A single maintainer who runs Bastion on their own machine or home server
  and uses the web UI as the entry point to all modules.

## Platform requirements

### Modules
- A module is a self-contained feature area with its own backend logic, API endpoints,
  data, background jobs and UI pages.
- Modules plug into the platform through a defined module contract; the platform
  never contains module-specific code.
- A module works completely on its own: the application runs with any subset of
  modules enabled, including only one or none.
- Modules do not reference each other. If they ever need to cooperate, they do it
  only through the other module's public contracts.
- Each module owns its data; one module never reads or writes another module's tables.
- A whole module is enabled or disabled in configuration. A disabled module
  contributes no endpoints, jobs, navigation entries or pages.
- Inside a module, feature flags hide unfinished or optional functionality.
- Adding a new module does not require changes in other modules.
- Architecture rules and their enforcement: ADR 0002.

### Web UI shell
- One React application with shared layout and navigation.
- Navigation lists the enabled modules; each module contributes its own pages.
- A home page (the "headquarters" view) shows a summary card from each enabled module.

### Shared services
- Background jobs: modules can register scheduled and manually triggered jobs.
  Every job run is recorded with its status, timing and errors.
- Audit: each run has a summary and a timeline of events viewable in the UI;
  technical logs go to the console and rolling files (ADR 0013).
- Configuration, logging and health checks, shared by all modules.
- Database access and schema migrations, with a separate schema per module.

## Module: Flat offers (first module)

Replaces the legacy flat offers scraper.

- Collects flat offers (title, area, rooms, price, rent, agency, flags, description)
  from sources; each source is a separate, pluggable scraper.
- The user manages **search profiles** in the UI: which source to scrape and with what
  criteria (e.g. rent or buy, city, price range, rooms). Each run executes the enabled
  profiles. Several profiles can run side by side (e.g. rent in one city, buy in another).
- Requests are rate-limited with randomized delays between calls and follow the
  scraping rules (robots.txt, one request at a time per portal, no personal data): ADR 0016.
- Each source can be run from saved sample files instead of the live website
  (offline mode for development and tests).
- When parsing fails, the raw input and the error are saved for debugging,
  and the remaining sources keep running.
- Every run records its parameters and counts of new, unchanged and total offers.
- Offers are unique per source by the source's external id. Changes to known offers
  are applied and recorded in a changelog.
- Triage: browse, filter and sort offers; change status (e.g. not interested,
  waiting, visit scheduled, still interested after visit), bookmark, add notes.
  Every manual change is recorded in the changelog.

## Module: Job offers (second module)

Replaces the legacy job offers scraper. Everything listed for flat offers applies,
with job-specific data (company, salary range, seniority, skills, contract type,
work mode) and statuses (e.g. applied, interview scheduled, rejected), plus:

- Search profiles use job-specific criteria (e.g. work mode, specialization, sorting).
  The legacy scraper hardcoded these combinations; here they are managed in the UI.
- Run statistics also count updated offers and can be viewed per run, per hour and per day.
- Offers that look like duplicates across sources (same content checksum) are flagged.

## Shared scraping toolkit

Both scraper modules need the same technical building blocks (HTTP/browser fetching,
rate limiting, offline sample files, error capture, storing every raw response
compressed per run with retention, see ADR 0013). These end up in a reusable
library that scraper modules use. It is not part of the platform core, because
non-scraper modules do not need it. The code starts inside the flat offers module
and is extracted into the library when the job offers module needs it.

## Security and privacy

- No secrets in the repository; configuration comes from TOML files (ADR 0008) and
  environment variables; secrets only from encrypted secret files (ADR 0010).
- The repository never contains real scraped data. Sample HTML/JSON used in tests
  contains no personal data.
- User-specific preferences (search criteria, locations, price ranges) are never
  stored in code or configuration files; they are data managed through the UI.
- Live scraping goes out only through a VPN, so the maintainer's IP address is not exposed
  to scraped portals. The check is provider-agnostic (Proton, plain WireGuard or any other
  VPN): before every live run the scraping toolkit asks the OS which interface traffic to the
  portal's host uses (`ip route get <host>`) and fails the run (fail closed) unless it is a tunnel
  (type `wireguard`/`tun`, or a name matching configurable prefixes, by default `wg`, `tun`, `proton`).
  The exact method is not final yet, see Unresolved questions.
  The check is controlled by the `Scraping:RequireVpn` setting, `true` by default;
  it may be turned off only locally (e.g. when the whole machine sits behind a VPN router).
  Offline runs from sample files do not need a VPN. Bastion only checks the VPN;
  it never connects or disconnects it.

## Non-goals

- Running Bastion as a commercial product. It is open source under AGPL-3.0 (see `LICENSE`).
- Multi-user accounts, roles or a login for now. The data model has no concept of a
  user (no `user_id` columns); it is added in its own ADR if Bastion is ever hosted
  for several people.
- Third-party plugins: modules are developed in this repository.
- Bypassing CAPTCHAs or other explicit access restrictions of a website.
- Applying to jobs or contacting sellers automatically.
- Mobile apps.

## Acceptance criteria

- The platform hosts both scraper modules, and each can be disabled without affecting the other.
- Every capability of both legacy scrapers listed above is available in Bastion.
- A new module can be added without modifying the platform core or other modules.
- Each scraper source has tests that run against saved sample files, without network access.
- `master` builds and all tests pass in CI.

## Unresolved questions

- Feature flags (see ADR 0001 consequences).
- Should historical data from the legacy databases be migrated?
- Deployment details: Bastion and PostgreSQL run in Docker Compose on a headless home
  server, started by systemd (ADR 0010); server setup and backups are still open.
  The web UI has no login (single user, see AGENTS.md "Focus on the core problem");
  revisit only if Bastion is ever reachable from the internet.
- VPN check: is the route check (`ip route get`) the right method? Inside Docker it sees only
  the container's `eth0`. Alternatives: route scraping traffic through a WireGuard container,
  or compare the public egress IP with the home IP (needs an external IP service).
  Phase 1 runs the app directly on the desktop, where the route check works; the Docker
  case is decided together with deployment.
- AI support (later phase): e.g. "find the offers that fit me best" over the collected data.
  Options: an MCP server over the database used from an existing Claude subscription
  (no API cost), or Claude API calls on offers pre-filtered with SQL (pay per token).
  Preferred direction: MCP (read-only access to the data). Not needed soon.
