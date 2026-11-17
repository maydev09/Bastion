using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public static partial class OtoDomAnonymizer
{
    private static readonly string[] PeopleFields = ["advertOwner", "organisationAssignedMember"];

    private static readonly string[] PersonalFields = ["name", "firstName", "lastName", "imageUrl", "photoURL"];

    private static readonly string[] FreeTextFields = ["shortDescription"];

    private static readonly string[] SessionFields = ["userSessionId", "unconfirmedUserAuthToken"];

    // Keeps Polish letters readable and still escapes <, > and & inside <script>.
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public static string Anonymize(string html, string anonymizationMask)
    {
        var script = OtoDomListingParser.NextDataRegex().Match(html);
        JsonNode? root;
        try
        {
            root = script.Success ? JsonNode.Parse(script.Groups[1].Value) : null;
        }
        catch (JsonException)
        {
            root = null;
        }

        if (root?["props"]?["pageProps"] is not JsonObject pageProps)
        {
            return MaskContacts(html, anonymizationMask);
        }

        foreach (var field in SessionFields.Where(pageProps.ContainsKey))
        {
            pageProps[field] = anonymizationMask;
        }

        if (pageProps["cookiesCategorisation"] is JsonObject cookies && cookies.ContainsKey("dfp_user_id"))
        {
            cookies["dfp_user_id"] = anonymizationMask;
        }

        var masked = new HashSet<string>(StringComparer.Ordinal);
        if (pageProps["data"]?["searchAds"]?["items"] is JsonArray items)
        {
            foreach (var node in items)
            {
                if (node is not JsonObject item)
                {
                    continue;
                }

                foreach (var field in PeopleFields)
                {
                    if (item[field] is { } person)
                    {
                        MaskPersonalFields(person, masked, anonymizationMask);
                    }
                }

                foreach (var field in FreeTextFields)
                {
                    if (item[field] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        item[field] = MaskContacts(text, anonymizationMask);
                    }
                }
            }
        }

        // The offer cards in the markup repeat the names and photo links.
        var data = script.Groups[1];
        var before = MaskTexts(html[..data.Index], masked, anonymizationMask);
        var after = MaskTexts(html[(data.Index + data.Length)..], masked, anonymizationMask);
        return before + root.ToJsonString(Json) + after;
    }

    private static void MaskPersonalFields(JsonNode node, HashSet<string> masked, string anonymizationMask)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    MaskPersonalFields(child, masked, anonymizationMask);
                }
            }
        }
        else if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(p => p.Key).ToList())
            {
                if (PersonalFields.Contains(key) && obj[key] is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    masked.Add(text);
                    obj[key] = anonymizationMask;
                }
                else if (obj[key] is { } child)
                {
                    MaskPersonalFields(child, masked, anonymizationMask);
                }
            }
        }
    }

    private static string MaskTexts(string html, HashSet<string> texts, string anonymizationMask)
    {
        foreach (var text in texts.Where(t => t.Length >= 3).OrderByDescending(t => t.Length))
        {
            html = html.Replace(text, anonymizationMask, StringComparison.Ordinal);
        }

        return MaskContacts(html, anonymizationMask);
    }

    private static string MaskContacts(string text, string anonymizationMask) =>
        PhoneRegex().Replace(EmailRegex().Replace(text, anonymizationMask), anonymizationMask);

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex EmailRegex();

    // 600 100 200, 600-100-200, 600100200, +48 600 100 200
    [GeneratedRegex(@"(?<![\d.,])(\+48[\s-]?)?\d{3}[\s-]?\d{3}[\s-]?\d{3}(?![\d.,])")]
    private static partial Regex PhoneRegex();
}
