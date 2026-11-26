namespace Bastion.Modules.Housing.Domain;

// Table housing.rent_rate: the median admin rent per m² of the offers in one group (Basis) that state a plausible rent.
// The keys that do not belong to the basis are null; DistrictId is the district, or the neighborhood for the
// neighborhood groups.
public sealed record RentRate(
    RentBasis Basis,
    Voivodeship? Voivodeship,
    int? PlaceId,
    int? DistrictId,
    BuildingType? BuildingType,
    decimal MedianPerM2,
    int SampleSize,
    DateTimeOffset CalculatedAt);
