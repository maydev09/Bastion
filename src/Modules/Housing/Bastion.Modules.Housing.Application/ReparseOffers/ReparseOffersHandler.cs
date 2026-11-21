using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ReparseOffers;

public sealed class ReparseOffersHandler(
    OfferSources sources, RawCaptureService rawCaptures, IOfferRepository repository, IPlaceRepository places)
{
    public async Task<ReparseResult> Handle(CancellationToken cancellationToken)
    {
        // Oldest first: a deleted offer gets its creation time back; older pages never overwrite newer data.
        // Old pages show no new changes, so no changes are recorded.
        var captures = await rawCaptures.ListAsync(cancellationToken);
        var resolver = new PlaceResolver(sources, places);
        var statistics = new SaveStatistics();
        int failed = 0, detailCount = 0;
        foreach (var capture in captures)
        {
            var page = await rawCaptures.ReadAsync(capture, cancellationToken);
            if (page is null || !sources.TryGet(capture.SourceId, out var offerSource))
            {
                failed++;
                continue;
            }

            if (capture.Kind == CaptureKind.DetailPage)
            {
                var details = offerSource.ParseDetails(page);
                if (details.Failure is not null
                    || !await repository.SaveDetailsAsync(capture, details.ExternalId!, details.Details!, recordChanges: false, cancellationToken))
                {
                    failed++;
                    continue;
                }

                detailCount++;
                continue;
            }

            var parsed = offerSource.Parse(page);
            if (parsed.Failure is not null)
            {
                failed++;
                continue;
            }

            var offers = await resolver.LinkAsync(parsed.Offers, cancellationToken);
            statistics.Add(await repository.SaveAsync(offers, capture, recordChanges: false, cancellationToken), parsed.SkippedCount);
        }

        return new ReparseResult(
            captures.Count, failed, statistics.Added, statistics.Updated, statistics.Unchanged, statistics.Skipped, detailCount);
    }
}
