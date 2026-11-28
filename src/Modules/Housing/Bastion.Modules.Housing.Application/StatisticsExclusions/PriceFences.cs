using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.StatisticsExclusions;

// The usual prices per m² (PLN) of groups of offers by rooms (1, 2, 3, 4 and more, not known): Tukey's fences on
// ln(price per m²), as prices spread by a factor rather than by an amount. An offer is compared with the most local group
// that has a fence: its place, else places of its size in its voivodeship, else of its size in the country, else the
// country. Places of the same size, not the whole voivodeship, so a village is not compared with the voivodeship's cities.
public sealed class PriceFences
{
    private readonly record struct Group(PriceFenceBasis Basis, int? Rooms, int? PlaceId, Voivodeship? Voivodeship, PlaceSize? Size);

    private const int MaxRooms = 4;

    private readonly Dictionary<Group, PriceFence> _fences;
    private readonly PlaceSizeOptions _placeSizes;

    // The stored fences, to compare offers with.
    public PriceFences(IReadOnlyList<PriceFence> fences, PlaceSizeOptions placeSizes)
    {
        _fences = fences.ToDictionary(f => new Group(f.Basis, f.Rooms, f.PlaceId, f.Voivodeship, f.PlaceSize));
        _placeSizes = placeSizes;
    }

    // From the offers with a price in PLN and an area; a group with fewer than MinGroupSize of them gets no fence.
    public static List<PriceFence> Calculate(
        IReadOnlyList<ExclusionInput> offers, StatisticsExclusionOptions options, PlaceSizeOptions placeSizes, DateTimeOffset now)
    {
        var samples = new Dictionary<Group, List<double>>();
        foreach (var offer in offers)
        {
            if (offer is not { Price: { } price, PriceCurrency: Currency.Pln, AreaM2: { } area } || price <= 0 || area <= 0)
            {
                continue;
            }

            var value = Math.Log((double)(price / area));
            foreach (var group in ListGroups(offer, placeSizes))
            {
                if (!samples.TryGetValue(group, out var values))
                {
                    values = [];
                    samples[group] = values;
                }

                values.Add(value);
            }
        }

        var fences = new List<PriceFence>();
        foreach (var (group, values) in samples)
        {
            if (values.Count < options.MinGroupSize)
            {
                continue;
            }

            values.Sort();
            var q1 = Percentile(values, 0.25);
            var q3 = Percentile(values, 0.75);
            var reach = options.FenceMultiplier * (q3 - q1);
            fences.Add(new PriceFence(
                0, group.Basis, group.Rooms, group.PlaceId, group.Voivodeship, group.Size,
                ToPerM2(q1 - reach), ToPerM2(q3 + reach), Currency.Pln, values.Count, now));
        }

        return fences;
    }

    // The fence of the offer's most local group that has one; null when none has.
    public PriceFence? Find(ExclusionInput offer)
    {
        foreach (var group in ListGroups(offer, _placeSizes))
        {
            if (_fences.TryGetValue(group, out var fence))
            {
                return fence;
            }
        }

        return null;
    }

    // The offer's groups, the most local first.
    private static List<Group> ListGroups(ExclusionInput offer, PlaceSizeOptions placeSizes)
    {
        int? rooms = offer.Rooms > 0 ? Math.Min(offer.Rooms.Value, MaxRooms) : null;
        var size = offer.Population is { } population ? placeSizes.Of(offer.PlaceKind, population) : null;
        var groups = new List<Group>();
        if (offer.PlaceId is { } placeId)
        {
            groups.Add(new Group(PriceFenceBasis.Place, rooms, placeId, null, null));
        }

        if (size is not null && offer.Voivodeship is { } voivodeship)
        {
            groups.Add(new Group(PriceFenceBasis.VoivodeshipPlaceSize, rooms, null, voivodeship, size));
        }

        if (size is not null)
        {
            groups.Add(new Group(PriceFenceBasis.PlaceSize, rooms, null, null, size));
        }

        groups.Add(new Group(PriceFenceBasis.Country, rooms, null, null, null));
        return groups;
    }

    // Linear between the two nearest values (as PostgreSQL's percentile_cont).
    private static double Percentile(List<double> sorted, double fraction)
    {
        var position = fraction * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, sorted.Count - 1);
        return sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }

    private static decimal ToPerM2(double logValue) => Math.Round((decimal)Math.Exp(logValue), 2, MidpointRounding.AwayFromZero);
}
