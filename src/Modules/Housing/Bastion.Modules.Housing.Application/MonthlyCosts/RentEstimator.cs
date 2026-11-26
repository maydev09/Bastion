using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.MonthlyCosts;

// Estimates the admin rent of an offer that states none, from offers that state a plausible one (the samples):
// 1. the wider area: the median rent per m² of the country, then of ever more local groups (voivodeship, place, district,
//    neighborhood, each also by building type); each group pulls the rate towards its own median by its size:
//    (n * group median + WiderGroupWeight * rate so far) / (n + WiderGroupWeight);
// 2. the neighbors: the median rent per m² of the nearest offers of the same building type within NeighborRadiusKm;
// 3. both together: (neighbors * their median + WiderAreaWeight * wider area) / (neighbors + WiderAreaWeight);
// 4. the area: the rate times (area / TypicalAreaM2) ^ AreaExponent, as the rent per m² falls with the area.
// Tested on the stored offers (2026-11-26, leave one out): typical error 16.4% against 17.7% for the median of the most
// local group with 20 offers; even flats in the same building differ by about 14%.
public sealed class RentEstimator
{
    private const double KmPerDegree = 111.2;

    private readonly MonthlyCostOptions _options;
    private readonly Dictionary<RateKey, RentRate> _rates;

    // The samples with a pin, by latitude, so the neighbors of a point are looked for in a narrow band.
    private readonly Sample[] _samples;

    public RentEstimator(MonthlyCostOptions options, IReadOnlyList<RentRate> rates, IReadOnlyList<CostInput> samples)
    {
        _options = options;
        _rates = rates.ToDictionary(r => new RateKey(r.Basis, r.Voivodeship, r.PlaceId, r.DistrictId, r.BuildingType));
        _samples = samples
            .Where(s => s is { Latitude: not null, Longitude: not null })
            .Select(s => new Sample((double)s.Latitude!.Value, (double)s.Longitude!.Value, RateOf(s), s.BuildingType))
            .OrderBy(s => s.Latitude)
            .ToArray();
    }

    // The median rent per m² of every group that has samples; the samples are offers with a plausible rent.
    public static List<RentRate> CalculateRates(IReadOnlyList<CostInput> samples, DateTimeOffset now)
    {
        var groups = new Dictionary<RateKey, List<decimal>>();
        foreach (var sample in samples)
        {
            foreach (var key in ListKeys(sample))
            {
                if (!groups.TryGetValue(key, out var values))
                {
                    values = [];
                    groups[key] = values;
                }

                values.Add(RateOf(sample));
            }
        }

        return groups
            .Select(g => new RentRate(
                g.Key.Basis, g.Key.Voivodeship, g.Key.PlaceId, g.Key.DistrictId, g.Key.BuildingType,
                Math.Round(CalculateMedian(g.Value), 2, MidpointRounding.AwayFromZero), g.Value.Count, now))
            .ToList();
    }

    // Null when the offer has no area, or neither a group nor a neighbor gives a rate.
    public RentEstimate? Calculate(CostInput offer)
    {
        if (offer.AreaM2 is not > 0)
        {
            return null;
        }

        RentRate? local = null;
        decimal? wider = null;
        foreach (var key in ListKeys(offer))
        {
            if (_rates.TryGetValue(key, out var rate))
            {
                wider = wider is null
                    ? rate.MedianPerM2
                    : (rate.SampleSize * rate.MedianPerM2 + _options.WiderGroupWeight * wider.Value) / (rate.SampleSize + _options.WiderGroupWeight);
                local = rate;
            }
        }

        var (neighbors, neighborsPerM2, neighborsKm) = FindNeighbors(offer);
        decimal combined;
        if (neighborsPerM2 is null && wider is null)
        {
            return null;
        }
        else if (neighborsPerM2 is null)
        {
            combined = wider!.Value;
        }
        else if (wider is null)
        {
            combined = neighborsPerM2.Value;
        }
        else
        {
            combined = (neighbors * neighborsPerM2.Value + _options.WiderAreaWeight * wider.Value) / (neighbors + _options.WiderAreaWeight);
        }

        var areaFactor = (decimal)Math.Pow((double)(offer.AreaM2.Value / _options.TypicalAreaM2), _options.AreaExponent);
        return new RentEstimate(
            offer.OfferId,
            Math.Round(combined * areaFactor, 2, MidpointRounding.AwayFromZero),
            local?.Basis,
            wider is null ? null : Math.Round(wider.Value, 2, MidpointRounding.AwayFromZero),
            local?.SampleSize,
            neighbors,
            neighborsPerM2,
            neighborsKm);
    }

