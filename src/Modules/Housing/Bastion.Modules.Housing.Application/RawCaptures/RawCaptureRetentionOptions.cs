using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.RawCaptures;

// The job's own settings in [Modules.Housing.Jobs.RawCaptureRetention], next to its schedule.
public sealed class RawCaptureRetentionOptions
{
    public const string Section = "Jobs:RawCaptureRetention";

    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;
}
