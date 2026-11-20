namespace Bastion.Modules.Housing.Domain;

// Size by population on separate scales for villages and towns; thresholds come from configuration.
public enum PlaceSize
{
    SmallVillage = 1,
    MediumVillage = 2,
    LargeVillage = 3,
    VeryLargeVillage = 4,
    VerySmallTown = 11,
    SmallTown = 12,
    MediumTown = 13,
    LargerTown = 14,
    LargeCity = 15,
    VeryLargeCity = 16,
    Metropolis = 17,
}
