using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

public sealed record OfferListItem
{
    public required int Id { get; init; }

    public required Source Source { get; init; }

    public required string Title { get; init; }

    public required string Url { get; init; }

    public required PropertyType PropertyType { get; init; }

    public required Transaction Transaction { get; init; }

    public decimal? Price { get; init; }

    public Currency? PriceCurrency { get; init; }

    public decimal? AreaM2 { get; init; }

    // In PriceCurrency; null without a price or an area.
    public decimal? PricePerM2 { get; init; }

    public int? Rooms { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? ExternalCreatedAt { get; init; }

    public DateTimeOffset? ExternalUpdatedAt { get; init; }

    // From Rent to DetailsFetchedAt only on the offer card (GetAsync); the list does not show them.
    public decimal? Rent { get; init; }

    public Currency? RentCurrency { get; init; }

    public decimal? Deposit { get; init; }

    public Currency? DepositCurrency { get; init; }

    public DateOnly? AvailableFrom { get; init; }

    public int? Floor { get; init; }

    public int? BuildingFloors { get; init; }

    public int? BuildYear { get; init; }

    public BuildingType? BuildingType { get; init; }

    public Heating? Heating { get; init; }

    public Condition? Condition { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public int? LocationRadiusM { get; init; }

    public string? Description { get; init; }

    public Answer? Furnished { get; init; }

    public IReadOnlyList<Feature> Features { get; init; } = [];

    // When the detail page was last fetched; null while it never was.
    public DateTimeOffset? DetailsFetchedAt { get; init; }

    public string? PlaceName { get; init; }

    public PlaceKind? PlaceKind { get; init; }

    public PlaceSize? PlaceSize { get; init; }

    public PlaceRole? PlaceRole { get; init; }

    public Voivodeship? Voivodeship { get; init; }

    public string? County { get; init; }

    public string? Municipality { get; init; }

    public int? Population { get; init; }

    public int? PopulationYear { get; init; }

    public PlaceSource? PopulationSource { get; init; }

    // Nearest places by tier (see NearbyTier), each tier nearest first.
    public IReadOnlyList<NearbyPlace> Nearby { get; init; } = [];

    // Only on the offer card; newest first.
    public IReadOnlyList<OfferChange> Changes { get; init; } = [];
}
