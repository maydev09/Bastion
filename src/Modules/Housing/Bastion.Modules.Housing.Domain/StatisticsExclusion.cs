namespace Bastion.Modules.Housing.Domain;

// Table housing.statistics_exclusion: an offer the statistics leave out, and why (Reason). PriceFenceId is the fence of the
// offer's group it was compared with; null when the offer has no price in PLN or no area, or its groups have no fence.
public sealed record StatisticsExclusion(
    int OfferId,
    StatisticsExclusionReason Reason,
    int? PriceFenceId,
    DateTimeOffset CalculatedAt);
