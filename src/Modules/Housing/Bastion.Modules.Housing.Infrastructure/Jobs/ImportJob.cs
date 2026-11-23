using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.ImportOffers;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// Imports offers of every source; the first failing source fails the run, which still keeps the counts of the work
// done until then.
public abstract class ImportJob(OfferSources sources, ImportOffersHandler handler, ImportMode mode) : IJob
{
    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        int pages = 0, added = 0, updated = 0, unchanged = 0, skipped = 0, missed = 0, removed = 0;
        foreach (var source in sources.All)
        {
            var outcome = await handler.Handle(source, mode, cancellationToken);
            if (outcome.Result is { } result)
            {
                pages += result.PageCount;
                added += result.AddedCount;
                updated += result.UpdatedCount;
                unchanged += result.UnchangedCount;
                skipped += result.SkippedCount;
                missed += result.MissedCount;
                removed += result.RemovedCount;
            }

            if (outcome.Failure is { } failure)
            {
                throw new JobFailedException(failure.Code, failure.Detail)
                {
                    Summary = BuildSummary(pages, added, updated, unchanged, skipped, missed, removed),
                };
            }
        }

        return BuildSummary(pages, added, updated, unchanged, skipped, missed, removed);
    }

    private JobSummary BuildSummary(int pages, int added, int updated, int unchanged, int skipped, int missed, int removed)
    {
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
