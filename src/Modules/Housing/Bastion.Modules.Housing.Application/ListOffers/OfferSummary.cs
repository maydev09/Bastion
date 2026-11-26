using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

// What the offers of a filter typically cost and offer; a value no offer has is null.
// Money is in PLN: offers priced in another currency are left out of it.
public sealed record OfferSummary
{
    public required int Count { get; init; }

    public Spread? Price { get; init; }

    public Spread? PricePerM2 { get; init; }

    // The admin rent of the offers whose rent is known: a plausible rent field, or an amount the description states.
    // Estimates, implausible values and offers without a rent (0, included) are left out.
    public Spread? Rent { get; init; }

    // The price plus the admin rent of the offers with a monthly cost (see OfferListItem.TotalCost).
    public Spread? TotalCost { get; init; }

    // The offers in TotalCost by where their rent comes from (see OfferListItem.RentSource); a source no offer has is missing.
    public required IReadOnlyDictionary<RentSource, int> RentSources { get; init; }

    public Spread? AreaM2 { get; init; }

    // How many days the offers have been listed so far, from the portal's date (or ours when the portal gives none).
    public Spread? DaysListed { get; init; }
}
