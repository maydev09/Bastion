namespace Bastion.BuildingBlocks.Jobs;

public enum JobRunStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5,
    Interrupted = 6,
}
