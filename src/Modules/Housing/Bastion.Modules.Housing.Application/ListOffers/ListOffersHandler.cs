using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.ListOffers;

// The size of a place is computed here, so changed thresholds apply at once; the size filter uses the same thresholds.
// The price per m² is computed here too; the list query sorts by the same expression.
public sealed class ListOffersHandler(IOfferRepository repository, IOptions<PlaceSizeOptions> sizes, TimeProvider time)
{
    public async Task<PagedResult<OfferListItem>> Handle(
        OfferFilter filter, PageRequest page, OfferSort sort, SortDirection direction, CancellationToken cancellationToken)
    {
        var placeSizes = filter.PlaceSizes.Distinct().Select(sizes.Value.RangeOf).ToList();
        var offers = await repository.ListAsync(filter, placeSizes, page, sort, direction, cancellationToken);
        return offers with { Items = offers.Items.Select(AddDerivedValues).ToList() };
    }

    public Task<OfferFacets> CountFacets(OfferFilter filter, CancellationToken cancellationToken)
    {
        var placeSizes = filter.PlaceSizes.Distinct().Select(sizes.Value.RangeOf).ToList();
        var allSizes = Enum.GetValues<PlaceSize>().ToDictionary(size => size, sizes.Value.RangeOf);
        return repository.CountFacetsAsync(filter, placeSizes, allSizes, cancellationToken);
    }

    public Task<OfferSummary> Summarize(OfferFilter filter, CancellationToken cancellationToken)
    {
        var placeSizes = filter.PlaceSizes.Distinct().Select(sizes.Value.RangeOf).ToList();
        return repository.SummarizeAsync(filter, placeSizes, time.GetUtcNow(), cancellationToken);
    }

    public async Task<OfferListItem?> Get(int id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken) is { } offer ? AddDerivedValues(offer) : null;

    private OfferListItem AddDerivedValues(OfferListItem offer) => offer with
    {
        PlaceSize = offer.Population is { } population ? sizes.Value.Of(offer.PlaceKind, population) : null,
        PricePerM2 = offer is { Price: { } price, AreaM2: { } area } && area > 0 ? Math.Round(price / area, 2) : null,
    };
}
