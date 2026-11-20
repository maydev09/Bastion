using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Application.RawCaptures;

namespace Bastion.Modules.Housing.Application.ReparseOffers;

public sealed class ReparseOffersHandler(
    OfferSources sources, RawCaptureService rawCaptures, IOfferRepository repository, PlaceMatchers matchers)
{
    public async Task<ReparseResult> Handle(CancellationToken cancellationToken)
    {
        // Oldest first: a deleted offer gets its creation time back; older pages never overwrite newer data.
        var captures = await rawCaptures.ListAsync(cancellationToken);
        var resolver = new PlaceResolver(sources, await matchers.GetAsync(cancellationToken));
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

            var offers = parsed.Offers.Select(resolver.Link).ToList();
            statistics.Add(await repository.SaveAsync(offers, capture, cancellationToken), parsed.SkippedCount);
        }

        return new ReparseResult(
            captures.Count, failed, statistics.Added, statistics.Updated, statistics.Unchanged, statistics.Skipped);
    }
}
