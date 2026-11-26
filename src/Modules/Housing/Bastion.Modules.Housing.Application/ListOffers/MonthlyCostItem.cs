using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

// What an offer costs a month: Total = the price plus Rent, in Currency. Not all bills: what the admin rent covers
// (heating, water) differs per building.
public sealed record MonthlyCostItem
{
    public required decimal Total { get; init; }

    public required Currency Currency { get; init; }

    // The admin rent in Total.
    public required decimal Rent { get; init; }

    // Stated: the offer's rent field. Description: the amount the description states (RentQuote), as the field is empty,
    // nominal or implausible. Included: the description says the rent is in the price or there is none (0). Nominal: the field
    // says 0 or a token 1 zł and the description says nothing, taken as 0. Estimated: none of these (see Estimate).
    public required RentSource RentSource { get; init; }

    // The words of the description the rent was read from (Description, Included).
    public string? RentQuote { get; init; }

    // When the cost was last calculated: at once when the offer is saved, and for all offers every night.
    public required DateTimeOffset CalculatedAt { get; init; }

    // How an estimated rent was made; null for the other sources.
    public RentEstimateItem? Estimate { get; init; }
}
