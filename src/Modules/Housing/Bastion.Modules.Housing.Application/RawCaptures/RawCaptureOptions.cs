using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.RawCaptures;

public sealed class RawCaptureOptions
{
    public const string Section = "RawCaptures";

    [Required]
    public string Path { get; set; } = "";

    [Required]
    public string AnonymizationMask { get; set; } = "***";
}
