namespace Bastion.Modules.Housing.Domain;

// The group of offers a price fence is made of, each also by the number of rooms; an offer is compared with the most local
// one that has a fence. Stored as integer in housing.price_fence.basis; never change the values.
public enum PriceFenceBasis
{
    Country = 1,

    // Places of the same size in the whole country.
    PlaceSize = 2,

    // Places of the same size in the voivodeship.
    VoivodeshipPlaceSize = 3,

    Place = 4,
}
