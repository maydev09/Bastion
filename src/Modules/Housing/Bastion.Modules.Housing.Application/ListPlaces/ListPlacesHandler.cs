using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListPlaces;

public sealed class ListPlacesHandler(IPlaceRepository repository)
{
    public const int MaxResults = 20;

    // The search is matched like offer locations: "bielsko biała", "Biala" and "bielsko-biala" all find Bielsko-Biała.
    public async Task<IReadOnlyList<PlaceListItem>> Search(string search, CancellationToken cancellationToken)
    {
        var nameKey = PlaceNames.Normalize(search);
        return nameKey.Length == 0 ? [] : await repository.SearchAsync(nameKey, MaxResults, cancellationToken);
    }

    public Task<PlaceListItem?> Get(int id, CancellationToken cancellationToken) => repository.GetListItemAsync(id, cancellationToken);

    public Task<IReadOnlyList<DistrictListItem>> ListDistricts(int placeId, CancellationToken cancellationToken) =>
        repository.ListDistrictsAsync(placeId, cancellationToken);
}
