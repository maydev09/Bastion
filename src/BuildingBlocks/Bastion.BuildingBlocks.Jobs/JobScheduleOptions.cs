namespace Bastion.BuildingBlocks.Jobs;

// [Modules.<Module>.Jobs.<job name>]
public sealed class JobScheduleOptions
{
    public const string Section = "Jobs";

    // Without a time the job runs only when started from the UI.
    public TimeOnly? DailyAt { get; set; }
}
