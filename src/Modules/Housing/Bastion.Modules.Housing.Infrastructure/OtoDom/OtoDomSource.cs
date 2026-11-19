using System.Text;
using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public sealed class OtoDomSource(ScrapingClient client, IOptions<OtoDomOptions> options, IOptions<RawCaptureOptions> rawCaptures)
    : IOfferSource
{
    public const string ParseFailed = "housing.parseFailed";

    public Source Source => Source.OtoDom;

    public async Task<FetchResult> FetchListingPageAsync(int number, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetAsync(ListingPageUrl(number), cancellationToken);
            return new FetchResult(
                new RawPage(response.Url, CaptureKind.ListingPage, response.ContentType, response.StatusCode, response.Content));
        }
        catch (ScrapingException e)
        {
            var kind = e.Code == ScrapingException.VpnRequired ? FailureKind.Unavailable : FailureKind.Failed;
            return FetchResult.Failed(new Failure(e.Code, e.Message, kind));
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

    // The listing URL already has a query (sorting, page size); the page number is added to it.
    private Uri ListingPageUrl(int number) => new($"{options.Value.ListingUrl}&page={number}");
}
