using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

// An offer as a listing page shows it, in plain values.
// DistrictName and NeighborhoodName are the portal's names of the district and of the neighborhood inside it,
// which PlaceResolver turns into Offer.DistrictId.
public sealed record ParsedOffer(
    string ExternalId,
    string Title,
    Uri Url,
    PropertyType PropertyType,
    Transaction Transaction,
    DateTimeOffset ExternalCreatedAt,
    decimal? PriceAmount = null,
    Currency? PriceCurrency = null,
    decimal? AreaM2 = null,
    int? Rooms = null,
    DateTimeOffset? ExternalUpdatedAt = null,
    string? LocationKey = null,
    decimal? RentAmount = null,
    Currency? RentCurrency = null,
    int? Floor = null,
    IReadOnlyList<Feature>? Features = null,
    string? DistrictName = null,
    string? NeighborhoodName = null)
{
    // An amount is money only with its currency.
    public Offer ToDomain(Source source) => new(
        source,
        ExternalId,
        Title,
        Url,
        PropertyType,
        Transaction,
        ExternalCreatedAt,
        PriceAmount is { } price && PriceCurrency is { } priceCurrency ? new Money(price, priceCurrency) : null,
        AreaM2,
        Rooms,
        ExternalUpdatedAt,
        LocationKey,
        Rent: RentAmount is { } rent && RentCurrency is { } rentCurrency ? new Money(rent, rentCurrency) : null,
        Floor: Floor,
        Features: Features);
}
