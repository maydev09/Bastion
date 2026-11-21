using System.Text;
using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public sealed class OtoDomSource(ScrapingClient client, IOptions<OtoDomOptions> options, IOptions<RawCaptureOptions> rawCaptures)
    : IOfferSource
{
    public const string ParseFailed = "housing.parseFailed";

    public Source Source => Source.OtoDom;

    public Task<FetchResult> FetchListingPageAsync(int number, CancellationToken cancellationToken) =>
        FetchAsync(ListingPageUrl(number), CaptureKind.ListingPage, cancellationToken);

    public Task<FetchResult> FetchDetailPageAsync(Uri url, CancellationToken cancellationToken) =>
        FetchAsync(url, CaptureKind.DetailPage, cancellationToken);

    private async Task<FetchResult> FetchAsync(Uri url, CaptureKind kind, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetAsync(url, cancellationToken);
            return new FetchResult(new RawPage(response.Url, kind, response.ContentType, response.StatusCode, response.Content));
        }
        catch (ScrapingException e)
        {
            // Only an HTTP error or a refused URL is about the page itself; the rest is about reaching the portal at all.
            var failureKind = e.Code switch
            {
                ScrapingException.HttpError when e.StatusCode is 404 or 410 => FailureKind.Gone,
                ScrapingException.HttpError or ScrapingException.DisallowedByRobots => FailureKind.Failed,
                ScrapingException.RateLimited => FailureKind.RateLimited,
                _ => FailureKind.Unavailable,
            };
            return FetchResult.Failed(new Failure(e.Code, e.Message, failureKind));
        }
    }

    public RawPage Anonymize(RawPage page) => page with
    {
        Content = Encoding.UTF8.GetBytes(
            OtoDomAnonymizer.Anonymize(Encoding.UTF8.GetString(page.Content), rawCaptures.Value.AnonymizationMask)),
    };

    public ParseResult Parse(RawPage page)
    {
        try
        {
            var (offers, skipped, isLastPage) = OtoDomListingParser.Parse(Encoding.UTF8.GetString(page.Content), options.Value.BaseUrl);
            return new ParseResult(offers, skipped, isLastPage);
        }
        catch (OtoDomParseException e)
        {
            return ParseResult.Failed(new Failure(ParseFailed, e.Message, FailureKind.Failed));
        }
    }

    public DetailsParseResult ParseDetails(RawPage page)
    {
        try
        {
            var (externalId, details) = OtoDomDetailParser.Parse(Encoding.UTF8.GetString(page.Content));
            return new DetailsParseResult(externalId, details);
        }
        catch (OtoDomParseException e)
        {
            return DetailsParseResult.Failed(new Failure(ParseFailed, e.Message, FailureKind.Failed));
        }
    }

    public LocationHint? LocationHintOf(string locationKey) => OtoDomLocation.HintOf(locationKey);

    // The listing URL already has a query (sorting, page size); the page number is added to it.
    private Uri ListingPageUrl(int number) => new($"{options.Value.ListingUrl}&page={number}");
}
