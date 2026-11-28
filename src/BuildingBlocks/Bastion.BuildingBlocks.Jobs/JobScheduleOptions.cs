namespace Bastion.BuildingBlocks.Jobs;

// [Modules.<Module>.Jobs.<name>]: when the job runs on its own and how long a run may work. The job's own settings sit in
// the same section, read by the module.
public sealed class JobScheduleOptions
{
    public const string Section = "Jobs";

    public static readonly TimeSpan MinEvery = TimeSpan.FromMinutes(1);

    // At most one of the two; without either the job runs only when started from the UI.
    public TimeOnly? DailyAt { get; set; }

    // The first run comes one interval after the application starts.
    public TimeSpan? Every { get; set; }

    // With Every: runs only from From until Until (exclusive), in the time zone of [Jobs]; Until "00:00" is midnight.
    // A window may pass midnight (From "22:00", Until "06:00").
    public TimeOnly? From { get; set; }

    public TimeOnly? Until { get; set; }

    // A run that has worked this long takes no new work and ends; what is left waits for the next run.
    public TimeSpan? TimeLimit { get; set; }
}
