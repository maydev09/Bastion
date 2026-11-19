namespace Bastion.BuildingBlocks.Jobs;

public sealed record JobRun(
    int Id,
    string Job,
    JobTrigger Trigger,
    JobRunStatus Status,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? ErrorCode,
    JobSummary? Summary);
