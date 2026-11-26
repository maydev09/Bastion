namespace Bastion.Modules.Housing.Domain;

// Table housing.monthly_cost: what an offer costs a month, its price plus the admin rent (Rent, from RentSource), in Currency.
// A rent read from the description keeps the words it came from (RentQuote); an estimated one has a RentEstimate.
public sealed record MonthlyCost(
    int OfferId,
    decimal Rent,
    RentSource RentSource,
    decimal Total,
    Currency Currency,
    DateTimeOffset CalculatedAt,
    string? RentQuote = null);
