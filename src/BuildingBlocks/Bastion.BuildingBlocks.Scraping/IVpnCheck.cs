namespace Bastion.BuildingBlocks.Scraping;

internal interface IVpnCheck
{
    Task EnsureTunnelAsync(string host, CancellationToken cancellationToken);
}
