namespace Bastion.Modules.Housing.Application.ListOffers;

// The column the offer list is sorted by; offers without a value come last in both directions.
public enum OfferSort
{
    FirstSeen = 1,
    Price = 2,
    PricePerM2 = 3,
    AreaM2 = 4,
    Rooms = 5,
    Rent = 6,
}
