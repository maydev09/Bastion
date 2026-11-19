using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IOfferRepository
{
    // Every saved offer counts as seen at the time of the capture.
    Task<SaveCounts> SaveAsync(IReadOnlyList<Offer> offers, RawCapture capture, CancellationToken cancellationToken);

    // Called after a complete full scan: listed offers not seen since it started miss one more scan.
    Task<MissedCounts> MarkMissedAsync(Source source, DateTimeOffset scanStartedAt, int missedScansToRemove, CancellationToken cancellationToken);

    Task<PagedResult<OfferListItem>> ListAsync(PageRequest page, CancellationToken cancellationToken);
}
