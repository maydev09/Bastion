# 0009. Background jobs with a hosted service and own schedules

- **Status:** Accepted; partly superseded by [0018](0018-background-jobs-as-building-block.md)
- **Date:** 2026-11-15

## Context

Modules need scheduled and manually triggered jobs, for example "check new flat
offers every hour between 7:00 and 22:00" (SPEC, Shared services). Every run must be
recorded with its status, timing and errors.

Bastion is meant for people without technical knowledge. They must be able to see and
change schedules in the UI in plain terms ("every hour, 7:00–22:00, Monday–Friday"),
start a job now, pause it, and see when it ran last and when it runs next. Cron
expressions or a technical dashboard are not acceptable for them.

The schedule is therefore user data that lives in our database whatever scheduler we
use. The scheduler only has to answer "is this job due now?" and run it.

We start simple: build what is useful now with plain .NET, and adopt a bigger tool
only when a real need appears that the simple solution cannot cover.

## Decision

The platform contains a small scheduler: one `BackgroundService` plus its own tables.
No scheduling library is used.

### Registering a job (module side)

```csharp
public interface IJob
{
    Task Run(JobContext context, CancellationToken cancellationToken);
}

// in IModule.AddServices
services.AddJob<IndexOffersJob>("index-offers", defaultSchedule: Schedule.Hourly(from: 7, to: 22));
```

- A module only says "I have a job with this name and this default schedule".
- The job's business result (e.g. new and known offers) stays in the module's own tables.

### Schedules and runs (platform side)

- `platform.job_schedule`: one row per job (module, job name, enabled, interval,
  active hours, days of week, next run). Created from the default on first start;
  afterwards it is edited only from the UI.
- `platform.job_run`: one row per run (job, trigger `schedule`/`manual`, status,
  started, finished, error).
- The schedule model is deliberately simple: every N minutes or hours, within a
  time window, on selected days. Times are in the time zone from configuration
  (default `Europe/Warsaw`).

### Running

- A `PeriodicTimer` ticks every minute, reads due schedules and starts them.
- The same job never runs twice at the same time. A due run while the job is still
  running is skipped. "Run now" from the UI uses the same path.
- Different jobs may run in parallel.
- No automatic retries: the next scheduled run is the retry. For scrapers an instant
  retry would only hit a portal that has just blocked us.
- Missed runs (application was off) are not caught up; the next run is computed from now.
- On shutdown the running job gets the cancellation token; the run is marked `cancelled`.
- On startup, runs without a finish time (crash, power loss) are marked `interrupted`.
- Jobs of a disabled module are not registered, so their schedules are ignored but kept.

## Alternatives considered

- **Quartz.NET:** mature scheduler with cron, calendars, misfire policies, clustering
  and a persistent store. Apache 2.0 licensed. Not chosen now: its API is heavy for
  our needs, its store adds about 11 `qrtz_*` tables, and the schedule would live
  twice (our table for the UI and Quartz's tables). **It is the upgrade path:** if we
  ever need complex calendars, many job types with advanced policies, or several
  instances, Quartz replaces the internals of the platform scheduler. Modules only
  know `IJob` and `AddJob`, so they do not change.
- **Hangfire:** convenient queued and recurring jobs with a built-in dashboard. Not
  chosen: the core is LGPL and some features are only in the paid Pro edition (against
  ADR 0005), the PostgreSQL storage is a community package, the dashboard is too
  technical for our users, and automatic retries do not suit scraping.
- **OS cron** (as in the legacy job scraper): simplest for one job, but schedules
  cannot be edited from the UI, runs happen outside the application, and every job
  needs its own entry point.

## Consequences

- About 150 lines of platform code that we own and test, no extra dependency.
- We must test the schedule logic ourselves (time windows, days, DST changes);
  it is pure code, so unit tests are easy.
- One process only. Running several instances would need a lock or a switch to Quartz.
- The UI shows schedules and run history from platform endpoints, the same for every module.
