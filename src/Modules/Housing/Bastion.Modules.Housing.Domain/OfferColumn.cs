namespace Bastion.Modules.Housing.Domain;

// Columns of housing.offer whose implausible portal values a parser keeps in OfferDetails.RejectedValues.
public static class OfferColumn
{
    public const string BuildYear = "build_year";
    public const string BuildingFloors = "building_floors";
    public const string LocationRadiusM = "location_radius_m";
}
