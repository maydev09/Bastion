namespace Bastion.BuildingBlocks.Jobs;

// A job of a module, when it runs on its own and how its latest run went. DailyAt is a time of day in JobList.TimeZone,
// Every an interval; neither: the job runs only when started by hand.
public sealed record JobListItem(string Name, TimeOnly? DailyAt, TimeSpan? Every, JobRun? LatestRun);
