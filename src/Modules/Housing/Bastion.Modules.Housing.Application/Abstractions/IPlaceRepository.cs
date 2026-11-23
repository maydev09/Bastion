using Bastion.Modules.Housing.Application.ListPlaces;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

public interface IPlaceRepository
{
    Task<IReadOnlyList<Place>> ListAsync(CancellationToken cancellationToken);

    // The places of one name (see Place.NameKey) in a voivodeship.
    Task<IReadOnlyList<Place>> ListByNameAsync(Voivodeship voivodeship, string nameKey, CancellationToken cancellationToken);

    // Inserts new places and updates changed ones by their SIMC id; places missing from the list are kept.
    Task<SaveCounts> SaveAsync(IReadOnlyList<Place> places, CancellationToken cancellationToken);

    // Places whose name, or a word of it, starts with the name key; most listed offers first, also places without offers.
    Task<IReadOnlyList<PlaceListItem>> SearchAsync(string nameKeyPrefix, int limit, CancellationToken cancellationToken);

    // Also a place without listed offers; null when there is no place with the id.
    Task<PlaceListItem?> GetListItemAsync(int id, CancellationToken cancellationToken);

    // Replaces all nearby places at once.
    Task ReplaceNearbyAsync(IReadOnlyList<PlaceNearby> nearby, CancellationToken cancellationToken);
}
