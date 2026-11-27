namespace Bastion.BuildingBlocks.Jobs;

// A job of a module, when it runs on its own and how its latest run went. DailyAt is a time of day in JobList.TimeZone,
// Every an interval, only from From until Until when they are set; neither: the job runs only when started by hand.
// TimeLimit: how long one run may work.
public sealed record JobListItem(
    string Name, TimeOnly? DailyAt, TimeSpan? Every, TimeOnly? From, TimeOnly? Until, TimeSpan? TimeLimit, JobRun? LatestRun);
