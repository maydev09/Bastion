using System.Threading.Channels;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bastion.BuildingBlocks.Jobs;

// One runner per module. Rules: the same job never runs twice at the same time, different
// jobs may run in parallel, no retries, a scheduled run missed while the application was off is skipped.
public sealed partial class JobRunner(
    string schema,
    IReadOnlyList<JobDefinition> jobs,
    JobRunRepository runs,
    IServiceScopeFactory scopes,
    IHubContext<JobsHub, IJobsClient> hub,
    IOptionsMonitor<JobScheduleOptions> schedules,
    IOptions<JobsOptions> options,
    TimeProvider time,
    ILogger<JobRunner> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    private readonly Channel<JobRun> _queue = Channel.CreateUnbounded<JobRun>();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, JobRun> _active = [];

    public static string OptionsName(string schema, string job) => $"{schema}.{job}";

    public bool Has(string job) => jobs.Any(j => j.Name == job);

    // Returns the run that is already queued or running, if there is one.
    public async Task<JobRun> QueueAsync(string job, JobTrigger trigger, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_active.TryGetValue(job, out var active))
            {
                return active;
            }

            // Not canceled from here on: a run saved in the database must reach the queue or be finished.
            var run = await runs.AddAsync(job, trigger, time.GetUtcNow(), CancellationToken.None);
            if (!_queue.Writer.TryWrite(run))
            {
                // The application is stopping and the queue is closed.
                run = run with { Status = JobRunStatus.Canceled, FinishedAt = time.GetUtcNow() };
                await runs.UpdateAsync(run, CancellationToken.None);
                return run;
            }

            _active[job] = run;
            return run;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<JobRun?> LatestAsync(string job, CancellationToken cancellationToken) =>
        runs.LatestAsync(job, cancellationToken);

    public async Task<JobList> ListAsync(CancellationToken cancellationToken)
    {
        var latest = (await runs.ListLatestAsync(cancellationToken)).ToDictionary(r => r.Job);
        var items = jobs
            .Select(job =>
            {
                var schedule = schedules.Get(OptionsName(schema, job.Name));
                return new JobListItem(job.Name, schedule.DailyAt, schedule.Every, latest.GetValueOrDefault(job.Name));
            })
            .ToList();
        return new JobList(items, options.Value.Scheduled, options.Value.TimeZone, time.GetUtcNow());
    }

    public Task<PagedResult<JobRun>> ListRunsAsync(JobRunFilter filter, PageRequest page, CancellationToken cancellationToken) =>
        runs.ListAsync(filter, page, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startedAt = time.GetUtcNow();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        var running = new List<Task> { MarkInterruptedAsync(startedAt, stoppingToken) };
        if (options.Value.Scheduled)
        {
            foreach (var job in jobs)
            {
                var schedule = schedules.Get(OptionsName(schema, job.Name));
                if (schedule.DailyAt is { } dailyAt)
                {
                    running.Add(ScheduleAsync(job.Name, now => DailySchedule.NextRun(now, dailyAt, timeZone), stoppingToken));
                }
                else if (schedule.Every is { } every)
                {
                    running.Add(ScheduleAsync(job.Name, now => now + every, stoppingToken));
                }
            }
        }
        else
        {
            LogSchedulesOff(logger, schema);
        }

        try
        {
            await foreach (var run in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                running.RemoveAll(t => t.IsCompleted);
                running.Add(RunAsync(run, stoppingToken));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        // Runs still waiting in the queue when the application stops are canceled.
        _queue.Writer.TryComplete();
        while (_queue.Reader.TryRead(out var run))
        {
            running.Add(FinishAsync(run with { Status = JobRunStatus.Canceled, FinishedAt = time.GetUtcNow() }));
        }

        await Task.WhenAll(running);
    }

    // Runs left without a finish time by the previous start of the application (crash, power loss).
    // Retried, because the database may not be ready yet when the application starts.
    private async Task MarkInterruptedAsync(DateTimeOffset startedAt, CancellationToken stoppingToken)
    {
        var wait = TimeSpan.Zero;
        while (true)
        {
            try
            {
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, time, stoppingToken);
                }

                await runs.MarkInterruptedAsync(startedAt, time.GetUtcNow(), stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                LogMarkInterruptedFailed(logger, e, schema);
                wait = RetryDelay;
            }
        }
    }

    // A run still going at the next time is not queued again (see QueueAsync), so a slow run skips a turn.
    private async Task ScheduleAsync(string job, Func<DateTimeOffset, DateTimeOffset> nextRun, CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {
                var now = time.GetUtcNow();
                var wait = nextRun(now) - now;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, time, stoppingToken);
                }

                await QueueAsync(job, JobTrigger.Schedule, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                LogScheduleFailed(logger, e, schema, job);
            }
        }
    }

    private async Task RunAsync(JobRun queued, CancellationToken stoppingToken)
    {
        // Lets the queue loop take the next run while this one works.
        await Task.Yield();

        var run = queued with { Status = JobRunStatus.Running, StartedAt = time.GetUtcNow() };
        try
        {
            await runs.UpdateAsync(run, CancellationToken.None);
            await SetActiveAsync(run);
            await NotifyAsync(client => client.JobRunStarted(run));
        }
        catch (Exception e)
        {
            LogRunFailed(logger, e, schema, run.Job, run.Id);
            await FinishAsync(run with { Status = JobRunStatus.Failed, FinishedAt = time.GetUtcNow(), ErrorCode = JobFailedException.Unexpected });
            return;
        }

        await FinishAsync(await ExecuteJobAsync(run, stoppingToken));
    }

    private async Task<JobRun> ExecuteJobAsync(JobRun run, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var type = jobs.Single(j => j.Name == run.Job).Type;
            var job = (IJob)scope.ServiceProvider.GetRequiredService(type);
            var summary = await job.Run(new JobContext(run.Id, run.Trigger), stoppingToken);
            LogSucceeded(logger, schema, run.Job, run.Id);
            return run with { Status = JobRunStatus.Succeeded, FinishedAt = time.GetUtcNow(), Summary = summary };
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return run with { Status = JobRunStatus.Canceled, FinishedAt = time.GetUtcNow() };
        }
        catch (JobFailedException e)
        {
            LogFailed(logger, schema, run.Job, run.Id, e.Code, e.Message);
            return run with { Status = JobRunStatus.Failed, FinishedAt = time.GetUtcNow(), ErrorCode = e.Code, Summary = e.Summary };
        }
        catch (Exception e)
        {
            LogRunFailed(logger, e, schema, run.Job, run.Id);
            return run with { Status = JobRunStatus.Failed, FinishedAt = time.GetUtcNow(), ErrorCode = JobFailedException.Unexpected };
        }
    }

    // Saves the final state with a second try, so a short database outage does not leave the run
    // queued or running; if both fail, the next start of the application marks it interrupted.
    private async Task FinishAsync(JobRun run)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                await runs.UpdateAsync(run, CancellationToken.None);
                break;
            }
            catch (Exception e)
            {
                LogSaveFailed(logger, e, schema, run.Job, run.Id, attempt);
            }
        }

        await RemoveActiveAsync(run.Job);
        await NotifyAsync(client => client.JobRunFinished(run));
    }

    // A failed message to the browsers never changes the state of a run.
    private async Task NotifyAsync(Func<IJobsClient, Task> send)
    {
        try
        {
            await send(hub.Clients.Group(schema));
        }
        catch (Exception e)
        {
            LogNotifyFailed(logger, e, schema);
        }
    }

    private async Task SetActiveAsync(JobRun run)
    {
        await _lock.WaitAsync();
        _active[run.Job] = run;
        _lock.Release();
    }

    private async Task RemoveActiveAsync(string job)
    {
        await _lock.WaitAsync();
        _active.Remove(job);
        _lock.Release();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Schedules of {Schema} jobs are off; jobs run only when started from the UI")]
    private static partial void LogSchedulesOff(ILogger logger, string schema);

    [LoggerMessage(Level = LogLevel.Information, Message = "Job {Schema}/{Job} run {RunId} succeeded")]
    private static partial void LogSucceeded(ILogger logger, string schema, string job, int runId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {Schema}/{Job} run {RunId} failed: {Code} {Detail}")]
    private static partial void LogFailed(ILogger logger, string schema, string job, int runId, string code, string detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {Schema}/{Job} run {RunId} failed unexpectedly")]
    private static partial void LogRunFailed(ILogger logger, Exception exception, string schema, string job, int runId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled run of job {Schema}/{Job} could not be queued; next try at the next scheduled time")]
    private static partial void LogScheduleFailed(ILogger logger, Exception exception, string schema, string job);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unfinished runs of {Schema} could not be marked interrupted; retrying in a minute")]
    private static partial void LogMarkInterruptedFailed(ILogger logger, Exception exception, string schema);

    [LoggerMessage(Level = LogLevel.Error, Message = "Job {Schema}/{Job} run {RunId} could not be saved (attempt {Attempt})")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception, string schema, string job, int runId, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Clients of {Schema} could not be told about a job run")]
    private static partial void LogNotifyFailed(ILogger logger, Exception exception, string schema);
}
