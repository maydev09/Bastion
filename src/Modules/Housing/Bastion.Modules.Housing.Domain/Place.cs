namespace Bastion.Modules.Housing.Domain;

// Table housing.place: a city, town or village from the official register (SIMC id).
public sealed record Place(
    string Simc,
    string Name,
    Voivodeship Voivodeship,
    string? County = null,
    string? Municipality = null,
    PlaceKind? Kind = null,
    PlaceRole? Role = null,
    int? Population = null,
    short? PopulationYear = null,
    PlaceSource? PopulationSource = null,
    decimal? Latitude = null,
    decimal? Longitude = null)
{
    public int Id { get; init; }

    public string NameKey => PlaceNames.Normalize(Name);
}
