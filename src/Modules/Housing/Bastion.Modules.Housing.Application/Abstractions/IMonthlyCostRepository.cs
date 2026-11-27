using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IMonthlyCostRepository
{
    // Every listed offer, and a removed one that never got a cost. A removed offer keeps the cost it had while it was
    // listed, so it is not read again.
    Task<IReadOnlyList<CostInput>> ListInputsAsync(CancellationToken cancellationToken);

    // Every listed offer (not removed) that states a rent and an area, without its title and description: the typical rents
    // describe the market as it is now.
    Task<IReadOnlyList<CostInput>> ListRateSamplesAsync(CancellationToken cancellationToken);

    // The stored rates are replaced by these.
    Task ReplaceRatesAsync(IReadOnlyList<RentRate> rates, CancellationToken cancellationToken);

    // Stores the costs of the offers and the estimates of their rents, calculated at calculatedAt; an offer of offerIds
    // without a cost or an estimate loses its stored one. A row whose values do not change keeps its calculated_at.
    Task SaveAsync(
        IReadOnlyList<int> offerIds,
        DateTimeOffset calculatedAt,
        IReadOnlyList<MonthlyCost> costs,
        IReadOnlyList<RentEstimate> estimates,
        CancellationToken cancellationToken);
}
