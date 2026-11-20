using System.Globalization;
using System.Text.Json;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Infrastructure.Wikidata;

// Polish places are the Wikidata items with a SIMC id (P4046). One big query hits the query
// service's 60-second limit, so the data comes in several small ones.
public sealed class WikidataPlaceSource(WikidataClient client) : IPlaceSource
{
    public const string Unavailable = "housing.placesUnavailable";
    public const string Incomplete = "housing.placesIncomplete";

    // Wikidata had 65 663 Polish places in November 2026; far fewer means a broken response.
    public const int MinPlaces = 50_000;

    // VALUES lists of this many items keep a query short.
    private const int UnitBatchSize = 400;

    private const string SimcsQuery = """
        SELECT ?item ?simc ?name WHERE {
          ?item wdt:P4046 ?simc.
          OPTIONAL { ?item rdfs:label ?name FILTER(LANG(?name) = "pl") }
        }
        """;

    private const string TypesQuery = "SELECT ?item ?type WHERE { ?item wdt:P4046 []; wdt:P31 ?type }";

    // "Located in" (P131) also keeps former units with an end date (P582), e.g. Toruń in the pre-war
    // Pomeranian voivodeship; those and deprecated statements are skipped.
    private const string CurrentParent = """
        ?statement ps:P131 ?parent; wikibase:rank ?rank.
        FILTER(?rank != wikibase:DeprecatedRank)
        FILTER NOT EXISTS { ?statement pq:P582 [] }
        """;

    private const string ParentsQuery = $$"""
        SELECT ?item ?parent WHERE { ?item wdt:P4046 []; p:P131 ?statement. {{CurrentParent}} }
        """;

    private const string UnitParentsQuery = $$"""
        SELECT DISTINCT ?item ?parent WHERE { [] wdt:P4046 []; wdt:P131 ?item. ?item p:P131 ?statement. {{CurrentParent}} }
        """;

    private const string UpperUnitParentsQuery = $$"""
        SELECT DISTINCT ?item ?parent WHERE { [] wdt:P4046 []; wdt:P131/wdt:P131 ?item. ?item p:P131 ?statement. {{CurrentParent}} }
        """;

    // Units whose current seat the place is: "capital of" (P1376) on the place or "capital" (P36) on the unit.
    // Only municipalities, counties and voivodeships; former ones (end date, dissolved) are skipped.
    private const string SeatsQuery = """
        SELECT DISTINCT ?item ?name WHERE {
          { ?item wdt:P4046 []; p:P1376 ?statement. ?statement ps:P1376 ?unit. }
          UNION { ?unit p:P36 ?statement. ?statement ps:P36 ?item. ?item wdt:P4046 []. }
          FILTER NOT EXISTS { ?statement pq:P582 [] }
          FILTER NOT EXISTS { ?unit wdt:P576 [] }
          ?unit rdfs:label ?name FILTER(LANG(?name) = "pl")
          FILTER(STRSTARTS(LCASE(?name), "gmina ") || STRSTARTS(?name, "powiat ") || STRSTARTS(?name, "województwo "))
        }
        """;

    // The Polish label, else the multilingual one ("mul"): many items have only that, e.g. Rumia, Żory, Konin.
    private const string NamesQuery = """
        SELECT ?item ?name WHERE {
          VALUES ?item { {0} }
          OPTIONAL { ?item rdfs:label ?pl FILTER(LANG(?pl) = "pl") }
          OPTIONAL { ?item rdfs:label ?mul FILTER(LANG(?mul) = "mul") }
          BIND(COALESCE(?pl, ?mul) AS ?name)
          FILTER(BOUND(?name))
        }
        """;

    // Population statements carry the census date (P585), e.g. 2021-03-31 for the 2021 census.
    private const string PopulationsQuery = """
        SELECT ?item ?population ?date WHERE {
          ?item wdt:P4046 []; p:P1082 ?statement.
          ?statement ps:P1082 ?population.
          OPTIONAL { ?statement pq:P585 ?date }
        }
        """;

