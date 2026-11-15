# 0013. Audit, logging and raw data

- **Status:** Accepted
- **Date:** 2026-11-15

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

### 3. Raw data (scraping toolkit)
- Every fetched page or API response is stored, not only failed ones, so parsers can
  be fixed and old data re-parsed without scraping again. Stored pages also serve as
  samples for offline mode and tests.
- Files live on a Docker volume, outside the database:
  `/data/raw/<module>/<yyyy-MM-dd>/run-<id>/`. During a run, pages are written as plain
  files. When the run ends, the folder is packed into one `run-<id>.tar.zst` and the plain
  files are deleted. Folders left by crashed runs are packed by the cleanup job.
- Compression is **Zstandard** with long distance matching, so similar pages of one run
  compress well together. Tar packing is built in (`System.Formats.Tar`); Zstandard
  comes from ZstdSharp (MIT) until we move to .NET 11, which has `ZstandardStream` built in. The archives can also be opened with
  standard Linux tools (`tar --zstd -xf`).
- Each module has a `raw_capture` table (run, URL, time, HTTP status, size, SHA-256,
  archive path) in its own schema. Timeline events link to it, and the UI can show
  the raw content.
- Offers record the run in which they were first seen and last seen. Their changelog
  links to the run that made each change.

### Retention
- A daily cleanup job (ADR 0009) deletes raw data older than 30 days, raw data of failed
  runs older than 180 days, and timeline events older than 180 days. A total size limit
  for raw data is a safety net. All limits are configurable.

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
- **Compressing each file separately with gzip:** simple, but gzip only looks 32 KB back,
  so it cannot use the similarity between pages.
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
