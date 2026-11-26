using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

// How an estimated rent was made: Rent = RatePerM2 × area, where RatePerM2 = (Neighbors × NeighborsPerM2 + 10 × BasisPerM2)
// / (Neighbors + 10), times (area / 46 m²) ^ −0.18 as the rent per m² falls with the area. Neighbors are the nearest
// listed offers of the same building type with a rent, up to 10 within 2 km (NeighborsKm: the farthest one). Basis is the
// most local group of offers with a rent (from the whole country down to the neighborhood), SampleSize its offers;
// BasisPerM2 blends it with the wider groups by their sizes. Rates are in zł per m² a month.
public sealed record RentEstimateItem
{
    public required decimal RatePerM2 { get; init; }

    public RentBasis? Basis { get; init; }

    public decimal? BasisPerM2 { get; init; }

    public int? SampleSize { get; init; }

    public required int Neighbors { get; init; }

    public decimal? NeighborsPerM2 { get; init; }

    public decimal? NeighborsKm { get; init; }
}
