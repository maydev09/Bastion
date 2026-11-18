# 0013. Audit, logging and raw data

- **Status:** Accepted; partly superseded by [0018](0018-background-jobs-as-building-block.md)
- **Date:** 2026-11-15, updated 2026-11-17 (raw captures as built)

## Context

Scrapers break often: portals change their markup, block requests or send invalid
data. The maintainer must be able to answer quickly, from the UI: when did a run
happen, what did it find, what failed and why, and what exactly did the portal send.
The legacy job scraper only dumped HTML of failed pages into a folder and zipped the
folder when it grew too big. That raw HTML is what made fixing broken parsers possible.

Earlier decisions give us the base: every job run is recorded by the platform
(ADR 0009), modules own their data (ADR 0002), and scraping helpers live in the
scraping toolkit, not in the platform (SPEC).

## Decision

Audit has three levels, from overview to evidence, plus technical logs.

### 1. Run summary (platform)
- `platform.job_run` (ADR 0009) also stores a summary reported by the job, as `jsonb`
  (e.g. new, updated, unchanged, failed counts). The UI lists runs with time, duration,
  status and counts, and can filter to runs with errors.

### 2. Run timeline (platform)
- `platform.job_run_event`: events of one run with time, level (`info`, `warning`,
  `error`), message and optional `jsonb` data. Jobs write them through `JobContext`.
- It is generic: the platform knows nothing about offers or pages, so any module can use it.

```text
14:30:09  info   Page 1: 72 offers (5 new, 2 updated)
14:30:15  warn   Offer 6512 skipped: missing price           [raw data]
14:30:21  error  Page 3: __NEXT_DATA__ not found               [raw data]
```

### 3. Raw data (module)
- Every fetched page or API response is stored **before** parsing, not only failed ones,
  so parsers can be fixed and old data re-parsed without scraping again. Stored pages also
  serve as samples for offline mode and tests. A raw capture is what one request returned,
  in any format (HTML, JSON, a browser snapshot), not one offer.
- **Personal data is anonymized before a page is stored.** Each portal's
  `IOfferSource.Anonymize` replaces the values of personal fields (names and photos of owners
  and agents) wherever the page repeats them, phone numbers and e-mails in free text, and our
  own session ids with a configured mask (`[Modules.Housing.RawCaptures] AnonymizationMask`,
  default `***`). Every other field stays as it was; the parser reads the anonymized page on
  import and re-parse.
  A stored page is therefore not byte-for-byte what the portal sent, and its SHA-256 is of
  the anonymized content. Company names (agencies) are not masked.
- Each module has a `raw_capture` table in its own schema: source, kind, content type, URL,
  HTTP status, fetch time, size and SHA-256. An offer links to the capture that last changed
  it (`raw_capture_id`). Timeline events will link to captures, and the UI can show the content.
- The content is a file on a Docker volume, outside the database:
  `/data/raw/<module>/<yyyy-MM-dd>/<id>.zst`, one file per capture, one folder per day (UTC).
  The path is configured per module (`[Modules.Housing.RawCaptures] Path`).
- Compression is **Zstandard** from ZstdSharp (MIT) until we move to .NET 11, which has
  `ZstandardStream` built in. Files open with standard tools (`zstd -dc <id>.zst`).
- Once job runs exist (ADR 0009), the captures of one run are packed into one
  `run-<id>.tar.zst` with long distance matching, so similar pages compress well together
  (tar is built in, `System.Formats.Tar`). Until then each capture is its own file.
- In the module: `RawCaptureRepository` (table), `RawCaptureFileStorage` (files) and
  `RawCaptureService` (keeps a row and its file together, reads them back, applies retention);
  use cases use only the service. Each portal is an `IOfferSource` strategy with `FetchAsync`
  (raw pages) and `Parse(RawPage)` (offers); the `OfferSources` registry picks it by source.
- **Re-parse** parses every stored capture again, oldest first, saves the offers with the
  capture's fetch time and fetches nothing (`POST /api/housing/reparse`, a button in the UI).
- Offers record the run in which they were first seen and last seen. Their changelog
  links to the run that made each change.

### Retention
- A daily cleanup job (ADR 0009) deletes raw data older than 30 days, raw data of failed
  runs older than 180 days, and timeline events older than 180 days. A total size limit
  for raw data is a safety net. All limits are configurable.
- Until that job exists, raw data older than `RetentionDays` (default 30) is deleted, rows and
  day folders together, at the start of every import and re-parse. Raw pages contain personal
  data (ADR 0016), so re-parsing works only within the retention period.

### Technical logs
- Code logs only through `ILogger<T>`. **Serilog** is configured once in the Host as
  the logging provider; the static `Log` class is not used.
- Outputs: console (for `docker logs`) and daily rolling plain-text files
  (`bastion-<yyyyMMdd>.log`) in `/data/logs`, kept 14 days. Same line format as the
  console, so they are easy to read with `less` or `tail -f`.
- Each entry inside a job carries `RunId`, module and job name, so logs and audit can
  be matched.
- Logs are for technical debugging (startup errors, crashes). The audit in the database
  is what the user works with in the UI.

## Alternatives considered

- **Raw data in the database (`bytea`):** one place for everything, but the database
  and its backups grow quickly.
- **Selected fields as JSON per offer** (`listing_json`, used before raw captures): small, but
  only for JSON portals and only for the fields chosen in advance.
- **Compressing each file separately with gzip:** simple, but gzip only looks 32 KB back,
  so it cannot use the similarity between pages.
- **`tar.zst` per import before job runs exist:** needs a temporary run concept; files expire
  after 30 days, so packing them later is cheap.
- **Packing by folder size** (legacy approach): works, but a run is the natural unit
  and is easy to find from the audit.
- **xz:** slightly smaller archives, but much slower, especially when opening raw data from the UI.
- **WARC** (format of web archives): standard for crawling at scale, more than we need.
- **Object storage (MinIO/S3):** not needed for one server; paths can move there later.
- **Compact JSON log files:** easy to filter by property with `jq`, but hard to read by eye;
  structured data the user works with is in the audit tables anyway.
- **Built-in logging providers only:** no file output; **NLog:** comparable to Serilog,
  less common in new projects.

## Consequences

- Any failed parse can be traced from the UI down to the exact bytes the portal sent.
- Disk usage is predictable thanks to compression and retention.
- The Docker setup needs a `/data` volume and it should be backed up together with the database.
- Raw data compression needs one package (ZstdSharp) until the move to .NET 11.
- A new portal is one new `IOfferSource` class and one DI line.
