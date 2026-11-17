using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.RawCaptures;

public sealed class RawCaptureOptions
{
    public const string Section = "RawCaptures";

    [Required]
    public string Path { get; set; } = "";

    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;

    [Required]
    public string AnonymizationMask { get; set; } = "***";
}
