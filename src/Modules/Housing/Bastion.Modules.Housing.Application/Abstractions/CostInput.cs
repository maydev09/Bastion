using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

// What the monthly cost of an offer is calculated from, and what an offer that states a rent adds to the typical rents.
// DistrictId is the district, also of an offer in a neighborhood (NeighborhoodId); Voivodeship is that of the place.
public sealed record CostInput
{
    public required int OfferId { get; init; }

    public decimal? Price { get; init; }

    public Currency? PriceCurrency { get; init; }

    public decimal? AreaM2 { get; init; }

    public decimal? Rent { get; init; }

    public Currency? RentCurrency { get; init; }

    public BuildingType? BuildingType { get; init; }

    public int? PlaceId { get; init; }

    public int? DistrictId { get; init; }

    public int? NeighborhoodId { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public Voivodeship? Voivodeship { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }
}
