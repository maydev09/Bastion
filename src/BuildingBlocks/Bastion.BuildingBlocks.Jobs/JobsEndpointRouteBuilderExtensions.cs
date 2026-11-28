using System.ComponentModel.DataAnnotations;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.BuildingBlocks.Jobs;

public static class JobsEndpointRouteBuilderExtensions
{
    public const string UnknownJob = "jobs.unknownJob";

    public static void MapJobs(this IEndpointRouteBuilder moduleGroup, string schema)
    {
        var runner = moduleGroup.ServiceProvider.GetRequiredKeyedService<JobRunner>(schema);
        var jobs = moduleGroup.MapGroup("/jobs");

        jobs.MapGet("/", async (CancellationToken cancellationToken) => TypedResults.Ok(await runner.ListAsync(cancellationToken)))
        .WithSummary("Returns every job of the module in its registered order: its schedule (dailyAt, a time of day in timeZone, "
            + "or every, an interval, only from from until until in timeZone when they are set; neither: started by hand only), "
            + "timeLimit (how long one run may work, null without a limit), whether schedules are on here (scheduled), its latest run "
            + "(null before the first one) and the server's current time.");

        jobs.MapGet("/runs", async Task<Results<Ok<PagedResult<JobRun>>, ProblemHttpResult>> (
            CancellationToken cancellationToken,
            string? job = null,
            JobRunStatus? status = null,
            JobTrigger? trigger = null,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            [Range(1, int.MaxValue)] int page = 1,
            [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize) =>
            job is not null && !runner.Has(job)
                ? NotFound(job)
                : TypedResults.Ok(await runner.ListRunsAsync(
                    new JobRunFilter(job, status, trigger, from, to), new PageRequest(page, pageSize), cancellationToken)))
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Returns the runs of the module's jobs, newest first: when each was queued, started and finished, what "
            + "started it (Manual or Schedule), how it ended, the error code of a failed run and the counts of its summary. "
            + "job, status and trigger narrow the list; from (inclusive) and to (exclusive) narrow it to runs queued in that "
            + "time range (ISO 8601; a time without an offset is UTC); page and pageSize page it.");

        jobs.MapPost("/{job}/runs", async Task<Results<Accepted<JobRun>, ProblemHttpResult>> (string job, CancellationToken cancellationToken) =>
            runner.Has(job)
                ? TypedResults.Accepted((string?)null, await runner.QueueAsync(job, JobTrigger.Manual, cancellationToken))
                : NotFound(job))
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Starts the job in the background, or returns its run that is already queued or running.");

        jobs.MapGet("/{job}/runs/latest", async Task<Results<Ok<LatestJobRun>, ProblemHttpResult>> (
            string job, TimeProvider time, CancellationToken cancellationToken) =>
            runner.Has(job)
                ? TypedResults.Ok(new LatestJobRun(await runner.LatestAsync(job, cancellationToken), time.GetUtcNow()))
                : NotFound(job))
        .ProducesProblem(StatusCodes.Status404NotFound)
        .WithSummary("Returns the latest run of the job (null before the first run) and the server's current time.");

        jobs.MapHub<JobsHub>("/hub").WithMetadata(new JobsModule(schema));
    }

    private static ProblemHttpResult NotFound(string job) =>
        TypedResults.Problem(
            detail: $"No job named '{job}' is registered.",
            statusCode: StatusCodes.Status404NotFound,
            title: "Unknown job",
            extensions: new Dictionary<string, object?> { ["code"] = UnknownJob });
}
