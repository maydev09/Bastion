# 0018. Background jobs as a building block, runs owned by modules

- **Status:** Accepted
- **Date:** 2026-11-18

## Context

ADR 0009 put the scheduler and its tables (`platform.job_schedule`, `platform.job_run`) in
the platform, and ADR 0013 added the run summary and timeline there too. That makes jobs a
platform feature that every module gets, but not every module needs background work, and
a module should own all of its data, run history included. The platform only hosts modules:
contract, navigation, routes and cross-cutting concerns (ADR 0007).

Long operations (import, re-parse, retention) run inside one HTTP request today. The user
must be able to start one from the UI, leave it running in the background, and see its live
state and result, also after reloading the page. Retention of raw data must run on its own,
not only at the start of an import (ADR 0013).

## Decision

Background jobs are a building block, `Bastion.BuildingBlocks.Jobs`, that a module uses only
if it needs it. Run data lives in the module's own schema. The platform knows nothing about jobs.

### Module side

```csharp
public interface IJob
{
    Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken);
}

// in HousingModule.AddServices
services.AddJobs(schema: "housing")
    .Add<ImportOffersJob>("import-offers")
    .Add<RawCaptureRetentionJob>("raw-capture-retention", moduleSection.GetSection("Jobs:RawCaptureRetention"));

// in HousingModule.MapEndpoints
moduleGroup.MapJobs();
```

- A job returns a short summary for the user (e.g. added, updated and unchanged offers;
  deleted files and freed space). The business data itself stays in the module's tables.
- The module creates the job tables in its own migrations, in its own schema.
- A module without jobs does not reference the building block and has no job tables.

### Runs

- `<module>.job_run`: job name, trigger (`manual`, `schedule`), status (`queued`, `running`,
  `succeeded`, `failed`, `cancelled`, `interrupted`), `queued_at`, `started_at`,
  `finished_at`, error, and the summary as `jsonb`. Duration is `finished_at - started_at`,
  not stored separately.
- A queue in a `BackgroundService` runs the jobs. A manual start and a scheduled start use
  the same path.
- The rules of ADR 0009 stay: the same job never runs twice at the same time (a second start
  while one is queued or running returns the existing run), different jobs may run in
  parallel, no automatic retries, missed runs are not caught up, a run stopped by shutdown is
  `cancelled`, and on startup runs without a finish time are marked `interrupted`.

### Endpoints and live updates

- `MapJobs()` adds, under the module's route group:
  - `POST /api/<module>/jobs/{job}/runs`: queues a run, answers `202 Accepted` with the run id.
  - `GET /api/<module>/jobs/{job}/runs/latest`: status, timestamps, summary, error and the
    server's current time.
  - A SignalR hub that sends `JobRunStarted` and `JobRunFinished` to clients of that module only.
- The state is in the database, the browser only shows it. After a reload, in a second tab or
  on another device, the UI reads the latest run and subscribes to the hub again.
- The UI counts elapsed seconds itself from `started_at`, corrected by the server's time.
  The server does not send a message every second.
- A shared frontend hook in `src/Web/Shared` connects to a module's hub, so module UIs do
  not repeat this code.

### Schedules

- For now a job may have a fixed interval in the module's configuration:

```toml
[Modules.Housing.Jobs.RawCaptureRetention]
Interval = "1.00:00:00"   # once a day
```

- Schedules that the user edits in the UI (time window, days of week, pause, next run) are
  still planned as in ADR 0009, but in the module's schema (`<module>.job_schedule`).
- The run timeline (`job_run_event`, ADR 0013) will also live in the module's schema.

### Unchanged from ADR 0009

No scheduling library; Quartz.NET stays the upgrade path behind `IJob` and `AddJobs`.

## Alternatives considered

- **Jobs in the platform (ADR 0009):** one scheduler and one place for all runs. Not chosen:
  modules would not own their run data, and modules without jobs would carry a platform
  feature they do not use. A cross-module overview can be added later on top of what modules expose.
- **Everything inside each module, no shared code:** full freedom, but every module with
  jobs would copy the queue, the run rules and the live updates.
- **Polling the run status from the UI:** no extra connection, but either delayed or many
  useless requests.
- **Server-Sent Events:** built into the browser and into .NET 10, one-way only, which is
  enough for status updates. Not chosen: SignalR adds automatic reconnect handling,
  per-module groups and room for messages from the client later, at the cost of one
  frontend package (`@microsoft/signalr`).

## Consequences

- The platform stays small; jobs are an opt-in tool like the scraping toolkit.
- Every module with jobs has its own `job_run` migration, a few lines of SQL.
- Seeing the runs of all modules in one place needs extra work later.
- ADR 0009 is superseded where it puts schedules and runs in the platform. ADR 0013 is
  superseded where it puts the run summary and timeline in the platform.
- Retention of raw data becomes a job and no longer runs at the start of imports and re-parses.
