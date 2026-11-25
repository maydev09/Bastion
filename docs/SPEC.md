# Bastion specification

- **Status:** Draft
- **Last updated:** 2026-11-16

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
  owned by the user, and recognizes the same offer published on several portals, so the
  user sees it once instead of three times.
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
- Background jobs: modules that need them register scheduled and manually triggered jobs
  through a building block, not the platform core. Every job run is recorded in
  the module's own schema with its status, timing and errors.
- Audit: each run has a summary and a timeline of events viewable in the UI;
  technical logs go to the console and rolling files.
- Configuration, logging and health checks, shared by all modules.
- Database access and schema migrations, with a separate schema per module.

## Module: Housing (first module)

Replaces the legacy flat offers scraper.

- Collects housing offers: flats, houses and rooms, for rent or for sale (title, area, rooms, price, rent, agency, flags, description)
  from sources; each source is a separate, pluggable scraper.
- The user manages **search profiles** in the UI: which source to scrape and with what
  criteria (e.g. rent or buy, city, price range, rooms). Each run executes the enabled
  profiles. Several profiles can run side by side (e.g. rent in one city, buy in another).
- Requests are rate-limited with randomized delays between calls and follow the
  scraping rules (robots.txt, one request at a time per portal, no personal data), see `AGENTS.md`.
- Each source can be run from saved sample files instead of the live website
  (offline mode for development and tests).
- When parsing fails, the raw input and the error are saved for debugging,
  and the remaining sources keep running.
- Every run records its parameters and counts of new, unchanged and total offers.
- Offers are unique per source by the source's external id. Changes to known offers
  are applied and recorded in a changelog.
- **Places:** every offer is linked to its place (town or village, with municipality, county
  and voivodeship) from a dictionary of Polish places loaded from Wikidata (CC0) on demand.
  Next to the official status the user sees a size by population, with thresholds from
  configuration. Matching is portal-neutral: each source turns its own location format into
  a hint that one matcher resolves.
- **Duplicates across portals:** offers that are probably the same flat on different
  portals (or posted twice on one) are grouped, so the list shows one entry with links
  to every source. A grouping is a suggestion: the user can confirm or split it.
- Triage: browse, filter and sort offers (the view, e.g. page, sort order and filters, is part of the
  link, so a copied link opens the same view). Filters: place and voivodeship (or offers whose location
  matched no place; the place search also finds places without offers and shows them grayed out), the district
  and the neighborhood of a picked place (named as the portal names them; a picked district narrows its place, a picked
  neighborhood its district), size of the place
  (each size with the populations its configured thresholds cover), the highest seat it holds (voivodeship capital,
  county or municipality seat), price, price per m², area, rooms, furnished, amenities by category (all or any of
  the picked) and a price cut seen in the changelog. Filters join with AND, the choices inside one filter with OR; each choice shows how many
  offers it would leave, and the filters in use show above the list, removable one by one. Above the list,
  the typical price, price per m², rent and area of the filtered offers (median and the middle half), and
  quick picks for offers found in the last 24 hours or since the last visit; the price, price per m² and area
  filters show how the offers spread over the values. The offer card shows where the offer stands among similar ones
  (the same room count in the same place, or in the voivodeship when the place has too few): price per m²,
  rent, area and days listed against their median and middle half. Change status (e.g. not interested,
  waiting, visit scheduled, still interested after visit), bookmark, add notes.
  Every manual change is recorded in the changelog.

## Module: Job offers (second module)

Replaces the legacy job offers scraper. Everything listed for housing applies,
with job-specific data (company, salary range, seniority, skills, contract type,
work mode) and statuses (e.g. applied, interview scheduled, rejected), plus:

- Search profiles use job-specific criteria (e.g. work mode, specialization, sorting).
  The legacy scraper hardcoded these combinations; here they are managed in the UI.
- Run statistics also count updated offers and can be viewed per run, per hour and per day.
- Duplicates across portals are grouped as for housing. The legacy scraper only
  flagged offers with the same checksum of company and title (`duplicate_possible`).

## Shared scraping toolkit

Both scraper modules need the same technical building blocks (HTTP/browser fetching,
rate limiting, offline sample files, error capture, storing every raw response
compressed per run with retention). These end up in a reusable
library that scraper modules use. It is not part of the platform core, because
non-scraper modules do not need it. The code starts inside the housing module
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
  On the server the app shares the network of a WireGuard container, so the same check works
  inside Docker (ADR 0019).
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
  server, started by systemd (ADR 0010); setup and updates are in README ("Running on a
  server"); database backups are pulled to another machine (ADR 0020).
  The web UI has no login (single user, see AGENTS.md "Focus on the core problem");
  revisit only if Bastion is ever reachable from the internet.
- Duplicate matching: which signals decide that two offers are the same (exact
  checksum of key fields, or a similarity score over address, price, area, rooms,
  title), and is a group stored or derived from the data? Decide before the
  second portal of a module is added.
- AI support: the maintainer asks Claude (an existing subscription, no API cost) about the market instead of
  writing SQL or clicking. Direction (2026-11-25): a read-only MCP server over the app's own statistics
  handlers, the same ones the HTTP API calls, not over the database; wanted soon (Phase 10, with the market
  view). Open: transport (HTTP on the Host or stdio), how a module registers its tools without the platform
  knowing module code, and access on the home network without a login. Decide in an ADR before it is built.
