namespace Bastion.BuildingBlocks.Jobs;

// Deadline: when the run must stop taking new work (its start plus the job's TimeLimit); null without a limit.
public sealed record JobContext(int RunId, JobTrigger Trigger, DateTimeOffset? Deadline);
