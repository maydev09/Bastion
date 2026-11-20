using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Microsoft.Extensions.Options;

namespace Bastion.Modules.Housing.Application.ImportPlaces;

public sealed class ImportPlacesHandler(
    IPlaceSource source,
    IPlaceRepository places,
    IOfferRepository offers,
    OfferSources sources,
    IOptions<PlaceOptions> options,
    IOptions<PlaceSizeOptions> sizes)
{
    public async Task<ImportPlacesOutcome> Handle(CancellationToken cancellationToken)
    {
        var fetched = await source.FetchAsync(cancellationToken);
        if (fetched.Failure is not null)
        {
            return new ImportPlacesOutcome(null, fetched.Failure);
        }

        var saved = await places.SaveAsync(fetched.Places, cancellationToken);

        // Nearby places need the stored ids, so they are computed from the stored places.
        var stored = await places.ListAsync(cancellationToken);
        var nearby = NearbyPlaces.Of(stored, options.Value, sizes.Value.Town.Metropolis);
        await places.ReplaceNearbyAsync(nearby, cancellationToken);

        // Stored offers get their places again: new places, and better matches after a fix in the matcher.
        var resolver = new PlaceResolver(sources, new PlaceMatcher(stored));
        var locations = await offers.ListLocationsAsync(cancellationToken);
        var links = locations
            .Select(l => new PlaceLink(l.Source, l.LocationKey, resolver.PlaceIdOf(l.Source, l.LocationKey)))
            .ToList();
        await offers.SetPlacesAsync(links, cancellationToken);

        var linked = locations.Zip(links).Where(p => p.Second.PlaceId is not null).Sum(p => p.First.OfferCount);
        var result = new ImportPlacesResult(
            fetched.Places.Count, saved.Added, saved.Updated, linked, locations.Sum(l => l.OfferCount) - linked);
        return new ImportPlacesOutcome(result, null);
    }
}
