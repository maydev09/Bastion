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

    public int? Rooms { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? ExternalCreatedAt { get; init; }

    public DateTimeOffset? ExternalUpdatedAt { get; init; }

    public string? PlaceName { get; init; }

    public PlaceKind? PlaceKind { get; init; }

    public PlaceSize? PlaceSize { get; init; }

    public PlaceRole? PlaceRole { get; init; }

    public Voivodeship? Voivodeship { get; init; }

    public string? County { get; init; }

    public string? Municipality { get; init; }

    public int? Population { get; init; }

    public short? PopulationYear { get; init; }

    public PlaceSource? PopulationSource { get; init; }

    // Nearest places by tier (see NearbyTier), each tier nearest first.
    public IReadOnlyList<NearbyPlace> Nearby { get; init; } = [];
}
