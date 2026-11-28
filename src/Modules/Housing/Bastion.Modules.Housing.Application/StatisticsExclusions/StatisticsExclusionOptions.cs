using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.StatisticsExclusions;

// The limits of the rules that leave offers out of the statistics; the values were chosen on the stored offers (2026-11-27), see StatisticsExclusionService.
public sealed class StatisticsExclusionOptions : IValidatableObject
{
    public const string Section = "StatisticsExclusions";

    // The fences are Q1 - k × IQR and Q3 + k × IQR of ln(price per m²) in the offer's group. With k = 3 about 0.3% of the
    // offers fall outside, nearly all of them errors; with 1.5 it was 2.5%, mostly real luxury flats and cheap small towns.
    [Range(1, 10)]
    public double FenceMultiplier { get; set; } = 3;

    // A group with fewer offers is too small for its own fences: the next wider group is used.
    [Range(4, 1_000)]
    public int MinGroupSize { get; set; } = 30;

    // A price below this is a token amount (1 zł, 32 zł), and one above this per m² is a sale price.
    [Range(0, 10_000)]
    public decimal MinPrice { get; set; } = 100;

    [Range(1, 100_000)]
    public decimal MaxPricePerM2 { get; set; } = 1_000;

    // An area below this is a typo; so is one above LargeAreaM2 when the price per m² falls below the fences
    // (e.g. 4 216 m² for 1 350 zł).
    [Range(0, 100)]
    public decimal MinAreaM2 { get; set; } = 5;

    [Range(1, 100_000)]
    public decimal LargeAreaM2 { get; set; } = 300;

    // A title about a parking space or a garage is a parking space only when its area or price is this small: most such
    // titles are flats with a parking space.
    [Range(0, 1_000)]
    public decimal ParkingMaxAreaM2 { get; set; } = 20;

    [Range(0, 10_000)]
    public decimal ParkingMaxPrice { get; set; } = 700;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinAreaM2 >= LargeAreaM2)
        {
            yield return new ValidationResult("MinAreaM2 must be below LargeAreaM2.");
        }
    }
}
