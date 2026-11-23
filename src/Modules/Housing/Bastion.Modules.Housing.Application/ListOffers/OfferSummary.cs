namespace Bastion.Modules.Housing.Application.ListOffers;

// What the offers of a filter typically cost and offer; a value no offer has is null.
// Money is in PLN: offers priced in another currency are left out of it.
public sealed record OfferSummary
{
    public required int Count { get; init; }

    public Spread? Price { get; init; }

    public Spread? PricePerM2 { get; init; }

    public Spread? Rent { get; init; }

    public Spread? AreaM2 { get; init; }

    // How many days the offers have been listed so far, from the portal's date (or ours when the portal gives none).
    public Spread? DaysListed { get; init; }
}
