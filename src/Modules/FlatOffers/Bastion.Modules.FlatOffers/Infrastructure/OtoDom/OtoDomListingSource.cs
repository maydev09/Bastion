using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.FlatOffers.Application.Abstractions;
using Bastion.Modules.FlatOffers.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.FlatOffers.Infrastructure.OtoDom;

internal sealed class OtoDomListingSource(ScrapingClient client, IOptions<OtoDomOptions> options) : IListingSource
{
    public const string ParseFailed = "flatOffers.parseFailed";

    public OfferSource Source => OfferSource.OtoDom;

    public async Task<ListingFetchResult> FetchListingPageAsync(CancellationToken cancellationToken)
    {
        var url = options.Value.ListingUrl
            ?? throw new InvalidOperationException("Modules:FlatOffers:OtoDom:ListingUrl is not configured.");

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
