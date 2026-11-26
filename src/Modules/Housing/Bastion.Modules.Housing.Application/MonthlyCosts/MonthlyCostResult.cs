namespace Bastion.Modules.Housing.Application.MonthlyCosts;

// Offers by the rent in their monthly cost (see RentSource); WithoutCost have no price in PLN, or need an estimate no group
// can give.
public sealed record MonthlyCostResult(
    int RateCount,
    int StatedRentCount,
    int NominalRentCount,
    int DescriptionRentCount,
    int IncludedRentCount,
    int EstimatedRentCount,
    int WithoutCostCount);
