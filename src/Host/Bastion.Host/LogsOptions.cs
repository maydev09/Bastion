using System.ComponentModel.DataAnnotations;

namespace Bastion.Host;

internal sealed class LogsOptions
{
    public const string Section = "Logs";

    [Required]
    public string Path { get; set; } = "logs";
}
