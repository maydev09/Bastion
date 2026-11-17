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

    public async Task<FetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetAsync(options.Value.ListingUrl, cancellationToken);
            return new FetchResult([
                new RawPage(response.Url, CaptureKind.ListingPage, response.ContentType, response.StatusCode, response.Content),
            ]);
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
            var (offers, skipped) = OtoDomListingParser.Parse(Encoding.UTF8.GetString(page.Content), options.Value.BaseUrl);
            return new ParseResult(offers, skipped);
        }
        catch (OtoDomParseException e)
        {
            return ParseResult.Failed(new Failure(ParseFailed, e.Message, FailureKind.Failed));
        }
    }
}
