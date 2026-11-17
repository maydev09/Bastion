using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IOfferRepository
{
    Task<SaveCounts> SaveAsync(IReadOnlyList<Offer> offers, RawCapture capture, CancellationToken cancellationToken);

    Task<PagedResult<OfferListItem>> ListAsync(PageRequest page, CancellationToken cancellationToken);
}
