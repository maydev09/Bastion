using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IPlaceRepository
{
    Task<IReadOnlyList<Place>> ListAsync(CancellationToken cancellationToken);

    // Inserts new places and updates changed ones by their SIMC id; places missing from the list are kept.
    Task<SaveCounts> SaveAsync(IReadOnlyList<Place> places, CancellationToken cancellationToken);

    // Replaces all nearby places at once.
    Task ReplaceNearbyAsync(IReadOnlyList<PlaceNearby> nearby, CancellationToken cancellationToken);
}
