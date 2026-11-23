using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;

namespace Bastion.Modules.Housing.Application.LinkPlaces;

// Stored offers get their places again: after new places were imported, or after a fix in the matcher.
// New offers do not need it: an import links them at once.
public sealed class LinkPlacesHandler(IOfferRepository offers, IPlaceRepository places, OfferSources sources)
{
    public async Task<LinkPlacesResult> Handle(CancellationToken cancellationToken)
    {
        var resolver = new PlaceResolver(sources, places);
        var locations = await offers.ListLocationsAsync(cancellationToken);
        var links = new List<PlaceLink>();
        foreach (var location in locations)
        {
            var placeId = await resolver.PlaceIdOfAsync(location.Source, location.LocationKey, cancellationToken);
            links.Add(new PlaceLink(location.Source, location.LocationKey, placeId));
        }

        await offers.SetPlacesAsync(links, cancellationToken);

        var linked = locations.Zip(links).Where(p => p.Second.PlaceId is not null).Sum(p => p.First.OfferCount);
        return new LinkPlacesResult(linked, locations.Sum(l => l.OfferCount) - linked);
    }
}
