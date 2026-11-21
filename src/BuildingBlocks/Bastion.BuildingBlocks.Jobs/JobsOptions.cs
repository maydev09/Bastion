using System.ComponentModel.DataAnnotations;

namespace Bastion.BuildingBlocks.Jobs;

// [Jobs], shared by the jobs of all modules.
public sealed class JobsOptions
{
    public const string Section = "Jobs";

    [Required]
    public string TimeZone { get; set; } = "";

    // False: no job runs on its schedule, only when started from the UI.
    public bool Scheduled { get; set; } = true;
}
