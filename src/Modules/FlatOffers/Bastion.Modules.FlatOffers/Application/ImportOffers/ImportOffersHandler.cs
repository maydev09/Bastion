using Bastion.Modules.FlatOffers.Application.Abstractions;
using Bastion.Modules.FlatOffers.Domain;

namespace Bastion.Modules.FlatOffers.Application.ImportOffers;

internal sealed class ImportOffersHandler(IListingSource source, IOfferStore store, TimeProvider time)
{
    public async Task<ImportOutcome> Handle(CancellationToken cancellationToken)
    {
        var page = await source.FetchListingPageAsync(cancellationToken);
        if (!page.Succeeded)
        {
            return new ImportOutcome(null, page.ErrorCode, page.ErrorDetail);
        }

        // Promoted offers can appear twice on one page. The page lists the newest offer first;
        // saving it last gives newer offers higher ids, which orders offers of one import by age.
        var offers = page.Offers.DistinctBy(o => o.ExternalId).Reverse().ToList();
        var counts = await store.SaveAsync(source.Source, offers, time.GetUtcNow(), cancellationToken);
        return new ImportOutcome(
            new ImportResult(source.Source, counts.Added, counts.Updated, counts.Unchanged, page.SkippedCount),
            null,
            null);
    }
}

internal sealed record ImportResult(OfferSource Source, int AddedCount, int UpdatedCount, int UnchangedCount, int SkippedCount);

internal sealed record ImportOutcome(ImportResult? Result, string? ErrorCode, string? ErrorDetail);
