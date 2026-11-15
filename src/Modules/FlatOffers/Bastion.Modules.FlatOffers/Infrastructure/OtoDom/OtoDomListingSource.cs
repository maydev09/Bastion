using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.FlatOffers.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.FlatOffers.Infrastructure.OtoDom;

internal sealed class OtoDomListingSource(ScrapingClient client, IOptions<OtoDomOptions> options) : IListingSource
{
    public const string ParseFailed = "flatOffers.parseFailed";

    public string Name => "OtoDom";

    public async Task<ListingFetchResult> FetchSmokeTestPageAsync(CancellationToken cancellationToken)
    {
        var url = options.Value.SmokeTestUrl
            ?? throw new InvalidOperationException("Modules:FlatOffers:OtoDom:SmokeTestUrl is not configured.");

        string html;
        try
        {
            html = await client.GetStringAsync(url, cancellationToken);
        }
        catch (ScrapingException e)
        {
            return ListingFetchResult.Failed(e.Code, e.Message);
        }

        try
        {
            var (offers, skipped) = OtoDomListingParser.Parse(html, options.Value.BaseUrl);
            return new ListingFetchResult(offers, skipped);
        }
        catch (OtoDomParseException e)
        {
            return ListingFetchResult.Failed(ParseFailed, e.Message);
        }
    }
}
