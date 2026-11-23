using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

// What the offer list is narrowed to; an empty list or a null leaves that filter off.
// An offer without the filtered value (no price, no place) is left out.
public sealed record OfferFilter
{
    // The location: offers in any of the voivodeships or any of the places.
    public IReadOnlyList<Voivodeship> Voivodeships { get; init; } = [];

    public IReadOnlyList<int> PlaceIds { get; init; } = [];

    public IReadOnlyList<PlaceSize> PlaceSizes { get; init; } = [];

    // The currency of the price bounds; an offer priced in another currency is left out.
    public const Currency PriceCurrency = Currency.Pln;

    // Bounds are inclusive.
    public decimal? PriceMin { get; init; }

    public decimal? PriceMax { get; init; }

    public decimal? AreaMin { get; init; }

    public decimal? AreaMax { get; init; }

    public int? RoomsMin { get; init; }

    public int? RoomsMax { get; init; }

    // Offers whose page says so; an offer not known yet or not stated is left out.
    public Answer? Furnished { get; init; }

    // Offers with all of them.
    public IReadOnlyList<Feature> Features { get; init; } = [];

    // Offers with (true) or without (false) any recorded change.
    public bool? Changed { get; init; }

    // Offers whose recorded history has a lower price than the one before.
    public bool PriceCut { get; init; }
}
