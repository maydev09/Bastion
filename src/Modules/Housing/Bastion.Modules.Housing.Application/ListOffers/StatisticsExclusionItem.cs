using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

// Why the statistics leave an offer out (see StatisticsExclusionReason), and the fence it was compared with: the usual
// prices per m² of its group, Tukey's fences (Q1 − k × IQR, Q3 + k × IQR of ln(price per m²), k =
// StatisticsExclusionOptions.FenceMultiplier) of the listed offers with the same number of rooms in its place, else in places
// of its size in its voivodeship, else in the country (Basis, SampleSize offers). The fence is null when the offer has no
// price or area, or its groups have none. Found when the offer is saved and again for every listed offer every night.
public sealed record StatisticsExclusionItem
{
    public required StatisticsExclusionReason Reason { get; init; }

    public PriceFenceBasis? Basis { get; init; }

    public int? SampleSize { get; init; }

    public decimal? FenceLowPerM2 { get; init; }

    public decimal? FenceHighPerM2 { get; init; }

    public Currency? Currency { get; init; }

    public required DateTimeOffset CalculatedAt { get; init; }
}
