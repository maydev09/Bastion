namespace Bastion.Modules.Housing.Application.ListOffers;

// The column the offer list is sorted by; offers without a value come last in both directions.
public enum OfferSort
{
    FirstSeen = 1,
    Price = 2,
    PricePerM2 = 3,
    AreaM2 = 4,
    Rooms = 5,
    // The stated rent, or the estimated one where the monthly cost uses an estimate.
    Rent = 6,
    TotalCost = 7,
}
