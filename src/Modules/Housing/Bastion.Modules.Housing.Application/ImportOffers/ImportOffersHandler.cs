using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.ImportOffers;

public sealed class ImportOffersHandler(
    OfferSources sources,
    RawCaptureService rawCaptures,
    IOfferRepository repository,
    IPlaceRepository places,
    IOptions<ImportOptions> options)
{
    public const string PageLimitReached = "housing.pageLimitReached";

    // One missed scan is not enough: an offer bumped during a scan moves to a page already read.
    public const int MissedScansToRemove = 2;

    public async Task<ImportOutcome> Handle(Source source, ImportMode mode, CancellationToken cancellationToken)
    {
        if (!sources.TryGet(source, out var offerSource))
        {
            return new ImportOutcome(null, new Failure(OfferSources.UnknownSource, $"No offer source is registered for {source}.", FailureKind.NotFound));
        }

        var resolver = new PlaceResolver(sources, places);
        var fullScan = mode == ImportMode.FullScan;
        var maxPages = fullScan ? options.Value.FullScanMaxPages : options.Value.MaxPages;
        var statistics = new SaveStatistics();
        var seen = new HashSet<string>();
        var pageCount = 0;
        var reachedLastPage = false;
        DateTimeOffset? startedAt = null;
        for (var number = 1; number <= maxPages; number++)
        {
            var fetch = await offerSource.FetchListingPageAsync(number, cancellationToken);
            if (fetch.Failure is not null)
            {
                return new ImportOutcome(BuildResult(new MissedCounts(0, 0)), fetch.Failure);
            }

            // Stored before parsing, so a page the parser cannot read is kept for fixing it.
            var page = offerSource.Anonymize(fetch.Page!);
            var capture = await rawCaptures.SaveAsync(source, page, cancellationToken);
            startedAt ??= capture.FetchedAt;
            var parsed = offerSource.Parse(page);
            if (parsed.Failure is not null)
            {
                return new ImportOutcome(BuildResult(new MissedCounts(0, 0)), parsed.Failure);
            }

            // New offers posted during the import push offers of a read page onto the next one; they are not saved twice.
            var offers = await resolver.LinkAsync(source, parsed.Offers.Where(o => seen.Add(o.ExternalId)), cancellationToken);
            var saved = await repository.SaveAsync(offers, capture, recordChanges: true, cancellationToken);
            statistics.Add(saved, parsed.SkippedCount);
            pageCount++;

            if (parsed.IsLastPage)
            {
                reachedLastPage = true;
                break;
            }

            // The listing is newest first, so after an offer known from an earlier import only known offers follow.
            if (!fullScan && saved.Added < offers.Count)
            {
                break;
            }
        }

        var missed = new MissedCounts(0, 0);
        if (fullScan)
        {
            // Offers after the limit were not read, so none can be counted as missing.
            if (!reachedLastPage)
            {
                return new ImportOutcome(BuildResult(missed), new Failure(PageLimitReached,
                    $"The full scan of {source} stopped at the limit of {maxPages} pages before the last page; no offer was marked removed.",
                    FailureKind.Failed));
            }

            missed = await repository.MarkMissedAsync(source, startedAt!.Value, MissedScansToRemove, cancellationToken);
        }

        return new ImportOutcome(BuildResult(missed), null);

        ImportResult BuildResult(MissedCounts missedCounts) => new(source, pageCount, statistics.Added, statistics.Updated,
            statistics.Unchanged, statistics.Skipped, missedCounts.Missed, missedCounts.Removed);
    }
}
