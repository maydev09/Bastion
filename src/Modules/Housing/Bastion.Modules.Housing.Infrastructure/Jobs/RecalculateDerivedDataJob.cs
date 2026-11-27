using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application.DerivedData;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// The last step of every collecting run (ImportNewOffers, SyncAllOffers) on its own, started by hand: e.g. after the rules
// of the monthly cost or of the statistics exclusions changed in the configuration.
public sealed class RecalculateDerivedDataJob(DerivedDataService derivedData) : IJob
{
    public const string Name = "RecalculateDerivedData";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        var result = await derivedData.RecalculateAllAsync(cancellationToken);
        var (costs, exclusions) = (result.Costs, result.Exclusions);
        return new JobSummary(new Dictionary<string, long>
        {
            ["rentRates"] = costs.RateCount,
            ["statedRents"] = costs.StatedRentCount,
            ["nominalRents"] = costs.NominalRentCount,
            ["descriptionRents"] = costs.DescriptionRentCount,
            ["includedRents"] = costs.IncludedRentCount,
            ["estimatedRents"] = costs.EstimatedRentCount,
            ["withoutCost"] = costs.WithoutCostCount,
            ["priceFences"] = exclusions.FenceCount,
            ["checkedOffers"] = exclusions.CheckedCount,
            ["excludedOffers"] = exclusions.Excluded.Values.Sum(),
            ["rooms"] = exclusions.Excluded.GetValueOrDefault(StatisticsExclusionReason.Room),
            ["parking"] = exclusions.Excluded.GetValueOrDefault(StatisticsExclusionReason.Parking),
            ["implausibleAreas"] = exclusions.Excluded.GetValueOrDefault(StatisticsExclusionReason.ImplausibleArea),
            ["implausiblePrices"] = exclusions.Excluded.GetValueOrDefault(StatisticsExclusionReason.ImplausiblePrice),
            ["auctions"] = exclusions.Excluded.GetValueOrDefault(StatisticsExclusionReason.Auction),
            ["perPerson"] = exclusions.Excluded.GetValueOrDefault(StatisticsExclusionReason.PerPerson),
            ["rentForRenovation"] = exclusions.Excluded.GetValueOrDefault(StatisticsExclusionReason.RentForRenovation),
            ["unusualPrices"] = exclusions.Excluded.GetValueOrDefault(StatisticsExclusionReason.UnusualPrice),
        });
    }
}
