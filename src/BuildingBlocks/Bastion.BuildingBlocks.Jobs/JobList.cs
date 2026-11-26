namespace Bastion.BuildingBlocks.Jobs;

// The jobs of a module in the order the module registers them. Scheduled: false when schedules are off on this instance
// (e.g. in development), so jobs run only when started by hand. ServerTime lets a client count how long a run takes
// without trusting its own clock.
public sealed record JobList(IReadOnlyList<JobListItem> Jobs, bool Scheduled, string TimeZone, DateTimeOffset ServerTime);
