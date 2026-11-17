using System.ComponentModel.DataAnnotations;

namespace Bastion.BuildingBlocks.Scraping;

public sealed class ScrapingOptions
{
    public const string Section = "Scraping";

    public bool RequireVpn { get; set; } = true;

    // One comma-separated value on purpose, not a TOML array: configuration merges arrays by index
    // across files, so a local override with fewer items could not remove a prefix.
    [Required]
    public string VpnInterfacePrefixes { get; set; } = "wg,tun,proton";

    public IReadOnlyList<string> VpnInterfacePrefixList =>
        VpnInterfacePrefixes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    [Range(0, 3600)]
    public double MinDelaySeconds { get; set; } = 3;

    [Range(0, 3600)]
    public double MaxDelaySeconds { get; set; } = 6;

    [Range(1, 600)]
    public double TimeoutSeconds { get; set; } = 30;

    [Range(0, 720)]
    public double RobotsCacheHours { get; set; } = 24;
}
