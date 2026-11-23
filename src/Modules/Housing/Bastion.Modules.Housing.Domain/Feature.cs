namespace Bastion.Modules.Housing.Domain;

// An amenity: shown when the portal lists it, with no "no" (see Answer for attributes that have one).
// Stored as integer in housing.offer_feature.feature; never change or reuse the values.
// 8 was Furnished, now the offer's furnished column.
public enum Feature
{
    Balcony = 1,
    Terrace = 2,
    Garden = 3,
    Lift = 4,
    Basement = 5,
    Parking = 6,
    AirConditioning = 7,
    Dishwasher = 9,
    WashingMachine = 10,
    Fridge = 11,
    Stove = 12,
    Oven = 13,
    SeparateKitchen = 14,
    UsableRoom = 15,
    TwoStorey = 16,
    Internet = 17,
    CableTelevision = 18,
    Entryphone = 19,
    Monitoring = 20,
    ClosedArea = 21,
    AntiBurglaryDoor = 22,
    NonSmokersOnly = 23,
}
