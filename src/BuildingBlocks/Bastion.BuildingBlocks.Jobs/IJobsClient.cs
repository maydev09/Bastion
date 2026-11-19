namespace Bastion.BuildingBlocks.Jobs;

public interface IJobsClient
{
    Task JobRunStarted(JobRun run);

    Task JobRunFinished(JobRun run);
}
