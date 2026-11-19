using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.ImportOffers;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// Imports offers of every source; the first failing source fails the run.
public abstract class ImportJob(OfferSources sources, ImportOffersHandler handler, ImportMode mode) : IJob
{
    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        int pages = 0, added = 0, updated = 0, unchanged = 0, skipped = 0, missed = 0, removed = 0;
        foreach (var source in sources.All)
        {
            var outcome = await handler.Handle(source, mode, cancellationToken);
            if (outcome.Failure is { } failure)
            {
                throw new JobFailedException(failure.Code, failure.Detail);
            }

            var result = outcome.Result!;
            pages += result.PageCount;
            added += result.AddedCount;
            updated += result.UpdatedCount;
            unchanged += result.UnchangedCount;
            skipped += result.SkippedCount;
            missed += result.MissedCount;
            removed += result.RemovedCount;
        }

        var counts = new Dictionary<string, long>
        {
            ["pages"] = pages,
            ["added"] = added,
            ["updated"] = updated,
            ["unchanged"] = unchanged,
            ["skipped"] = skipped,
        };
        if (mode == ImportMode.FullScan)
        {
            counts["missed"] = missed;
            counts["removed"] = removed;
        }

        return new JobSummary(counts);
    }
}
