using System.ComponentModel.DataAnnotations;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// The smallest population of each size; villages and towns have their own scales.
public sealed class PlaceSizeOptions : IValidatableObject
{
    public const string Section = "PlaceSizes";

    public VillageSizes Village { get; set; } = new();

    public TownSizes Town { get; set; } = new();

    // A place of unknown kind gets no size.
    public PlaceSize? Of(PlaceKind? kind, int population) => kind switch
    {
        PlaceKind.Village => population >= Village.VeryLarge ? PlaceSize.VeryLargeVillage
            : population >= Village.Large ? PlaceSize.LargeVillage
            : population >= Village.Medium ? PlaceSize.MediumVillage
            : PlaceSize.SmallVillage,
        PlaceKind.Town => population >= Town.Metropolis ? PlaceSize.Metropolis
            : population >= Town.VeryLarge ? PlaceSize.VeryLargeCity
            : population >= Town.Large ? PlaceSize.LargeCity
            : population >= Town.Larger ? PlaceSize.LargerTown
            : population >= Town.Medium ? PlaceSize.MediumTown
            : population >= Town.Small ? PlaceSize.SmallTown
            : PlaceSize.VerySmallTown,
        _ => null,
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!(0 < Village.Medium && Village.Medium < Village.Large && Village.Large < Village.VeryLarge))
        {
            yield return new ValidationResult("Village sizes must grow: 0 < Medium < Large < VeryLarge.");
        }

        if (!(0 < Town.Small && Town.Small < Town.Medium && Town.Medium < Town.Larger && Town.Larger < Town.Large
              && Town.Large < Town.VeryLarge && Town.VeryLarge < Town.Metropolis))
        {
            yield return new ValidationResult("Town sizes must grow: 0 < Small < Medium < Larger < Large < VeryLarge < Metropolis.");
        }
    }

    // Below Medium a village is small.
    public sealed class VillageSizes
    {
        public int Medium { get; set; } = 200;

        public int Large { get; set; } = 1_000;

        public int VeryLarge { get; set; } = 5_000;
    }

    // Below Small a town is very small.
    public sealed class TownSizes
    {
        public int Small { get; set; } = 5_000;

        public int Medium { get; set; } = 20_000;

        public int Larger { get; set; } = 50_000;

        public int Large { get; set; } = 100_000;

        public int VeryLarge { get; set; } = 200_000;

        public int Metropolis { get; set; } = 250_000;
    }
}
