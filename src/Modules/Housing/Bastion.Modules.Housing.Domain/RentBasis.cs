namespace Bastion.Modules.Housing.Domain;

// A group of offers with a typical rent per m², from the widest to the most local. Stored as integer in
// housing.rent_rate.basis and housing.monthly_cost.rent_basis; never change the values.
public enum RentBasis
{
    Country = 1,
    Voivodeship = 2,
    VoivodeshipBuildingType = 3,
    Place = 4,
    PlaceBuildingType = 5,
    District = 6,
    DistrictBuildingType = 7,
    Neighborhood = 8,
    NeighborhoodBuildingType = 9,
}
