using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// Resolves location keys to place ids, and district names to district ids, for one run. Each new key asks the
// database for the places of that name (an index lookup); many offers share a key, so the answer is kept until
// the run ends. A district the place does not have yet is added.
public sealed class PlaceResolver(OfferSources sources, IPlaceRepository places)
{
    private readonly Dictionary<(Source, string), int?> _resolved = [];
    private readonly Dictionary<(int PlaceId, int? ParentId, string NameKey), int> _districts = [];

    public async Task<int?> PlaceIdOfAsync(Source source, string? locationKey, CancellationToken cancellationToken)
    {
        if (locationKey is null || !sources.TryGet(source, out var offerSource))
        {
            return null;
        }

        if (!_resolved.TryGetValue((source, locationKey), out var placeId))
        {
            placeId = await MatchAsync(offerSource.LocationHintOf(locationKey), cancellationToken);
            _resolved[(source, locationKey)] = placeId;
        }

        return placeId;
    }

    public async Task<IReadOnlyList<Offer>> LinkAsync(IEnumerable<Offer> offers, CancellationToken cancellationToken)
    {
        var linked = new List<Offer>();
        foreach (var offer in offers)
        {
            var placeId = await PlaceIdOfAsync(offer.SourceId, offer.LocationKey, cancellationToken);
            linked.Add(offer with
            {
                PlaceId = placeId,
                DistrictId = await DistrictIdOfAsync(placeId, offer.DistrictName, offer.NeighborhoodName, cancellationToken),
            });
        }

        return linked;
    }

    // The neighborhood when there is one, else the district; a neighborhood is known only inside its district.
    private async Task<int?> DistrictIdOfAsync(int? placeId, string? districtName, string? neighborhoodName, CancellationToken cancellationToken)
    {
        if (placeId is null || districtName is null)
        {
            return null;
        }

        var districtId = await AddDistrictAsync(new District(placeId.Value, districtName), cancellationToken);
        return neighborhoodName is null || districtId is null
            ? districtId
            : await AddDistrictAsync(new District(placeId.Value, neighborhoodName, districtId), cancellationToken) ?? districtId;
    }

    // A name without letters or digits has no name key and is no district.
    private async Task<int?> AddDistrictAsync(District district, CancellationToken cancellationToken)
    {
        if (district.NameKey.Length == 0)
        {
            return null;
        }

        var key = (district.PlaceId, district.ParentId, district.NameKey);
        if (!_districts.TryGetValue(key, out var id))
        {
            id = await places.AddDistrictAsync(district, cancellationToken);
            _districts[key] = id;
        }

        return id;
    }

    private async Task<int?> MatchAsync(LocationHint? hint, CancellationToken cancellationToken)
    {
        if (hint is null || PlaceNames.VoivodeshipOf(hint.Voivodeship) is not { } voivodeship)
        {
            return null;
        }

        var candidates = await places.ListByNameAsync(voivodeship, PlaceNames.Normalize(hint.Place), cancellationToken);
        return PlaceMatcher.Match(hint, candidates)?.Id;
    }
}
