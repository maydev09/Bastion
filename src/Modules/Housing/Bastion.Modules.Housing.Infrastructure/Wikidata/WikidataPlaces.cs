using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Infrastructure.Wikidata;

public static class WikidataPlaces
{
    // "is a" (P31) values that mean a town: town, small town, city with county rights, city over 100 000, urban municipality.
    private static readonly HashSet<string> TownTypes = ["Q515", "Q3957", "Q925381", "Q1549591", "Q2616791"];

    private const string CityWithCountyRights = "Q925381";

    // Village, village in Poland, settlement, colony, hamlet, forest settlement, settlement with a Kashubian name.
    private static readonly HashSet<string> VillageTypes = ["Q532", "Q3558970", "Q5084", "Q12015918", "Q4379239", "Q17087359", "Q138845716"];

    // "Located in" (P131) has no fixed level: a village points to its municipality, Pruszków to its county,
    // Warszawa straight to its voivodeship. The whole chain up is searched for each level.
    // A place without a voivodeship cannot be matched and is left out.
    public static IReadOnlyList<Place> Build(WikidataRows rows)
    {
        var types = rows.Types.ToLookup(t => t.Item, t => t.Type);
        var parents = rows.Parents.ToLookup(p => p.Item, p => p.Parent);
        var populations = rows.Populations.ToLookup(p => p.Item);
        var coordinates = rows.Coordinates.ToLookup(c => c.Item);
        var seats = rows.Seats.ToLookup(s => s.Item, s => s.UnitName);
        var municipalities = rows.Simcs.Select(s => s.Item).Distinct().ToDictionary(i => i, i => MunicipalityUnit(parents[i], rows.UnitNames));
        var guessedSeats = GuessSeats(rows, types, municipalities);

        var places = new List<Place>();
        foreach (var (item, simc) in rows.Simcs.DistinctBy(s => s.Simc))
        {
            if (!rows.Names.TryGetValue(item, out var name))
            {
                continue;
            }

            var chain = Chain(item, parents).Where(rows.UnitNames.ContainsKey).Select(u => rows.UnitNames[u]).Where(IsCurrent).ToList();
            if (chain.Select(VoivodeshipOf).FirstOrDefault(v => v is not null) is not { } voivodeship)
            {
                continue;
            }

            var municipality = municipalities[item] is { } unit ? MunicipalityName(rows.UnitNames[unit]) : null;
            var kind = KindOf(types[item]);
            var role = RoleOf(seats[item], types[item], kind, guessedSeats.Contains(item));
            var place = new Place(simc, name, voivodeship, County(chain), municipality, kind, role);

            // The newest count wins; a count without a date is the oldest.
            if (populations[item].Any())
            {
                var (_, population, date) = populations[item].OrderByDescending(p => p.Date ?? DateTimeOffset.MinValue).First();
                place = place with
                {
                    Population = (int)Math.Round(population),
                    PopulationYear = date?.Year,
                    PopulationSource = PlaceSource.Wikidata,
                };
            }

            if (coordinates[item].Any())
            {
                var (_, latitude, longitude) = coordinates[item].First();
                place = place with { Latitude = Math.Round(latitude, 6), Longitude = Math.Round(longitude, 6) };
            }

            places.Add(place);
        }

        return places;
    }

    // Units above the item, nearest first.
    private static IEnumerable<string> Chain(string item, ILookup<string, string> parents)
    {
        var seen = new HashSet<string>();
        var queue = new Queue<string>(parents[item].Order());
        while (queue.TryDequeue(out var unit))
        {
            if (!seen.Add(unit))
            {
                continue;
            }

            yield return unit;
            foreach (var parent in parents[unit].Order())
            {
                queue.Enqueue(parent);
            }
        }
    }

    // Only a current voivodeship; the chain can also hold historical ones.
    private static Voivodeship? VoivodeshipOf(string unit) =>
        unit.StartsWith("województwo ", StringComparison.Ordinal) ? PlaceNames.VoivodeshipOf(unit) : null;

    // Former units are named with a parenthesis, e.g. "Gmina Biała (województwo opolskie)"; some lack an end date.
    private static bool IsCurrent(string unit) => !unit.Contains('(', StringComparison.Ordinal);

    // Labels are "powiat złotowski", and "gmina Wejherowo" or "Gmina Raków"; only the name is kept.
    private static string? County(IEnumerable<string> chain) =>
        chain.FirstOrDefault(u => u.StartsWith("powiat ", StringComparison.OrdinalIgnoreCase))?["powiat ".Length..];

    // The municipality is the item's own current unit when that is neither a county nor a voivodeship.
    private static string? MunicipalityUnit(IEnumerable<string> ownUnits, IReadOnlyDictionary<string, string> unitNames) =>
        ownUnits.Order()
            .FirstOrDefault(u => unitNames.TryGetValue(u, out var name) && IsCurrent(name) && !IsCountyOrVoivodeship(name) && name != "Polska");

    private static string MunicipalityName(string unit) =>
        unit.StartsWith("gmina ", StringComparison.OrdinalIgnoreCase) ? unit["gmina ".Length..] : unit;

    private static bool IsCountyOrVoivodeship(string unit) =>
        unit.StartsWith("powiat ", StringComparison.OrdinalIgnoreCase) || unit.StartsWith("województwo ", StringComparison.OrdinalIgnoreCase);

    // Wikidata names the seat of only about a third of municipalities. A municipality with a town has its seat in a
    // town (towns count as seats anyway); for any other one without a named seat, the place named like the
    // municipality is taken, but only when there is exactly one (e.g. two villages "Dłutów" give no guess).
    private static HashSet<string> GuessSeats(
        WikidataRows rows, ILookup<string, string> types, IReadOnlyDictionary<string, string?> municipalities)
    {
        var seated = rows.Seats.Where(s => !IsCountyOrVoivodeship(s.UnitName)).Select(s => s.Unit).ToHashSet();
        var withTown = municipalities.Where(m => KindOf(types[m.Key]) == PlaceKind.Town).Select(m => m.Value).ToHashSet();
        return municipalities
            .Where(m => m.Value is { } unit
                && !seated.Contains(unit)
                && !withTown.Contains(unit)
                && rows.Names.TryGetValue(m.Key, out var name)
                && PlaceNames.Normalize(name) == PlaceNames.Normalize(MunicipalityName(rows.UnitNames[unit])))
            .GroupBy(m => m.Value)
            .Where(g => g.Count() == 1)
            .Select(g => g.Single().Key)
            .ToHashSet();
    }

    // The highest seat; a town is almost always the seat of its municipality.
    private static PlaceRole? RoleOf(IEnumerable<string> seatOf, IEnumerable<string> types, PlaceKind? kind, bool guessedSeat)
    {
        var units = seatOf.Where(IsCurrent).ToList();
        if (units.Any(u => VoivodeshipOf(u) is not null))
        {
            return PlaceRole.VoivodeshipCapital;
        }

        if (units.Any(u => u.StartsWith("powiat ", StringComparison.OrdinalIgnoreCase)) || types.Contains(CityWithCountyRights))
        {
            return PlaceRole.CountySeat;
        }

        return units.Any(u => !IsCountyOrVoivodeship(u))
               || kind == PlaceKind.Town
               || guessedSeat
            ? PlaceRole.MunicipalitySeat
            : null;
    }

    private static PlaceKind? KindOf(IEnumerable<string> types)
    {
        var list = types.ToList();
        return list.Any(TownTypes.Contains) ? PlaceKind.Town
            : list.Any(VillageTypes.Contains) ? PlaceKind.Village
            : null;
    }
}
