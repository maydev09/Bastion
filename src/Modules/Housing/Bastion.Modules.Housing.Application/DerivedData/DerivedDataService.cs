using Bastion.Modules.Housing.Application.MonthlyCosts;
using Bastion.Modules.Housing.Application.StatisticsExclusions;

namespace Bastion.Modules.Housing.Application.DerivedData;

// What Bastion calculates for the offers from their own data and from the market: the group statistics (rent rates, price
// fences), and with them the monthly cost of every offer and whether the statistics leave it out. The collecting jobs
// call it as their last step, so the data is complete when a run ends; it takes a few seconds for all listed offers.
// One calculation at a time (a registered singleton): two runs ending together never write over each other.
public sealed class DerivedDataService(MonthlyCostService costs, StatisticsExclusionService exclusions) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<DerivedDataResult> RecalculateAllAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return new(await costs.RecalculateAllAsync(cancellationToken), await exclusions.RecalculateAllAsync(cancellationToken));
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();
}
