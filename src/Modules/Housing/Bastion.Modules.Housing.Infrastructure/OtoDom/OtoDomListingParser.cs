using System.Text.Json;
using System.Text.RegularExpressions;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public static partial class OtoDomListingParser
{
    public static (IReadOnlyList<Offer> Offers, int SkippedCount) Parse(string html, Uri baseUrl)
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
                && TransactionOf(item) is { } transaction)
            {
                offers.Add(new Offer(
                    Source.OtoDom,
                    id.GetRawText(),
                    title.GetString()!,
                    new Uri(baseUrl, $"/pl/oferta/{slug.GetString()}"),
                    propertyType,
                    transaction,
                    MoneyOf(item, "totalPrice"),
                    Number(item, "areaInSquareMeters"),
                    Rooms(item)));
            }
            else
            {
                skipped++;
            }
        }

        // Promoted offers can appear twice on one page, and the page lists the newest offer first.
        // Saving them oldest first gives newer offers higher ids, which orders offers of one import by age.
        return (offers.DistinctBy(o => o.ExternalId).Reverse().ToList(), skipped);
    }

    // totalPrice is null when the advertiser hides the price; for rentals it is the monthly rent.
    private static Money? MoneyOf(JsonElement item, string name) =>
        item.TryGetProperty(name, out var money) && money.ValueKind == JsonValueKind.Object
        && Number(money, "value") is { } amount
        && CurrencyOf(money) is { } currency
            ? new Money(amount, currency)
            : null;

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

    private static JsonDocument ParseJson(string json)
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

    private static bool TryGetPath(JsonElement element, out JsonElement result, params string[] path)
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
