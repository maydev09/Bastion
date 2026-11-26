namespace Bastion.Modules.Housing.Domain;

// Table housing.rent_estimate: how the estimated rent of a monthly cost was made, in the cost's currency. RatePerM2 is the rate it used (after the
// area correction); Basis is the most local group of offers it took a rate from, with that group's rate and size; Neighbors
// are the nearest offers it took a rate from, with their median rate and the distance to the farthest one.
public sealed record RentEstimate(
    int OfferId,
    decimal RatePerM2,
    RentBasis? Basis,
    decimal? BasisPerM2,
    int? SampleSize,
    int Neighbors,
    decimal? NeighborsPerM2,
    decimal? NeighborsKm);
