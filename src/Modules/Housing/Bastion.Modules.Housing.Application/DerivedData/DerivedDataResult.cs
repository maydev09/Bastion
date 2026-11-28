using Bastion.Modules.Housing.Application.MonthlyCosts;
using Bastion.Modules.Housing.Application.StatisticsExclusions;

namespace Bastion.Modules.Housing.Application.DerivedData;

public sealed record DerivedDataResult(MonthlyCostResult Costs, StatisticsExclusionResult Exclusions);
