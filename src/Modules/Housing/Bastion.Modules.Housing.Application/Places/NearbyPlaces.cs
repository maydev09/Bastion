using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// The ladder "where is the nearest ...": town, county seat, voivodeship capital, metropolis, and big cities around.
// A tier the place itself meets is left out, so Warszawa has none and a village has all of them.
public static class NearbyPlaces
{
    private const double EarthRadiusKm = 6371.0;

    public static IReadOnlyList<PlaceNearby> Of(IReadOnlyList<Place> places, PlaceOptions options, int metropolisMinPopulation)
    {
        var located = places.Where(p => p.Latitude is not null && p.Longitude is not null).Select(Point.Of).ToList();
        var towns = located.Where(p => p.Place.Kind == PlaceKind.Town).ToList();
        var countySeats = located.Where(p => p.Place.Role is PlaceRole.CountySeat or PlaceRole.VoivodeshipCapital).ToList();
        var capitals = located.Where(p => p.Place.Role == PlaceRole.VoivodeshipCapital).ToList();
        var metropolises = towns.Where(p => p.Place.Population >= metropolisMinPopulation).ToList();
        var bigCities = towns.Where(p => p.Place.Population >= options.BigCityMinPopulation).ToList();

        var result = new List<PlaceNearby>();
        foreach (var point in located)
        {
            var place = point.Place;
            if (place.Kind != PlaceKind.Town)
            {
                AddNearest(NearbyTier.Town, towns);
            }

            if (place.Role is not (PlaceRole.CountySeat or PlaceRole.VoivodeshipCapital))
            {
                AddNearest(NearbyTier.CountySeat, countySeats);
            }

            if (place.Role != PlaceRole.VoivodeshipCapital)
            {
                AddNearest(NearbyTier.VoivodeshipCapital, capitals);
            }

            if (!(place.Kind == PlaceKind.Town && place.Population >= metropolisMinPopulation))
            {
                AddNearest(NearbyTier.Metropolis, metropolises);
            }

            short rank = 1;
            foreach (var (city, km) in bigCities
                         .Where(c => c.Place.Id != place.Id)
                         .Select(c => (City: c, Km: point.KmTo(c)))
                         .Where(c => c.Km <= options.BigCityMaxKm)
                         .OrderBy(c => c.Km)
                         .Take(options.BigCityCount))
            {
                result.Add(new PlaceNearby(place.Id, NearbyTier.BigCity, rank++, city.Place.Id, Round(km)));
            }

            void AddNearest(NearbyTier tier, List<Point> candidates)
            {
                // A cheap flat-earth distance picks the nearest; only the winner gets the exact one.
                var nearest = candidates.Where(c => c.Place.Id != place.Id).MinBy(point.FlatDistanceTo);
                if (nearest is not null)
                {
                    result.Add(new PlaceNearby(place.Id, tier, 1, nearest.Place.Id, Round(point.KmTo(nearest))));
                }
            }
        }

        return result;
    }

    private static decimal Round(double km) => Math.Round((decimal)km, 1);

    private sealed record Point(Place Place, double Latitude, double Longitude, double CosLatitude)
    {
        public static Point Of(Place place)
        {
            var latitude = (double)place.Latitude!.Value * Math.PI / 180;
            return new Point(place, latitude, (double)place.Longitude!.Value * Math.PI / 180, Math.Cos(latitude));
        }

        public double FlatDistanceTo(Point other)
        {
            var x = (other.Longitude - Longitude) * CosLatitude;
            var y = other.Latitude - Latitude;
            return (x * x) + (y * y);
        }

        // Haversine formula on a sphere; a few hundred metres off at most.
        public double KmTo(Point other)
        {
            var a = Math.Pow(Math.Sin((other.Latitude - Latitude) / 2), 2)
                    + (CosLatitude * other.CosLatitude * Math.Pow(Math.Sin((other.Longitude - Longitude) / 2), 2));
            return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
        }
    }
}
