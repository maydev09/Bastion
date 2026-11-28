using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.StatisticsExclusions;

// The listed offers checked, the groups with a price fence, and the excluded offers by reason; a reason no offer has is
// missing.
public sealed record StatisticsExclusionResult(int CheckedCount, int FenceCount, IReadOnlyDictionary<StatisticsExclusionReason, int> Excluded);
