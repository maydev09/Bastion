namespace Bastion.BuildingBlocks.Jobs;

public interface IJob
{
    Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken);
}
