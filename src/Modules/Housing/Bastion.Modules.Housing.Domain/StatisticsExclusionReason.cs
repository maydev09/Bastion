namespace Bastion.Modules.Housing.Domain;

// Why an offer is left out of the statistics: it is not a flat for rent at a monthly price, or its numbers are wrong.
// Stored as integer in housing.statistics_exclusion.reason; never change the values.
public enum StatisticsExclusionReason
{
    // A room in a shared flat ("Pokój do wynajęcia"): the price is for one room.
    Room = 1,

    // A parking space or a garage, not a flat.
    Parking = 2,

    // The area cannot be right: below a few m², equal to the price, or hundreds of m² at a few zł per m² (a typo).
    ImplausibleArea = 3,

    // The price cannot be a monthly rent: a token amount (1 zł) or a sale price (thousands of zł per m²).
    ImplausiblePrice = 4,

    // A price per m² outside the fences of its group, and the description tells of an auction or a tender: the price is
    // a one-time amount.
    Auction = 5,

    // Outside the fences, and the offer is workers' quarters or priced per person or bed.
    PerPerson = 6,

    // Outside the fences, and the flat is rented for renovating it ("najem za remont").
    RentForRenovation = 7,

    // Outside the fences, with no reason found in the offer: a typo or a very unusual flat.
    UnusualPrice = 8,
}
