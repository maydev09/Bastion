# 0016. Scraping rules

- **Status:** Accepted
- **Date:** 2026-11-15, updated 2026-11-16 (accounts, portal review)

## Context

Bastion collects offers from public portals. We want to be a polite visitor: follow
the rules site owners publish, never overload their servers, and not collect personal
data we do not need. These rules apply to every scraper and are implemented once in
the scraping toolkit, so a module cannot forget them.

## Decision

### Scope
- We start with publicly accessible pages.
- **No accounts:** scrapers never log in, register or accept a portal's terms. Data
  behind a login is out of scope.

### Review before adding a portal
- No scraper for a new portal is written before its review is recorded in
  `docs/portals/<portal>.md`. The review checks and links:
  - the portal's terms of service: clauses on automated access, bots, copying,
    aggregating or reusing content;
  - `robots.txt`: whether the paths we need are allowed, crawl delay, AI or content signals;
  - whether the data is public or needs an account (then it is out of scope);
  - which personal data the pages contain and how we avoid storing it;
  - anti-bot measures the portal uses (CAPTCHA, `403` for data centers).
- The review records facts with links; whether to build the scraper is the
  maintainer's decision. It is updated when the portal's terms or `robots.txt` change.

### Browser
- Pages are fetched with a regular browser driven by **Playwright**, or with a plain
  HTTP client where a portal serves the data that way. No stealth plugins and no
  fingerprint manipulation.
- Plain HTTP requests send the `User-Agent` of the Firefox that Playwright ships, read
  from the browser itself (`navigator.userAgent`) and never typed into configuration.
  Headless Firefox reports the same string as a regular Firefox. robots.txt rules are
  read from the `*` group.

### robots.txt
- Before requesting a URL, the toolkit checks it against the portal's `robots.txt`
  and refuses disallowed URLs. The refusal is recorded in the run timeline (ADR 0013).
- `robots.txt` is fetched at most once a day per host and cached. It is refreshed
  lazily before the first request after the cache has expired; no separate job is needed.
- Unavailable `robots.txt` is handled as in RFC 9309: a `4xx` response means everything
  is allowed, a `5xx` response or a network error means nothing is allowed (fail closed).

### Not overloading portals
- **One request at a time per host**, across all runs, search profiles and modules.
  The toolkit keeps one gate per host in the process; a request waits until the
  previous one to the same host has finished and the randomized delay has passed.
  So two runs started at the same time queue up instead of doubling the load.
- `429 Too Many Requests` and `503 Service Unavailable` slow down the host: honor
  `Retry-After` if present, otherwise back off with growing delays.
- After several failed requests in a row the run stops instead of retrying further.
- Delays and limits are technical configuration (ADR 0008), with safe defaults.

### Not fetching the same data twice
- Listing pages are fetched page by page and paging stops when a page brings no new
  offers (the incremental stop of the legacy flat scraper).
- **Change detection by checksum:** for each offer seen on a listing page, a SHA-256
  of the listing fields we store (`listing_hash`) is compared with the stored one. The
  offer is updated only when it is new or the hash differs. Dates published by portals
  are stored as data (e.g. for sorting by age) but are not used to detect changes:
  they differ between portals, change when an ad is only "bumped", may stay the same
  after an edit, and are sometimes invalid. Computing the hash costs microseconds,
  a request costs seconds.

### Listing and details
- Data from listing pages and from detail pages are kept in two tables with their own
  life cycle, e.g. `offer` and `offer_details` (one-to-one). `offer_details` stores the
  `listing_hash` of the listing version its details were fetched for.
- Which offers need details is **derived from the data, never stored as a flag**: an
  offer needs details when it has no `offer_details` row or its `listing_hash` differs
  from the one in `offer_details`.

```sql
SELECT o.id FROM offer o
LEFT JOIN offer_details d ON d.offer_id = o.id
WHERE d.offer_id IS NULL OR d.listing_hash <> o.listing_hash
LIMIT 30;
```

- Failed detail fetches are recorded (`last_attempt_at`, `failed_attempts`) and retried
  with growing pauses; after a few failures the offer is skipped and the timeline says so.
- **One job, two steps:** a run first indexes the listing pages, then fetches details
  for at most N offers (configurable). Offers not reached are picked up by the next
  run automatically. Splitting into two jobs later (e.g. different frequencies) needs
  no change in the database.
- Raw data is kept (ADR 0013), so a parser fix re-parses stored pages instead of
  scraping again.

### No personal data
- We do not store personal data of private persons: names, phone numbers, e-mail
  addresses or photos of people. Names of agencies and companies as published are kept.
- Instead of contact details we store the link to the offer or its contact page.
  The UI has a "Contact" link that opens the portal, where the user sees the details.
- Raw pages may contain personal data, so they are only kept for the retention period
  (ADR 0013) and never leave the server. Samples for tests are anonymized (AGENTS.md).

## Alternatives considered

- **Ignoring robots.txt:** many scrapers do, but it is the site owner's published rule.
- **Portal update dates for change detection:** no hashing, but unreliable across
  portals (see above).
- **A `needs_details` flag** (legacy flat scraper): state stored apart from the facts
  it depends on, easy to get out of sync, and it does not say why or since when.
- **Separate jobs for listing and details from the start:** more scheduling and two
  timelines for one logical process; not needed while one schedule fits both.
- **Daily job for robots.txt:** works too, but a lazy cache is less code and fetches
  nothing for portals that are not used.
- **Parallel requests to one portal:** faster runs, but more load and a higher risk of
  being blocked; run time is not critical for us.
- **Storing contact details:** convenient, but personal data we do not need (GDPR
  data minimization); the portal already shows them.
- **Scraping with a portal account:** more data, but logging in means accepting the
  portal's terms.

## Consequences

- A portal can block a path for us at any time through `robots.txt`; the run timeline
  shows why an offer or page was skipped.
- Runs take longer because requests to one host are serialized.
- Contact details are always one click away on the portal, never in our database.
