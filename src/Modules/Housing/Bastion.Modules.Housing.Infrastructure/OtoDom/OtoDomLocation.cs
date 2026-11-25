using System.Text.Json;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public static class OtoDomLocation
{
    // location.reverseGeocoding.locations lists the levels of the offer's location; the city_or_village level
    // has the id voivodeship/county/municipality/place in OtoDom's URL spelling, e.g.
    // "pomorskie/wejherowski/gmina-wiejska--wejherowo/goscicino" or "mazowieckie/warszawa/warszawa/warszawa".
    public static string? KeyOf(JsonElement item) =>
        LevelOf(item, "city_or_village") is { } level && level.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    // The levels below the place: "district" (e.g. "Mokotów") and "residential", a neighborhood inside the district
    // ("Stegny"). Big cities have both, many towns only a district, villages and some towns neither.
    public static (string? District, string? Neighborhood) DistrictOf(JsonElement item)
    {
        var district = NameOf(LevelOf(item, "district"));
        return district is null ? (null, null) : (district, NameOf(LevelOf(item, "residential")));
    }

    private static JsonElement? LevelOf(JsonElement item, string levelName)
    {
        if (!item.TryGetProperty("location", out var location) || location.ValueKind != JsonValueKind.Object
            || !location.TryGetProperty("reverseGeocoding", out var geocoding) || geocoding.ValueKind != JsonValueKind.Object
            || !geocoding.TryGetProperty("locations", out var levels) || levels.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var level in levels.EnumerateArray())
        {
            if (level.ValueKind == JsonValueKind.Object
                && level.TryGetProperty("locationLevel", out var name) && name.ValueKind == JsonValueKind.String
                && name.GetString() == levelName)
            {
                return level;
            }
        }

        return null;
    }

    private static string? NameOf(JsonElement? level) =>
        level is { } found && found.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(name.GetString())
            ? name.GetString()!.Trim()
            : null;

    // A '-' in a name is "--" in the key. A municipality named like its town is prefixed "gmina-wiejska--"
    // (rural) or "gmina-miejska--" (urban); a town and a village of the same name are prefixed "miasto--"/"wies--".
    // A city with county rights repeats its name as the county.
    public static LocationHint? HintOf(string key)
    {
        var parts = key.Split('/');
        if (parts.Length != 4)
        {
            return null;
        }

        var (voivodeship, county, municipality, place) = (parts[0], parts[1], parts[2], parts[3]);
        municipality = WithoutPrefix(WithoutPrefix(municipality, "gmina-wiejska--"), "gmina-miejska--");

        PlaceKind? kind = county == place ? PlaceKind.Town : null;
        if (place.StartsWith("miasto--", StringComparison.Ordinal))
        {
            (place, kind) = (place["miasto--".Length..], PlaceKind.Town);
        }
        else if (place.StartsWith("wies--", StringComparison.Ordinal))
        {
            (place, kind) = (place["wies--".Length..], PlaceKind.Village);
        }

        return new LocationHint(voivodeship, county, municipality, place, kind);
    }

    private static string WithoutPrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..] : value;
}
