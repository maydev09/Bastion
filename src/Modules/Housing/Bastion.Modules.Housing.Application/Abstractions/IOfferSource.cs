using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IOfferSource
{
    Source Source { get; }

    // Results pages are numbered from 1 and list the newest offers first.
    Task<FetchResult> FetchListingPageAsync(int number, CancellationToken cancellationToken);

    Task<FetchResult> FetchDetailPageAsync(Uri url, CancellationToken cancellationToken);

    RawPage Anonymize(RawPage page);

    // A listing page.
    ParseResult Parse(RawPage page);

    DetailsParseResult ParseDetails(RawPage page);

    // Turns the offer's LocationKey, as the parser stored it, into a hint for the place matcher.
    LocationHint? LocationHintOf(string locationKey);
}
