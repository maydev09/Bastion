using System.Globalization;
using System.Text.Json;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public static class OtoDomDetailParser
{
    public static (string ExternalId, OfferDetails Details) Parse(string html)
    {
        var script = OtoDomListingParser.NextDataRegex().Match(html);
        if (!script.Success)
        {
            throw new OtoDomParseException("The page has no __NEXT_DATA__ script.");
        }

        using var document = OtoDomListingParser.ParseJson(script.Groups[1].Value);
        if (!OtoDomListingParser.TryGetPath(document.RootElement, out var ad, "props", "pageProps", "ad")
            || ad.ValueKind != JsonValueKind.Object
            || !ad.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number)
        {
            throw new OtoDomParseException("__NEXT_DATA__ has no props.pageProps.ad with an id.");
        }

        // An ended offer still has its page until the next full scan marks it removed; its details are not saved.
        var status = Text(ad, "status");
        if (status is not null && status != "active"
            || ad.TryGetProperty("shouldShowExpiredAdPage", out var expired) && expired.ValueKind == JsonValueKind.True)
        {
            throw new OtoDomParseException($"The offer is not active (status {status ?? "expired"}).");
        }

        // characteristics is a list of {key, value, currency} with plain values ("830", "floor_4", "2026-11-23").
        var characteristics = new Dictionary<string, JsonElement>();
        if (ad.TryGetProperty("characteristics", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (Text(item, "key") is { } key)
                {
                    characteristics[key] = item;
                }
            }
        }

        string? Value(string key) => characteristics.TryGetValue(key, out var item) ? Text(item, "value") : null;
        Money? MoneyOf(string key) =>
            characteristics.TryGetValue(key, out var item) && Decimal(Text(item, "value")) is { } amount && CurrencyOf(Text(item, "currency")) is { } currency
                ? new Money(amount, currency)
                : null;

        // Sellers type these by hand (a build year of 195014); an implausible number is kept only in RejectedValues,
        // under its housing.offer column name.
        var rejected = new Dictionary<string, string>();
        int? NumberBetween(string column, string? value, int min, int max)
        {
            // An empty value says nothing, like a missing one: unknown, not rejected.
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Integer(value) is { } number && number >= min && number <= max)
            {
                return number;
            }

            rejected[column] = value;
            return null;
        }

        // Planned buildings have future years; the oldest tenements to let are a few hundred years old.
        var buildYear = NumberBetween("build_year", Value("build_year"), 1000, 2100);
        var buildingFloors = NumberBetween("building_floors", Value("building_floors_num"), 0, 100);

        // mapDetails.radius is a JSON number (meters around the point), not text like the characteristics.
        OtoDomListingParser.TryGetPath(ad, out var location, "location");
        var radius = OtoDomListingParser.TryGetPath(location, out var radiusValue, "mapDetails", "radius")
            && radiusValue.ValueKind != JsonValueKind.Null
            ? NumberBetween("location_radius_m", radiusValue.ToString(), 0, 100_000)
            : null;

        var details = new OfferDetails(
            OtoDomListingParser.Plausible(MoneyOf("rent")),
            MoneyOf("deposit"),
            DateOnly.TryParseExact(Value("free_from"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) ? from : null,
            Floor(Value("floor_no")),
            buildingFloors,
            buildYear,
            BuildingTypeOf(Value("building_type")),
            HeatingOf(Value("heating")),
            ConditionOf(Value("construction_status")),
            Coordinate(location, "latitude"),
            Coordinate(location, "longitude"),
            radius,
            Text(ad, "description"),
            Features(ad),
            Furnished(ad),
            rejected.Count > 0 ? rejected : null);
        return (id.GetRawText(), details);
    }

    // "ground_floor", "floor_1" … "floor_10", "floor_higher_10" (no exact floor), "cellar", "garret".
    private static int? Floor(string? value) => value switch
    {
        "cellar" => -1,
        "ground_floor" => 0,
        _ when value?.StartsWith("floor_", StringComparison.Ordinal) == true => Integer(value["floor_".Length..]),
        _ => null,
    };

    private static BuildingType? BuildingTypeOf(string? value) => value switch
    {
        "block" => BuildingType.Block,
        "tenement" => BuildingType.Tenement,
        "apartment" => BuildingType.Apartment,
        "house" => BuildingType.House,
        "loft" => BuildingType.Loft,
        _ => null,
    };

    private static Heating? HeatingOf(string? value) => value switch
    {
        "urban" => Heating.District,
        "gas" => Heating.Gas,
        "electrical" => Heating.Electric,
        "boiler_room" => Heating.BoilerRoom,
        "tiled_stove" => Heating.TiledStove,
        _ => null,
    };

    private static Condition? ConditionOf(string? value) => value switch
    {
        "ready_to_use" => Condition.ReadyToUse,
        "to_renovation" => Condition.ToRenovate,
        "to_completion" => Condition.ToFinish,
        _ => null,
    };

    // additionalInformation is a list of {label, values}; values look like "extras_types::balcony" or
    // "security_types-102::monitoring". Air conditioning comes as "extras_types::air_conditioning" inside
    // equipment_types, so only the part after "::" counts.
    private static readonly string[] FeatureGroups = ["extras_types", "equipment_types", "media_types", "security_types"];

    // Null when the page has none of the groups: unknown, so the stored features are kept.
    private static List<Feature>? Features(JsonElement ad)
    {
        if (ReadGroups(ad) is not { } groups)
        {
            return null;
        }

        var features = new List<Feature>();
        foreach (var value in groups.Where(g => FeatureGroups.Contains(g.Key)).SelectMany(g => g.Value))
        {
            if (FeatureOf(value) is { } feature && !features.Contains(feature))
            {
                features.Add(feature);
            }
        }

        return groups.Keys.Any(FeatureGroups.Contains) ? features : null;
    }

    // OtoDom has no furnished field, only "furniture" among the equipment the advertiser ticked: other equipment
    // without it means an unfurnished flat, no equipment at all means not stated.
    private static Answer? Furnished(JsonElement ad)
    {
        if (ReadGroups(ad) is not { } groups)
        {
            return null;
        }

        var values = groups.Where(g => FeatureGroups.Contains(g.Key)).SelectMany(g => g.Value).ToList();
        var equipment = groups.GetValueOrDefault("equipment_types") ?? [];
        return values.Contains("furniture") ? Answer.Yes
            : equipment.Count > 0 ? Answer.No
            : Answer.NotStated;
    }

    // Each group's values without their "group::" prefix; null without additionalInformation.
    private static Dictionary<string, List<string>>? ReadGroups(JsonElement ad)
    {
        if (!ad.TryGetProperty("additionalInformation", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var groups = new Dictionary<string, List<string>>();
        foreach (var group in list.EnumerateArray())
        {
            if (Text(group, "label") is not { } label
                || !group.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            groups[label] = values.EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : "")
                .Where(v => v.Contains("::", StringComparison.Ordinal))
                .Select(v => v[(v.IndexOf("::", StringComparison.Ordinal) + 2)..])
                .ToList();
        }

        return groups;
    }

    private static Feature? FeatureOf(string value) => value switch
    {
        "balcony" => Feature.Balcony,
        "terrace" => Feature.Terrace,
        "garden" => Feature.Garden,
        "lift" => Feature.Lift,
        "basement" => Feature.Basement,
        "garage" => Feature.Parking,
        "air_conditioning" => Feature.AirConditioning,
        "dishwasher" => Feature.Dishwasher,
        "washing_machine" => Feature.WashingMachine,
        "fridge" => Feature.Fridge,
        "stove" => Feature.Stove,
        "oven" => Feature.Oven,
        "separate_kitchen" => Feature.SeparateKitchen,
        "usable_room" => Feature.UsableRoom,
        "two_storey" => Feature.TwoStorey,
        "internet" => Feature.Internet,
        "cable-television" => Feature.CableTelevision,
        "entryphone" => Feature.Entryphone,
        "monitoring" => Feature.Monitoring,
        "closed_area" => Feature.ClosedArea,
        "anti_burglary_door" => Feature.AntiBurglaryDoor,
        "non_smokers_only" => Feature.NonSmokersOnly,
        _ => null,
    };

    private static Currency? CurrencyOf(string? value) => value switch
    {
        "PLN" => Currency.Pln,
        _ => null,
    };

    private static decimal? Coordinate(JsonElement location, string name) =>
        OtoDomListingParser.TryGetPath(location, out var number, "coordinates", name) && number.ValueKind == JsonValueKind.Number
            ? number.GetDecimal()
            : null;

    private static decimal? Decimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static int? Integer(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String
            ? text.GetString()
            : null;
}
