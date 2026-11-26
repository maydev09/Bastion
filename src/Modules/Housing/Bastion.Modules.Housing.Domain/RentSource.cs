namespace Bastion.Modules.Housing.Domain;

// Where the admin rent in an offer's monthly cost comes from. Stored as integer in housing.monthly_cost.rent_source;
// never change the values.
public enum RentSource
{
    // The rent field of the offer, plausible for its price and area.
    Stated = 1,

    // The rent field says 0 or a token amount (e.g. 1 zł, as the portal's form requires a value) and the description says
    // nothing about the rent: taken as no rent (0).
    Nominal = 2,

    // Neither the field nor the description gives a plausible rent: the typical rent per m² of similar offers times the area.
    Estimated = 3,

    // The description states the amount (e.g. "czynsz administracyjny: 544 zł"); the rent field is empty, nominal or implausible.
    Description = 4,

    // The description says the rent is included in the price (e.g. "czynsz administracyjny w cenie"): the rent is 0.
    Included = 5,
}
