using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

// What the offer list is narrowed to; an empty list or a null leaves that filter off.
// An offer without the filtered value (no price, no place) is left out.
public sealed record OfferFilter
{
    // The location: offers in any of the voivodeships, places or districts, or without a place (WithoutPlace).
    public IReadOnlyList<Voivodeship> Voivodeships { get; init; } = [];

    public IReadOnlyList<int> PlaceIds { get; init; } = [];

    // A district takes in its neighborhoods. A picked district or neighborhood narrows its place to the picked ones, and a
    // picked neighborhood narrows its district the same way.
    public IReadOnlyList<int> DistrictIds { get; init; } = [];

    public IReadOnlyList<int> NeighborhoodIds { get; init; } = [];

    // Offers whose location matched no place.
    public bool WithoutPlace { get; init; }

    public IReadOnlyList<PlaceSize> PlaceSizes { get; init; } = [];

    // Offers in places whose highest seat is any of them, e.g. villages that are municipality seats.
    public IReadOnlyList<PlaceRole> PlaceRoles { get; init; } = [];

    // The currency of the price bounds; an offer priced in another currency is left out.
    public const Currency PriceCurrency = Currency.Pln;

    // Bounds are inclusive.
    public decimal? PriceMin { get; init; }

    public decimal? PriceMax { get; init; }

    // In PriceCurrency and rounded to two decimals, as an offer carries its price per m².
    public decimal? PricePerM2Min { get; init; }

    public decimal? PricePerM2Max { get; init; }

    // The total monthly cost (price plus admin rent) in PriceCurrency; an offer without one is left out.
    public decimal? TotalCostMin { get; init; }

    public decimal? TotalCostMax { get; init; }

    public decimal? AreaMin { get; init; }

    public decimal? AreaMax { get; init; }

    // The room counts: offers with any of Rooms, or with at least RoomsMin (e.g. 1, 2 or 4 and more).
    public IReadOnlyList<int> Rooms { get; init; } = [];

    public int? RoomsMin { get; init; }

    // Offers whose page says so; an offer not known yet or not stated is left out.
    public Answer? Furnished { get; init; }

    // Offers with all of them, or with any of them (FeatureMatch).
    public IReadOnlyList<Feature> Features { get; init; } = [];

    public FeatureMatch FeatureMatch { get; init; } = FeatureMatch.All;

    // Offers Bastion first saw at or after this moment (created_at, not the portal's date), so an offer
    // published earlier but collected later still counts as new.
    public DateTimeOffset? AddedSince { get; init; }

    // Leaves this offer out, e.g. to compare an offer with the others.
    public int? ExcludeOffer { get; init; }

    // Offers with (true) or without (false) any recorded change.
    public bool? Changed { get; init; }

    // Offers whose recorded history has a lower price than the one before.
    public bool PriceCut { get; init; }
}
