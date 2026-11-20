using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// Resolves location keys to place ids for one run. Each new key asks the database for the places of that
// name (an index lookup); many offers share a key, so the answer is kept until the run ends.
public sealed class PlaceResolver(OfferSources sources, IPlaceRepository places)
{
    private readonly Dictionary<(Source, string), int?> _resolved = [];

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
            linked.Add(offer with { PlaceId = await PlaceIdOfAsync(offer.SourceId, offer.LocationKey, cancellationToken) });
        }

        return linked;
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
