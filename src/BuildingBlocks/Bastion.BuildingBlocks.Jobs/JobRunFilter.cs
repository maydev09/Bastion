namespace Bastion.BuildingBlocks.Jobs;

// Which runs the history lists; a null value does not narrow it. From is inclusive, To exclusive, both on the time a run
// was queued.
public sealed record JobRunFilter(string? Job, JobRunStatus? Status, JobTrigger? Trigger, DateTimeOffset? From, DateTimeOffset? To);
