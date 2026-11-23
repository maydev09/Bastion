using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.FetchDetails;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// Fetches detail pages of offers that need them, for every source; the first failing source fails the run,
// which still keeps the counts of the work done until then.
public sealed class FetchDetailsJob(OfferSources sources, FetchDetailsHandler handler) : IJob
{
    public const string Name = "FetchDetails";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        int fetched = 0, failed = 0, removed = 0, retried = 0;
        foreach (var source in sources.All)
        {
            var outcome = await handler.Handle(source, cancellationToken);
            if (outcome.Result is { } result)
            {
                fetched += result.FetchedCount;
                failed += result.FailedCount;
                removed += result.RemovedCount;
                retried += result.RetriedCount;
            }

            if (outcome.Failure is { } failure)
            {
                throw new JobFailedException(failure.Code, failure.Detail) { Summary = BuildSummary(fetched, failed, removed, retried) };
            }
        }

        return BuildSummary(fetched, failed, removed, retried);
    }

    private static JobSummary BuildSummary(int fetched, int failed, int removed, int retried) =>
        new(new Dictionary<string, long>
        {
            ["fetched"] = fetched,
            ["failed"] = failed,
            ["removed"] = removed,
            ["retried"] = retried,
        });
}
