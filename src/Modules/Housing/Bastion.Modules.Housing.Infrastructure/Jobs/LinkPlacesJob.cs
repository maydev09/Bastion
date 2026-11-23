using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application.LinkPlaces;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

public sealed class LinkPlacesJob(LinkPlacesHandler handler) : IJob
{
    public const string Name = "LinkPlaces";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        var result = await handler.Handle(cancellationToken);
        return new JobSummary(new Dictionary<string, long>
        {
            ["linkedOffers"] = result.LinkedOfferCount,
            ["unlinkedOffers"] = result.UnlinkedOfferCount,
        });
    }
}
