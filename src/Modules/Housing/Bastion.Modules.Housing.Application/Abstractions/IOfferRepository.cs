using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IOfferRepository
{
    // Every saved offer counts as seen at the time of the capture.
    // recordChanges: content changes of stored offers go to housing.offer_change, dated by the capture.
    Task<SaveCounts> SaveAsync(IReadOnlyList<Offer> offers, RawCapture capture, bool recordChanges, CancellationToken cancellationToken);

    // Called after a complete full scan: listed offers not seen since it started miss one more scan.
    Task<MissedCounts> MarkMissedAsync(Source source, DateTimeOffset scanStartedAt, int missedScansToRemove, CancellationToken cancellationToken);

    // placeSizes: the population ranges of filter.PlaceSizes under the configured thresholds; a place matches any of them.
    Task<PagedResult<OfferListItem>> ListAsync(
        OfferFilter filter,
        IReadOnlyList<PopulationRange> placeSizes,
        PageRequest page,
        OfferSort sort,
        SortDirection direction,
        CancellationToken cancellationToken);

    // allSizes: the population range of every place size, to count offers by the size of their place.
    // countAddedSince: moments to count the offers first seen since, in place of the filter's own AddedSince.
    Task<OfferFacets> CountFacetsAsync(
        OfferFilter filter,
        IReadOnlyList<PopulationRange> placeSizes,
        IReadOnlyDictionary<PlaceSize, PopulationRange> allSizes,
        IReadOnlyList<DateTimeOffset> countAddedSince,
        CancellationToken cancellationToken);

    // now: the moment the days listed are counted to.
    Task<OfferSummary> SummarizeAsync(
        OfferFilter filter, IReadOnlyList<PopulationRange> placeSizes, DateTimeOffset now, CancellationToken cancellationToken);

    // Also a removed offer; null when there is no offer with the id.
    Task<OfferListItem?> GetAsync(int id, CancellationToken cancellationToken);

    // Newest offers first; removed offers and offers that failed maxFailedAttempts times for their content are left out,
    // and so are failures since runStartedAt, so one run does not retry the same offer.
    Task<IReadOnlyList<PendingDetails>> ListPendingDetailsAsync(
        Source source, int limit, int maxFailedAttempts, DateTimeOffset runStartedAt, CancellationToken cancellationToken);

    // Returns false when there is no offer with the external id. recordChanges as in SaveAsync.
    Task<bool> SaveDetailsAsync(RawCapture capture, string externalId, OfferDetails details, bool recordChanges, CancellationToken cancellationToken);

    Task MarkDetailsFetchedAsync(PendingDetails offer, DateTimeOffset at, CancellationToken cancellationToken);

    Task MarkDetailsFailedAsync(PendingDetails offer, DateTimeOffset at, string lastError, CancellationToken cancellationToken);

    // The portal says the offer is removed: marked as after missed scans, and listed again once the listing shows it.
    Task MarkRemovedAsync(PendingDetails offer, CancellationToken cancellationToken);

    Task<IReadOnlyList<OfferLocation>> ListLocationsAsync(CancellationToken cancellationToken);

    // Returns how many offers changed their place.
    Task<int> SetPlacesAsync(IReadOnlyList<PlaceLink> links, CancellationToken cancellationToken);
}
