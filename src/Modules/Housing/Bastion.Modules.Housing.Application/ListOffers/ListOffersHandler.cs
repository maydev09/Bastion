using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Platform.Abstractions;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.ListOffers;

// The size of a place is computed here, so changed thresholds apply at once.
public sealed class ListOffersHandler(IOfferRepository repository, IOptions<PlaceSizeOptions> sizes)
{
    public async Task<PagedResult<OfferListItem>> Handle(PageRequest page, CancellationToken cancellationToken)
    {
        var offers = await repository.ListAsync(page, cancellationToken);
        return offers with { Items = offers.Items.Select(WithSize).ToList() };
    }

    public async Task<OfferListItem?> Get(int id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken) is { } offer ? WithSize(offer) : null;

    private OfferListItem WithSize(OfferListItem offer) =>
        offer with { PlaceSize = offer.Population is { } population ? sizes.Value.Of(offer.PlaceKind, population) : null };
}
