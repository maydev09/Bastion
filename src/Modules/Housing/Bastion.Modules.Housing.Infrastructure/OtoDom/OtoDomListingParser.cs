using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public static partial class OtoDomListingParser
{
    public static (IReadOnlyList<Offer> Offers, int SkippedCount, bool IsLastPage) Parse(string html, Uri baseUrl)
    {
        var script = NextDataRegex().Match(html);
        if (!script.Success)
        {
            throw new OtoDomParseException("The page has no __NEXT_DATA__ script.");
        }

        // OtoDom is a Next.js app: the offers are in the page data, not in the HTML markup.
        using var document = ParseJson(script.Groups[1].Value);
        if (!TryGetPath(document.RootElement, out var items, "props", "pageProps", "data", "searchAds", "items")
            || items.ValueKind != JsonValueKind.Array)
        {
            throw new OtoDomParseException("__NEXT_DATA__ has no props.pageProps.data.searchAds.items array.");
        }

        var offers = new List<Offer>();
        var skipped = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number
                && item.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
                && item.TryGetProperty("slug", out var slug) && slug.ValueKind == JsonValueKind.String
                && PropertyTypeOf(item) is { } propertyType
                && TransactionOf(item) is { } transaction
                && Date(item, "createdAtFirst") is { } createdAt)
            {
                offers.Add(new Offer(
                    Source.OtoDom,
                    id.GetRawText(),
                    title.GetString()!,
                    new Uri(baseUrl, $"/pl/oferta/{slug.GetString()}"),
                    propertyType,
                    transaction,
                    createdAt,
                    MoneyOf(item, "totalPrice"),
                    Number(item, "areaInSquareMeters"),
                    Rooms(item),
                    Date(item, "pushedUpAt"),
                    OtoDomLocation.KeyOf(item),
                    Rent: Plausible(MoneyOf(item, "rentPrice")),
                    Floor: Floor(item),
                    Features: Features(item)));
            }
            else
            {
                skipped++;
            }
        }

        // Promoted offers can appear twice on one page, and the page lists the newest offer first.
        // Saving them oldest first gives newer offers higher ids, which orders offers of one import by age.
        return (offers.DistinctBy(o => o.ExternalId).Reverse().ToList(), skipped, IsLastPage(document.RootElement));
    }

    // Without page numbers there is nothing to read next.
    private static bool IsLastPage(JsonElement root) =>
        !TryGetPath(root, out var pagination, "props", "pageProps", "data", "searchAds", "pagination")
        || Number(pagination, "currentPage") is not { } current
        || Number(pagination, "totalPages") is not { } total
        || current >= total;

    // totalPrice is null when the advertiser hides the price; for rentals it is the monthly rent.
    private static Money? MoneyOf(JsonElement item, string name) =>
        item.TryGetProperty(name, out var money) && money.ValueKind == JsonValueKind.Object
        && Number(money, "value") is { } amount
        && CurrencyOf(money) is { } currency
            ? new Money(amount, currency)
            : null;

    // The rent field is required in OtoDom's form, so some advertisers type 1 zł instead of leaving it out.
    // 0 is kept: no fees, or fees included in the price.
    internal static Money? Plausible(Money? rent) => rent is { Amount: > 0 and < 10 } ? null : rent;

    // floorNumber is a word ("GROUND", "FIRST" … "TENTH"); "ABOVE_TENTH" has no exact floor.
    private static int? Floor(JsonElement item) => Text(item, "floorNumber") switch
    {
        "CELLAR" => -1,
        "GROUND" => 0,
        "FIRST" => 1,
        "SECOND" => 2,
        "THIRD" => 3,
        "FOURTH" => 4,
        "FIFTH" => 5,
        "SIXTH" => 6,
        "SEVENTH" => 7,
        "EIGHTH" => 8,
        "NINTH" => 9,
        "TENTH" => 10,
        _ => null,
    };

    // tags is a list of {value, weight}; only some of them are features, the rest (TOP_FLOOR, SEPARATE_KITCHEN…) are ignored.
    private static List<Feature> Features(JsonElement item)
    {
        var features = new List<Feature>();
        if (!item.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return features;
        }

        foreach (var tag in tags.EnumerateArray())
        {
            Feature? feature = tag.ValueKind == JsonValueKind.Object ? Text(tag, "value") switch
            {
                "BALCONY" => Feature.Balcony,
                "TERRACE" => Feature.Terrace,
                "GARDEN" => Feature.Garden,
                "BASEMENT" => Feature.Basement,
                "PARKING_SPOT" => Feature.Parking,
                "AIR_CONDITIONING" => Feature.AirConditioning,
                _ => null,
            } : null;
            if (feature is { } value && !features.Contains(value))
            {
                features.Add(value);
            }
        }

        return features;
    }

    private static Currency? CurrencyOf(JsonElement money) => Text(money, "currency") switch
    {
        "PLN" => Currency.Pln,
        _ => null,
    };

    private static decimal? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var number) && number.ValueKind == JsonValueKind.Number
            ? number.GetDecimal()
            : null;

    private static PropertyType? PropertyTypeOf(JsonElement item) => Text(item, "estate") switch
    {
        "FLAT" => PropertyType.Flat,
        _ => null,
    };

    private static Transaction? TransactionOf(JsonElement item) => Text(item, "transaction") switch
    {
        "RENT" => Transaction.Rent,
        _ => null,
    };

    // An advertising tile at the end of every results page has the invalid date 1999-02-29, so it is skipped.
    // pushedUpAt is set when the advertiser bumps the offer; it keeps its place in the newest-first listing.
    // Dates come with different offsets (Z, +01:00) and are stored in UTC.
    private static DateTimeOffset? Date(JsonElement element, string name) =>
        Text(element, name) is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            ? date
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String
            ? text.GetString()
            : null;

    // roomsNumber is a word ("ONE" … "TEN"); "MORE" (over ten) has no exact count.
    private static int? Rooms(JsonElement item)
    {
        if (!item.TryGetProperty("roomsNumber", out var rooms) || rooms.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return rooms.GetString() switch
        {
            "ONE" => 1,
            "TWO" => 2,
            "THREE" => 3,
            "FOUR" => 4,
            "FIVE" => 5,
            "SIX" => 6,
            "SEVEN" => 7,
            "EIGHT" => 8,
            "NINE" => 9,
            "TEN" => 10,
            _ => null,
        };
    }

    internal static JsonDocument ParseJson(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new OtoDomParseException($"__NEXT_DATA__ is not valid JSON: {e.Message}");
        }
    }

    internal static bool TryGetPath(JsonElement element, out JsonElement result, params string[] path)
    {
        result = element;
        foreach (var name in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(name, out result))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex("""<script id="__NEXT_DATA__"[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    internal static partial Regex NextDataRegex();
}
