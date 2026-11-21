using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.FetchDetails;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// Fetches detail pages of offers that need them, for every source; the first failing source fails the run.
public sealed class FetchDetailsJob(OfferSources sources, FetchDetailsHandler handler) : IJob
{
    public const string Name = "FetchDetails";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        int fetched = 0, failed = 0, retried = 0;
        foreach (var source in sources.All)
        {
            var outcome = await handler.Handle(source, cancellationToken);
            if (outcome.Failure is { } failure)
            {
                throw new JobFailedException(failure.Code, failure.Detail);
            }

            fetched += outcome.Result!.FetchedCount;
            failed += outcome.Result.FailedCount;
            retried += outcome.Result.RetriedCount;
        }

        return new JobSummary(new Dictionary<string, long>
        {
            ["fetched"] = fetched,
            ["failed"] = failed,
            ["retried"] = retried,
        });
    }
}
