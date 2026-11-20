namespace Bastion.Modules.Housing.Infrastructure.Wikidata;

// Query results about places with a SIMC id; items and units are Wikidata ids such as Q270.
// Seats name the units whose seat the item is, e.g. (Q1792, "powiat złotowski").
public sealed record WikidataRows(
    IReadOnlyList<(string Item, string Simc)> Simcs,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyList<(string Item, string Type)> Types,
    IReadOnlyList<(string Item, string Parent)> Parents,
    IReadOnlyDictionary<string, string> UnitNames,
    IReadOnlyList<(string Item, decimal Population, DateTimeOffset? Date)> Populations,
    IReadOnlyList<(string Item, decimal Latitude, decimal Longitude)> Coordinates,
    IReadOnlyList<(string Item, string UnitName)> Seats);
