using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// Resolves location keys to place ids for one run; many offers share a key, so each key is matched once.
public sealed class PlaceResolver(OfferSources sources, PlaceMatcher matcher)
{
    private readonly Dictionary<(Source, string), int?> _resolved = [];

    public int? PlaceIdOf(Source source, string? locationKey)
    {
        if (locationKey is null || !sources.TryGet(source, out var offerSource))
        {
            return null;
        }

        if (!_resolved.TryGetValue((source, locationKey), out var placeId))
        {
            placeId = offerSource.LocationHintOf(locationKey) is { } hint ? matcher.Match(hint)?.Id : null;
            _resolved[(source, locationKey)] = placeId;
        }

        return placeId;
    }

    public Offer Link(Offer offer) => offer with { PlaceId = PlaceIdOf(offer.SourceId, offer.LocationKey) };
}
