namespace Bastion.BuildingBlocks.Jobs;

public sealed record JobContext(int RunId, JobTrigger Trigger);
