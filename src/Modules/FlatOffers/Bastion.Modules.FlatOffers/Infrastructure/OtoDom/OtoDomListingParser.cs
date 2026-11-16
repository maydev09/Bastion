using System.Text.Json;
using System.Text.RegularExpressions;
using Bastion.Modules.FlatOffers.Domain;

namespace Bastion.Modules.FlatOffers.Infrastructure.OtoDom;

internal static partial class OtoDomListingParser
{
    public static (IReadOnlyList<ListingOffer> Offers, int SkippedCount) Parse(string html, Uri baseUrl)
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

        var offers = new List<ListingOffer>();
        var skipped = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number
                && item.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
                && item.TryGetProperty("slug", out var slug) && slug.ValueKind == JsonValueKind.String)
            {
                var price = Money(item, "totalPrice");
                offers.Add(new ListingOffer(
                    id.GetRawText(),
                    title.GetString()!,
                    new Uri(baseUrl, $"/pl/oferta/{slug.GetString()}"),
                    price?.Value,
                    price?.Currency,
                    Number(item, "areaInSquareMeters"),
                    Rooms(item)));
            }
            else
            {
                skipped++;
            }
        }

        return (offers, skipped);
    }

    // totalPrice is null when the advertiser hides the price; for rentals it is the monthly rent.
    private static (decimal Value, string Currency)? Money(JsonElement item, string name) =>
        item.TryGetProperty(name, out var money) && money.ValueKind == JsonValueKind.Object
        && Number(money, "value") is { } value
        && money.TryGetProperty("currency", out var currency) && currency.ValueKind == JsonValueKind.String
            ? (value, currency.GetString()!)
            : null;

    private static decimal? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var number) && number.ValueKind == JsonValueKind.Number
            ? number.GetDecimal()
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
    private static partial Regex NextDataRegex();
}

internal sealed class OtoDomParseException(string message) : Exception(message);
