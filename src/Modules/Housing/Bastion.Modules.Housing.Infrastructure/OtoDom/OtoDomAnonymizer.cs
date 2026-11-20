using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public static partial class OtoDomAnonymizer
{
    private static readonly string[] PeopleFields = ["advertOwner", "organisationAssignedMember"];

    private static readonly string[] PersonalFields = ["name", "firstName", "lastName", "imageUrl", "photoURL", "imageURLSmall"];

    // A detail page repeats the advertiser in several places; an agency's own object (ad.agency) is kept.
    // The contact person of a detail page, repeated in several places; for an agency it is an agent.
    private static readonly string[][] DetailContactPaths =
        [["ad", "contactDetails"], ["contactDetails"], ["unifiedAd", "contactDetails"], ["ad", "owner", "contacts"]];

    // The advertiser: a private person is masked, an agency's name is kept (ADR 0016).
    private static readonly string[][] DetailOwnerPaths = [["ad", "owner"], ["unifiedAd", "ownerAccount"]];

    // unifiedAd repeats the offer for another part of the page, the description included.
    private static readonly string[][] DetailFreeTextPaths = [["ad", "description"], ["unifiedAd", "description"], ["unifiedAd", "aiSummary"]];

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

        if (pageProps["ad"] is JsonObject ad)
        {
            var isAgency = ad["advertiserType"] is JsonValue type && type.TryGetValue<string>(out var advertiser) && advertiser == "business";
            foreach (var path in isAgency ? DetailContactPaths : [.. DetailContactPaths, .. DetailOwnerPaths])
            {
                if (Node(pageProps, path) is { } person)
                {
                    MaskPersonalFields(person, masked, anonymizationMask);
                }
            }

            // A private advertiser's address; it is not added to the masked texts, a town name would be masked everywhere.
            if (!isAgency && ad["owner"] is JsonObject owner && owner["address"] is JsonValue)
            {
                owner["address"] = anonymizationMask;
            }

            if (pageProps["unifiedAd"] is JsonObject unifiedAd && unifiedAd["resolvedPhone"] is JsonValue)
            {
                unifiedAd["resolvedPhone"] = anonymizationMask;
            }

            foreach (var path in DetailFreeTextPaths)
            {
                if (Node(pageProps, path[..^1]) is JsonObject parent
                    && parent[path[^1]] is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    parent[path[^1]] = MaskContacts(text, anonymizationMask);
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

    private static JsonNode? Node(JsonNode node, string[] path)
    {
        JsonNode? current = node;
        foreach (var name in path)
        {
            current = current is JsonObject obj ? obj[name] : null;
        }

        return current;
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
