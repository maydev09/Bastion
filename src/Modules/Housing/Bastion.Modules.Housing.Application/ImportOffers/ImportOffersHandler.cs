using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.ImportOffers;

public sealed class ImportOffersHandler(
    OfferSources sources,
    RawCaptureService rawCaptures,
    IOfferRepository repository,
    IOptions<ImportOptions> options)
{
    public const string UnknownSource = "housing.unknownSource";

    public async Task<ImportOutcome> Handle(Source source, CancellationToken cancellationToken)
    {
        if (!sources.TryGet(source, out var offerSource))
        {
            return new ImportOutcome(null, new Failure(UnknownSource, $"No offer source is registered for {source}.", FailureKind.NotFound));
        }

        var statistics = new SaveStatistics();
        var seen = new HashSet<string>();
        var pageCount = 0;
        for (var number = 1; number <= options.Value.MaxPages; number++)
        {
            var fetch = await offerSource.FetchListingPageAsync(number, cancellationToken);
            if (fetch.Failure is not null)
            {
                return new ImportOutcome(null, fetch.Failure);
            }

            // Stored before parsing, so a page the parser cannot read is kept for fixing it.
            var page = offerSource.Anonymize(fetch.Page!);
            var capture = await rawCaptures.SaveAsync(source, page, cancellationToken);
            var parsed = offerSource.Parse(page);
            if (parsed.Failure is not null)
            {
                return new ImportOutcome(null, parsed.Failure);
            }

            // New offers posted during the import push offers of a read page onto the next one; they are not saved twice.
            var offers = parsed.Offers.Where(o => seen.Add(o.ExternalId)).ToList();
            var saved = await repository.SaveAsync(offers, capture, cancellationToken);
            statistics.Add(saved, parsed.SkippedCount);
            pageCount++;

            // The listing is newest first, so after an offer known from an earlier import only known offers follow.
            if (parsed.IsLastPage || saved.Added < offers.Count)
            {
                break;
            }
        }

        var result = new ImportResult(source, pageCount, statistics.Added, statistics.Updated, statistics.Unchanged, statistics.Skipped);
        return new ImportOutcome(result, null);
    }
}
