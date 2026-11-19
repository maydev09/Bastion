using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application.RawCaptures;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

public sealed class RawCaptureRetentionJob(RawCaptureService rawCaptures) : IJob
{
    public const string Name = "RawCaptureRetention";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        var result = await rawCaptures.DeleteExpiredAsync(cancellationToken);
        return new JobSummary(new Dictionary<string, long>
        {
            ["deletedCaptures"] = result.DeletedCount,
            ["freedBytes"] = result.FreedBytes,
        });
    }
}
