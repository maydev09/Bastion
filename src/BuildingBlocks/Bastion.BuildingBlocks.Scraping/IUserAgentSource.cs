namespace Bastion.BuildingBlocks.Scraping;

internal interface IUserAgentSource
{
    Task<string> GetAsync(CancellationToken cancellationToken);
}
