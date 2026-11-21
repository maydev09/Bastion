using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IOfferRepository
{
    // Every saved offer counts as seen at the time of the capture.
    // logChanges: content changes of stored offers go to the change log, dated by the capture.
    Task<SaveCounts> SaveAsync(IReadOnlyList<Offer> offers, RawCapture capture, bool logChanges, CancellationToken cancellationToken);

    // Called after a complete full scan: listed offers not seen since it started miss one more scan.
    Task<MissedCounts> MarkMissedAsync(Source source, DateTimeOffset scanStartedAt, int missedScansToRemove, CancellationToken cancellationToken);

    Task<PagedResult<OfferListItem>> ListAsync(PageRequest page, CancellationToken cancellationToken);

    // Also a removed offer; null when there is no offer with the id.
    Task<OfferListItem?> GetAsync(int id, CancellationToken cancellationToken);

    // Newest offers first; removed offers and offers that failed maxFailedAttempts times for their content are left out,
    // and so are failures since runStartedAt, so one run does not retry the same offer.
    Task<IReadOnlyList<PendingDetails>> ListPendingDetailsAsync(
        Source source, int limit, int maxFailedAttempts, DateTimeOffset runStartedAt, CancellationToken cancellationToken);

    // Returns false when there is no offer with the external id. logChanges as in SaveAsync.
    Task<bool> SaveDetailsAsync(RawCapture capture, string externalId, OfferDetails details, bool logChanges, CancellationToken cancellationToken);

    Task MarkDetailsFetchedAsync(PendingDetails offer, DateTimeOffset at, CancellationToken cancellationToken);

    Task MarkDetailsFailedAsync(PendingDetails offer, DateTimeOffset at, string lastError, CancellationToken cancellationToken);

    Task<IReadOnlyList<OfferLocation>> ListLocationsAsync(CancellationToken cancellationToken);

    // Returns how many offers changed their place.
    Task<int> SetPlacesAsync(IReadOnlyList<PlaceLink> links, CancellationToken cancellationToken);
}
