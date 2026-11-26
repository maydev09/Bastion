using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application.ReparseOffers;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// Reads the offers again from the stored pages, without fetching anything; started by hand after a parser fix.
public sealed class ReparseOffersJob(ReparseOffersHandler handler) : IJob
{
    public const string Name = "ReparseOffers";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(cancellationToken);
        return new JobSummary(new Dictionary<string, long>
        {
            ["pages"] = result.CaptureCount,
            ["failedPages"] = result.FailedCount,
            ["added"] = result.AddedCount,
            ["updated"] = result.UpdatedCount,
            ["unchanged"] = result.UnchangedCount,
            ["skipped"] = result.SkippedCount,
            ["detailPages"] = result.DetailCount,
        });
    }
}
