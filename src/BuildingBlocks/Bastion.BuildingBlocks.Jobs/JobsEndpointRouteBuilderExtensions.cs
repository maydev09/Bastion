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