    // The nearest samples within the radius, of the offer's building type when it is known; duplicates of the offer's own
    // flat count too, as their rent is the rent of the same flat.
    private (int Count, decimal? MedianPerM2, decimal? FarthestKm) FindNeighbors(CostInput offer)
    {
        if (offer is not { Latitude: { } latitude, Longitude: { } longitude })
        {
            return (0, null, null);
        }

        var lat = (double)latitude;
        var lon = (double)longitude;
        var radius = (double)_options.NeighborRadiusKm;
        var kmPerLongitude = KmPerDegree * Math.Cos(lat * Math.PI / 180);
        var found = new List<(double Km, decimal Rate)>();
        for (var i = FindFirstAtOrAbove(lat - radius / KmPerDegree); i < _samples.Length && _samples[i].Latitude <= lat + radius / KmPerDegree; i++)
        {
            var sample = _samples[i];
            if (offer.BuildingType is { } type && sample.BuildingType != type)
            {
                continue;
            }

            var km = Math.Sqrt(Math.Pow((sample.Latitude - lat) * KmPerDegree, 2) + Math.Pow((sample.Longitude - lon) * kmPerLongitude, 2));
            if (km <= radius)
            {
                found.Add((km, sample.RatePerM2));
            }
        }

        if (found.Count == 0)
        {
            return (0, null, null);
        }

        var nearest = found.OrderBy(f => f.Km).Take(_options.Neighbors).ToList();
        return (
            nearest.Count,
            Math.Round(CalculateMedian(nearest.Select(n => n.Rate).ToList()), 2, MidpointRounding.AwayFromZero),
            Math.Round((decimal)nearest[^1].Km, 2, MidpointRounding.AwayFromZero));
    }

    // Binary search in the samples ordered by latitude.
    private int FindFirstAtOrAbove(double latitude)
    {
        int low = 0, high = _samples.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (_samples[middle].Latitude < latitude)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static decimal RateOf(CostInput sample) => sample.Rent!.Value / sample.AreaM2!.Value;

    // The groups of an offer, from the widest to the most local; a group needs the values it is keyed by.
    private static List<RateKey> ListKeys(CostInput offer)
    {
        var type = offer.BuildingType;
        var keys = new List<RateKey> { new(RentBasis.Country, null, null, null, null) };
        if (offer.Voivodeship is { } voivodeship)
        {
            keys.Add(new RateKey(RentBasis.Voivodeship, voivodeship, null, null, null));
            if (type is not null)
            {
                keys.Add(new RateKey(RentBasis.VoivodeshipBuildingType, voivodeship, null, null, type));
            }
        }

        if (offer.PlaceId is { } place)
        {
            keys.Add(new RateKey(RentBasis.Place, null, place, null, null));
            if (type is not null)
            {
                keys.Add(new RateKey(RentBasis.PlaceBuildingType, null, place, null, type));
            }
        }

        if (offer.DistrictId is { } district)
        {
            keys.Add(new RateKey(RentBasis.District, null, null, district, null));
            if (type is not null)
            {
                keys.Add(new RateKey(RentBasis.DistrictBuildingType, null, null, district, type));
            }
        }

        if (offer.NeighborhoodId is { } neighborhood)
        {
            keys.Add(new RateKey(RentBasis.Neighborhood, null, null, neighborhood, null));
            if (type is not null)
            {
                keys.Add(new RateKey(RentBasis.NeighborhoodBuildingType, null, null, neighborhood, type));
            }
        }

        return keys;
    }

    private static decimal CalculateMedian(List<decimal> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    }

    private readonly record struct RateKey(
        RentBasis Basis, Voivodeship? Voivodeship, int? PlaceId, int? DistrictId, BuildingType? BuildingType);

    private readonly record struct Sample(double Latitude, double Longitude, decimal RatePerM2, BuildingType? BuildingType);
}
