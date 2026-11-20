namespace Bastion.Modules.Housing.Domain;

// The columns of housing.offer (and its housing.offer_feature rows) that a detail page fills.
// A separate record, because Offer holds the listing fields and its ListingHash covers only them.
// Null means unknown and never clears a stored value; Features replace the stored set.
public sealed record OfferDetails(
    Money? Rent = null,
    Money? Deposit = null,
    DateOnly? AvailableFrom = null,
    int? Floor = null,
    int? BuildingFloors = null,
    int? BuildYear = null,
    BuildingType? BuildingType = null,
    Heating? Heating = null,
    Condition? Condition = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    int? LocationRadiusM = null,
    string? Description = null,
    IReadOnlyList<Feature>? Features = null);
