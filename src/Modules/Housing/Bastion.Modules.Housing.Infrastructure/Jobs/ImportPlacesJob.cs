using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application.ImportPlaces;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

public sealed class ImportPlacesJob(ImportPlacesHandler handler) : IJob
{
    public const string Name = "ImportPlaces";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        var outcome = await handler.Handle(cancellationToken);
        if (outcome.Failure is { } failure)
        {
            throw new JobFailedException(failure.Code, failure.Detail);
        }

        var result = outcome.Result!;
        return new JobSummary(new Dictionary<string, long>
        {
            ["places"] = result.PlaceCount,
            ["added"] = result.AddedCount,
            ["updated"] = result.UpdatedCount,
        });
    }
}
