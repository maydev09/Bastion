using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ImportOffers;

public sealed class ImportOffersHandler(OfferSources sources, RawCaptureService rawCaptures, IOfferRepository repository)
{
    public const string UnknownSource = "housing.unknownSource";

    public async Task<ImportOutcome> Handle(Source source, CancellationToken cancellationToken)
    {
        if (!sources.TryGet(source, out var offerSource))
        {
            return new ImportOutcome(null, new Failure(UnknownSource, $"No offer source is registered for {source}.", FailureKind.NotFound));
        }

        await rawCaptures.DeleteExpiredAsync(cancellationToken);

        var fetch = await offerSource.FetchAsync(cancellationToken);
        if (fetch.Failure is not null)
        {
            return new ImportOutcome(null, fetch.Failure);
        }

        var statistics = new SaveStatistics();
        foreach (var fetched in fetch.Pages)
        {
            // Stored before parsing, so a page the parser cannot read is kept for fixing it.
            var page = offerSource.Anonymize(fetched);
            var capture = await rawCaptures.SaveAsync(source, page, cancellationToken);
            var parsed = offerSource.Parse(page);
            if (parsed.Failure is not null)
            {
                return new ImportOutcome(null, parsed.Failure);
            }

            statistics.Add(await repository.SaveAsync(parsed.Offers, capture, cancellationToken), parsed.SkippedCount);
        }

        var result = new ImportResult(source, statistics.Added, statistics.Updated, statistics.Unchanged, statistics.Skipped);
        return new ImportOutcome(result, null);
    }
}
