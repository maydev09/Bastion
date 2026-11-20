using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// Finds the place of a location hint among places of the same name. Names repeat (hundreds of "Nowa Wieś"),
// so the voivodeship is required and the county, municipality and kind narrow the candidates down.
public static class PlaceMatcher
{
    public static Place? Match(LocationHint hint, IEnumerable<Place> places)
    {
        if (PlaceNames.VoivodeshipOf(hint.Voivodeship) is not { } voivodeship)
        {
            return null;
        }

        var name = PlaceNames.Normalize(hint.Place);
        IReadOnlyList<Place> result = places.Where(p => p.Voivodeship == voivodeship && p.NameKey == name).ToList();

        // A hint that fits no candidate is ignored, e.g. a county the dictionary does not know.
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
