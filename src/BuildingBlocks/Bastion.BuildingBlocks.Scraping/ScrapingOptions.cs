namespace Bastion.BuildingBlocks.Scraping;

public sealed class ScrapingOptions
{
    public const string Section = "Scraping";

    public bool RequireVpn { get; set; } = true;

    public string VpnInterfacePrefixes { get; set; } = "wg,tun,proton";

    public IReadOnlyList<string> VpnInterfacePrefixList =>
        VpnInterfacePrefixes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public double MinDelaySeconds { get; set; } = 3;

    public double MaxDelaySeconds { get; set; } = 6;

    public double TimeoutSeconds { get; set; } = 30;

    public double RobotsCacheHours { get; set; } = 24;
}
