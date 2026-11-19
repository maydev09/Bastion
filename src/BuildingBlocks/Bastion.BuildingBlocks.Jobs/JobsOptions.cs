using System.ComponentModel.DataAnnotations;

namespace Bastion.BuildingBlocks.Jobs;

// [Jobs], shared by the jobs of all modules.
public sealed class JobsOptions
{
    public const string Section = "Jobs";

    [Required]
    public string TimeZone { get; set; } = "";
}
