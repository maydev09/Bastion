namespace Bastion.BuildingBlocks.Jobs;

// [Modules.<Module>.Jobs.<job name>]
public sealed class JobScheduleOptions
{
    public const string Section = "Jobs";

    public static readonly TimeSpan MinEvery = TimeSpan.FromMinutes(1);

    // At most one of the two; without either the job runs only when started from the UI.
    public TimeOnly? DailyAt { get; set; }

    // The first run comes one interval after the application starts.
    public TimeSpan? Every { get; set; }
}
