namespace Bastion.BuildingBlocks.Scraping;

public sealed class ScrapingOptions
{
    public const string Section = "Scraping";

    public bool RequireVpn { get; set; } = true;

    public IList<string> VpnInterfacePrefixes { get; } = ["wg", "tun", "proton"];

    public double MinDelaySeconds { get; set; } = 3;

    public double MaxDelaySeconds { get; set; } = 6;

    public double TimeoutSeconds { get; set; } = 30;

    public double RobotsCacheHours { get; set; } = 24;
}
