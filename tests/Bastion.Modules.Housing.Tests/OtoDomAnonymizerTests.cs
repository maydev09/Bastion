using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bastion.Modules.Housing.Infrastructure.OtoDom;

namespace Bastion.Modules.Housing.Tests;

public class OtoDomAnonymizerTests
{
    private static readonly Uri BaseUrl = new("https://www.otodom.pl");

    [Test]
    public void Keeps_an_agency_advertiser_and_masks_its_agent()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"contactDetails":{"name":"Anna Nowak"},
              "unifiedAd":{"ownerAccount":{"name":"Best Homes Ltd","type":"AGENCY"}},
              "ad":{"id":1,"advertiserType":"business","owner":{"name":"Best Homes Ltd","contacts":[{"name":"Anna Nowak"}]},
                "contactDetails":{"name":"Anna Nowak"},"agency":{"name":"Best Homes Ltd"}}}}}
            </script>
            """;

        var redacted = OtoDomAnonymizer.Anonymize(html, "***");

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Not.Contain("Anna Nowak"));
            Assert.That(redacted, Does.Contain("Best Homes Ltd"));
        });
    }

    [Test]
    public void Masks_the_advertiser_of_a_detail_page_and_keeps_the_agency()
    {
        const string html = """
            <h1>Jan Kowalski</h1>
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"contactDetails":{"name":"Jan Kowalski","type":"private"},
              "unifiedAd":{"contactDetails":[{"name":"Jan Kowalski"}],"description":"<p>Call 600 100 200</p>","resolvedPhone":"600100200",
                "ownerAccount":{"name":"Jan Kowalski","type":"private"}},
              "ad":{"id":1,"description":"<p>Call 600 100 200</p>",
                "owner":{"name":"Jan Kowalski","address":"ul. Prosta 1","contacts":[{"name":"Jan Kowalski"}]},
                "contactDetails":{"name":"Jan Kowalski"},
                "agency":{"name":"Best Homes Ltd"}}}}}
            </script>
            """;

        var redacted = OtoDomAnonymizer.Anonymize(html, "***");

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Not.Contain("Kowalski").And.Not.Contain("Prosta").And.Not.Contain("600 100 200").And.Not.Contain("600100200"));
            Assert.That(redacted, Does.Contain("Best Homes Ltd").And.Contain("Call ***"));
        });
    }

    [Test]
    public void Masks_personal_data_and_keeps_the_rest()
    {
        const string html = """
            <div class="card">Jan Kowalski, tel. 600 100 200</div>
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"userSessionId":"session-1","cookiesCategorisation":{"dfp_user_id":"ad-user-1"},"data":{"searchAds":{"items":[
              {"id":1,"title":"Mieszkanie w Łodzi","slug":"flat-ID1","estate":"FLAT","transaction":"RENT",
               "advertOwner":{"name":"Jan Kowalski","contacts":[{"name":"Jan Kowalski"}],"type":"PRIVATE"},
               "organisationAssignedMember":{"firstName":"Anna","lastName":"Nowak","userID":"42"},
               "shortDescription":"Dzwoń 600-100-200 albo pisz: jan@example.com"}
            ]}}}}}
            </script>
            """;

        var redacted = OtoDomAnonymizer.Anonymize(html, "***");
        var script = Regex.Match(redacted, """<script id="__NEXT_DATA__"[^>]*>(.*?)</script>""", RegexOptions.Singleline);
        var data = JsonNode.Parse(script.Groups[1].Value)!;
        var item = data["props"]!["pageProps"]!["data"]!["searchAds"]!["items"]![0]!;

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Not.Contain("Jan Kowalski").And.Not.Contain("Anna").And.Not.Contain("Nowak").And.Not.Contain("600"));
            Assert.That(redacted, Does.Not.Contain("jan@example.com").And.Not.Contain("session-1").And.Not.Contain("ad-user-1"));
            Assert.That(item["advertOwner"]!["name"]!.GetValue<string>(), Is.EqualTo("***"));
            Assert.That(item["advertOwner"]!["type"]!.GetValue<string>(), Is.EqualTo("PRIVATE"));
            Assert.That(item["organisationAssignedMember"]!["userID"]!.GetValue<string>(), Is.EqualTo("42"));
            Assert.That(item["shortDescription"]!.GetValue<string>(), Is.EqualTo("Dzwoń *** albo pisz: ***"));
            Assert.That(redacted, Does.Contain("Mieszkanie w Łodzi"));
        });
    }

    [Test]
    public void Parser_reads_the_same_offers_from_a_masked_page()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "otodom-listing.html"));

        var original = OtoDomListingParser.Parse(html, BaseUrl);
        var masked = OtoDomListingParser.Parse(OtoDomAnonymizer.Anonymize(html, "***"), BaseUrl);

        // Features is a list, so offers are compared by their stored fields (the listing hash).
        Assert.That(masked.Offers.Select(o => o.Offer.ListingHash()), Is.EqualTo(original.Offers.Select(o => o.Offer.ListingHash())));
    }
}
