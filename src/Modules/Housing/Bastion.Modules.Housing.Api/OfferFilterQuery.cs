using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Bastion.Modules.Housing.Api;

// The filter query of the offer list and its counts.
// Repeated filters are repeated keys: ?voivodeship=Slaskie&voivodeship=Opolskie.
public sealed record OfferFilterQuery(
    [FromQuery] Voivodeship[]? voivodeship,
    [FromQuery] int[]? place,
    bool? withoutPlace,
    [FromQuery] PlaceSize[]? size,
    [FromQuery] Feature[]? feature,
    FeatureMatch? featureMatch,
    decimal? priceMin,
    decimal? priceMax,
    decimal? areaMin,
    decimal? areaMax,
    [FromQuery] int[]? rooms,
    int? roomsMin,
    Answer? furnished,
    DateTimeOffset? addedSince,
    bool? changed,
    bool? priceCut,
    int? excludeOffer)
{
    public OfferFilter ToFilter() => new()
    {
        Voivodeships = voivodeship ?? [],
        PlaceIds = place ?? [],
        WithoutPlace = withoutPlace ?? false,
        PlaceSizes = size ?? [],
        Features = feature ?? [],
        FeatureMatch = featureMatch ?? FeatureMatch.All,
        PriceMin = priceMin,
        PriceMax = priceMax,
        AreaMin = areaMin,
        AreaMax = areaMax,
        Rooms = rooms ?? [],
        RoomsMin = roomsMin,
        Furnished = furnished,
        AddedSince = addedSince,
        Changed = changed,
        PriceCut = priceCut ?? false,
        ExcludeOffer = excludeOffer,
    };
}
