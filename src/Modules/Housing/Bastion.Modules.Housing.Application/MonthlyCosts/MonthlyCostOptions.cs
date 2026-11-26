using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.MonthlyCosts;

// How a missing rent is estimated; the values were chosen by testing on the stored offers (2026-11-26), see RentEstimator.
public sealed class MonthlyCostOptions : IValidatableObject
{
    public const string Section = "MonthlyCosts";

    // A stated rent per m² outside this range is implausible (e.g. 20 zł, or 132 876 zł typed by mistake), and so is a rent
    // equal to the price (typed twice). Real rents go from about 2 zł/m² (a small fee) to about 50 zł/m² (luxury apartments).
    [Range(0, 1_000)]
    public decimal MinRentPerM2 { get; set; } = 1;

    [Range(0, 1_000)]
    public decimal MaxRentPerM2 { get; set; } = 100;

    // A stated rent below this is nominal: 0, or a token amount typed because the portal's form requires one (1 zł).
    [Range(0, 1_000)]
    public decimal NominalRentBelow { get; set; } = 10;

    // The nearest offers of the same building type an estimate looks at, and how far.
    [Range(1, 100)]
    public int Neighbors { get; set; } = 10;

    [Range(0.1, 50)]
    public decimal NeighborRadiusKm { get; set; } = 2;

    // How many offers the rate of the wider area counts as next to the neighbors' median.
    [Range(0, 1_000)]
    public int WiderAreaWeight { get; set; } = 10;

    // How many offers the rate of a wider group counts as next to the median of a more local one.
    [Range(0, 1_000)]
    public int WiderGroupWeight { get; set; } = 20;

    // The rent per m² falls with the area (a part of it is the same for every flat): the rate is multiplied by
    // (area / TypicalAreaM2) ^ AreaExponent.
    [Range(1, 1_000)]
    public decimal TypicalAreaM2 { get; set; } = 46;

    [Range(-1, 0)]
    public double AreaExponent { get; set; } = -0.18;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinRentPerM2 >= MaxRentPerM2)
        {
            yield return new ValidationResult("MinRentPerM2 must be below MaxRentPerM2.");
        }
    }
}
