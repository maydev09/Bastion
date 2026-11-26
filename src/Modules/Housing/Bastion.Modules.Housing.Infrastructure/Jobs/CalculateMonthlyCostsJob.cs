using Bastion.BuildingBlocks.Jobs;
using Bastion.Modules.Housing.Application.MonthlyCosts;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// Saved offers get their cost at once from the stored rates; this job calculates the rates again from all offers.
public sealed class CalculateMonthlyCostsJob(MonthlyCostService costs) : IJob
{
    public const string Name = "CalculateMonthlyCosts";

    public async Task<JobSummary> Run(JobContext context, CancellationToken cancellationToken)
    {
        var result = await costs.RecalculateAllAsync(cancellationToken);
        return new JobSummary(new Dictionary<string, long>
        {
            ["rentRates"] = result.RateCount,
            ["statedRents"] = result.StatedRentCount,
            ["nominalRents"] = result.NominalRentCount,
            ["descriptionRents"] = result.DescriptionRentCount,
            ["includedRents"] = result.IncludedRentCount,
            ["estimatedRents"] = result.EstimatedRentCount,
            ["withoutCost"] = result.WithoutCostCount,
        });
    }
}
