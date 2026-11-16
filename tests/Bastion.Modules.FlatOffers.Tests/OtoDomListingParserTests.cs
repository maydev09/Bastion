using Bastion.Modules.FlatOffers.Infrastructure.OtoDom;

namespace Bastion.Modules.FlatOffers.Tests;

public class OtoDomListingParserTests
{
    private static readonly Uri BaseUrl = new("https://www.otodom.pl");

    [Test]
    public void Parses_offers_from_a_saved_results_page()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "otodom-listing.html"));

        var (offers, skipped) = OtoDomListingParser.Parse(html, BaseUrl);

        Assert.Multiple(() =>
        {
            Assert.That(offers, Has.Count.EqualTo(2));
            Assert.That(skipped, Is.Zero);
            Assert.That(offers[0].ExternalId, Is.EqualTo("10000001"));
            Assert.That(offers[0].Title, Is.EqualTo("Sample flat 30 m2, first rental"));
            Assert.That(offers[0].Url, Is.EqualTo(new Uri("https://www.otodom.pl/pl/oferta/sample-flat-1-ID0SAMPLE1")));
            Assert.That((offers[0].Price, offers[0].PriceCurrency), Is.EqualTo((2200m, "PLN")));
            Assert.That(offers[0].AreaM2, Is.EqualTo(30m));
            Assert.That(offers[0].Rooms, Is.EqualTo(1));
        });
    }

    [Test]
    public void Skips_items_without_required_fields()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"data":{"searchAds":{"items":[
              {"id":1,"title":"Complete","slug":"complete-ID1"},
              {"id":2,"title":"No slug"}
            ]}}}}}
            </script>
            """;

        var (offers, skipped) = OtoDomListingParser.Parse(html, BaseUrl);

        Assert.Multiple(() =>
        {
            Assert.That(offers.Select(o => o.ExternalId), Is.EqualTo(new[] { "1" }));
            Assert.That(skipped, Is.EqualTo(1));
        });
    }

    [TestCase("<html>no data</html>", "no __NEXT_DATA__")]
    [TestCase("""<script id="__NEXT_DATA__">{not json</script>""", "not valid JSON")]
    [TestCase("""<script id="__NEXT_DATA__">{"props":{}}</script>""", "searchAds.items")]
    public void Reports_pages_it_cannot_read(string html, string expectedMessage)
    {
        var error = Assert.Throws<OtoDomParseException>(() => OtoDomListingParser.Parse(html, BaseUrl));

        Assert.That(error.Message, Does.Contain(expectedMessage));
    }
}