    private const string CoordinatesQuery = "SELECT ?item ?point WHERE { ?item wdt:P4046 []; wdt:P625 ?point }";

    public async Task<PlaceFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Place> places;
        try
        {
            places = WikidataPlaces.Build(await ReadAsync(cancellationToken));
        }
        catch (Exception e) when (WikidataClient.IsTransient(e, cancellationToken) || e is FormatException)
        {
            return PlaceFetchResult.Failed(new Failure(Unavailable, $"Wikidata did not return the places: {e.Message}", FailureKind.Unavailable));
        }

        return places.Count < MinPlaces
            ? PlaceFetchResult.Failed(new Failure(Incomplete,
                $"Wikidata returned only {places.Count} places, expected at least {MinPlaces}; nothing was saved.", FailureKind.Failed))
            : new PlaceFetchResult(places);
    }

    private async Task<WikidataRows> ReadAsync(CancellationToken cancellationToken)
    {
        var simcs = await client.QueryAsync(SimcsQuery, cancellationToken);
        var types = await client.QueryAsync(TypesQuery, cancellationToken);
        var parents = (await client.QueryAsync(ParentsQuery, cancellationToken))
            .Concat(await client.QueryAsync(UnitParentsQuery, cancellationToken))
            .Concat(await client.QueryAsync(UpperUnitParentsQuery, cancellationToken))
            .Select(r => (Item: r["item"], Parent: r["parent"]))
            .Distinct()
            .ToList();
        var populations = await client.QueryAsync(PopulationsQuery, cancellationToken);
        var coordinates = await client.QueryAsync(CoordinatesQuery, cancellationToken);
        var seats = await client.QueryAsync(SeatsQuery, cancellationToken);

        var unitNames = await NamesAsync(parents.Select(p => p.Parent).Distinct(), cancellationToken);

        var names = await NamesAsync(simcs.Where(r => !r.ContainsKey("name")).Select(r => r["item"]).Distinct(), cancellationToken);
        foreach (var row in simcs)
        {
            if (row.TryGetValue("name", out var name))
            {
                names[row["item"]] = name;
            }
        }

        return new WikidataRows(
            simcs.Select(r => (r["item"], r["simc"])).ToList(),
            names,
            types.Select(r => (r["item"], r["type"])).ToList(),
            parents,
            unitNames,
            populations.Select(r => (r["item"], decimal.Parse(r["population"], CultureInfo.InvariantCulture), DateOf(r))).ToList(),
            coordinates.Select(r => Point(r["item"], r["point"])).Where(p => p is not null).Select(p => p!.Value).ToList(),
            seats.Select(r => (r["item"], r["name"])).ToList());
    }

    private async Task<Dictionary<string, string>> NamesAsync(IEnumerable<string> items, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>();
        foreach (var batch in items.Order().Chunk(UnitBatchSize))
        {
            var query = NamesQuery.Replace("{0}", string.Join(' ', batch.Select(u => $"wd:{u}")), StringComparison.Ordinal);
            foreach (var row in await client.QueryAsync(query, cancellationToken))
            {
                names[row["item"]] = row["name"];
            }
        }

        return names;
    }

    // An unreadable date (e.g. a year before Christ) counts as no date.
    private static DateTimeOffset? DateOf(IReadOnlyDictionary<string, string> row) =>
        row.TryGetValue("date", out var text)
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;

    // Coordinates are a WKT literal "Point(longitude latitude)"; a few items use another globe and are skipped.
    private static (string, decimal, decimal)? Point(string item, string wkt)
    {
        if (!wkt.StartsWith("Point(", StringComparison.Ordinal) || !wkt.EndsWith(')'))
        {
            return null;
        }

        var parts = wkt["Point(".Length..^1].Split(' ');
        return parts.Length == 2
            && decimal.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            && decimal.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                ? (item, latitude, longitude)
                : null;
    }
}
