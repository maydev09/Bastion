using Bastion.Modules.FlatOffers.Application.ListOffers;
using Bastion.Modules.FlatOffers.Domain;
using Bastion.Platform.Abstractions;

namespace Bastion.Modules.FlatOffers.Application.Abstractions;

internal interface IOfferStore
{
    Task<SaveCounts> SaveAsync(OfferSource source, IReadOnlyList<ListingOffer> offers, DateTimeOffset now, CancellationToken cancellationToken);

    Task<PagedResult<OfferListItem>> ListAsync(PageRequest page, CancellationToken cancellationToken);
}

internal sealed record SaveCounts(int Added, int Updated, int Unchanged);
