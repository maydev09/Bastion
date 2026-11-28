using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IStatisticsExclusionRepository
{
    // Every listed offer (not removed).
    Task<IReadOnlyList<ExclusionInput>> ListInputsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceFence>> ListFencesAsync(CancellationToken cancellationToken);

    // The stored fences become these: a group keeps its row and id, a group without a fence now loses its row.
    Task ReplaceFencesAsync(IReadOnlyList<PriceFence> fences, CancellationToken cancellationToken);

    // Stores the exclusions of the offers, found at calculatedAt; an offer of offerIds without one loses its stored one.
    // A row whose values do not change keeps its calculated_at.
    Task SaveAsync(
        IReadOnlyList<int> offerIds, DateTimeOffset calculatedAt, IReadOnlyList<StatisticsExclusion> exclusions, CancellationToken cancellationToken);
}
