using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.RawCaptures;

namespace Bastion.Modules.Housing.Application.ReparseOffers;

public sealed class ReparseOffersHandler(OfferSources sources, RawCaptureService rawCaptures, IOfferRepository repository)
{
    public async Task<ReparseResult> Handle(CancellationToken cancellationToken)
    {
        // Oldest first: a deleted offer gets its first-seen time back; older pages never overwrite newer data.
        var captures = await rawCaptures.ListAsync(cancellationToken);
        var statistics = new SaveStatistics();
        var failed = 0;
        foreach (var capture in captures)
        {
            var page = await rawCaptures.ReadAsync(capture, cancellationToken);
            if (page is null || !sources.TryGet(capture.SourceId, out var offerSource))
            {
                failed++;
                continue;
            }

            var parsed = offerSource.Parse(page);
            if (parsed.Failure is not null)
            {
                failed++;
                continue;
            }

            statistics.Add(await repository.SaveAsync(parsed.Offers, capture, cancellationToken), parsed.SkippedCount);
        }

        return new ReparseResult(
            captures.Count, failed, statistics.Added, statistics.Updated, statistics.Unchanged, statistics.Skipped);
    }
}
