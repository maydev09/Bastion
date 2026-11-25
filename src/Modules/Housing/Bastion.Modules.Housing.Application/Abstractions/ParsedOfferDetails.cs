using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

// What a detail page shows, in plain values.
// RejectedValues: implausible portal values by column name (build_year -> "195014").
public sealed record ParsedOfferDetails(
    decimal? RentAmount = null,
    Currency? RentCurrency = null,
    decimal? DepositAmount = null,
    Currency? DepositCurrency = null,
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
    IReadOnlyList<Feature>? Features = null,
    Answer? Furnished = null,
    IReadOnlyDictionary<string, string>? RejectedValues = null)
{
    // An amount is money only with its currency.
    public OfferDetails ToDomain() => new(
        RentAmount is { } rent && RentCurrency is { } rentCurrency ? new Money(rent, rentCurrency) : null,
        DepositAmount is { } deposit && DepositCurrency is { } depositCurrency ? new Money(deposit, depositCurrency) : null,
        AvailableFrom,
        Floor,
        BuildingFloors,
        BuildYear,
        BuildingType,
        Heating,
        Condition,
        Latitude,
        Longitude,
        LocationRadiusM,
        Description,
        Features,
        Furnished,
        RejectedValues);
}
