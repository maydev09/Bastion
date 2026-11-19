using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.ImportOffers;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

public sealed class ImportOffersJob(OfferSources sources, ImportOffersHandler handler) : IJob
{
    public const string Name = "ImportOffers";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        int pages = 0, added = 0, updated = 0, unchanged = 0, skipped = 0;
        foreach (var source in sources.All)
        {
            var outcome = await handler.Handle(source, cancellationToken);
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
        }

        return new JobSummary(new Dictionary<string, long>
        {
            ["pages"] = pages,
            ["added"] = added,
            ["updated"] = updated,
            ["unchanged"] = unchanged,
            ["skipped"] = skipped,
        });
    }
}
