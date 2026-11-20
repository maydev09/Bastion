using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// Finds the place of a location hint. Names repeat (hundreds of "Nowa Wieś"), so the voivodeship
// is required and the county, municipality and kind narrow the candidates down.
public sealed class PlaceMatcher
{
    private readonly Dictionary<(Voivodeship, string), List<Place>> _byName = [];

    public PlaceMatcher(IEnumerable<Place> places)
    {
        foreach (var place in places)
        {
            var key = (place.Voivodeship, PlaceNames.Normalize(place.Name));
            if (!_byName.TryGetValue(key, out var candidates))
            {
                candidates = [];
                _byName[key] = candidates;
            }

            candidates.Add(place);
        }
    }

    public Place? Match(LocationHint hint)
    {
        if (PlaceNames.VoivodeshipOf(hint.Voivodeship) is not { } voivodeship
            || !_byName.TryGetValue((voivodeship, PlaceNames.Normalize(hint.Place)), out var candidates))
        {
            return null;
        }

        // A hint that fits no candidate is ignored, e.g. a county the dictionary does not know.
        IReadOnlyList<Place> result = candidates;
        result = Narrow(result, p => hint.County is not null && SameName(p.County, hint.County));
        result = Narrow(result, p => hint.Municipality is not null && SameName(p.Municipality, hint.Municipality));
        result = Narrow(result, p => hint.Kind is not null && p.Kind == hint.Kind);
        if (result.Count > 1)
        {
            // Same name twice in one municipality (e.g. a village and a settlement): the larger one is meant.
            var largest = result.Max(p => p.Population ?? -1);
            result = Narrow(result, p => largest >= 0 && p.Population == largest);
        }

        return result.Count == 1 ? result[0] : null;
    }

    private static IReadOnlyList<Place> Narrow(IReadOnlyList<Place> candidates, Func<Place, bool> fits)
    {
        if (candidates.Count <= 1)
        {
            return candidates;
        }

        var narrowed = candidates.Where(fits).ToList();
        return narrowed.Count > 0 ? narrowed : candidates;
    }

    private static bool SameName(string? name, string hint) =>
        name is not null && PlaceNames.Normalize(name) == PlaceNames.Normalize(hint);
}
