using Bastion.Modules.Housing.Domain;
using Bastion.Modules.Housing.Infrastructure.OtoDom;

namespace Bastion.Modules.Housing.Tests;

public class OtoDomListingParserTests
{
    private static readonly Uri BaseUrl = new("https://www.otodom.pl");

    [Test]
    public void Parses_offers_from_a_saved_results_page()
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "otodom-listing.html"));

        var (offers, skipped, _) = OtoDomListingParser.Parse(html, BaseUrl);
        var offer = offers.Single(o => o.ExternalId == "10000001");

        Assert.Multiple(() =>
        {
            Assert.That(offers, Has.Count.EqualTo(2));
            Assert.That(skipped, Is.Zero);
            Assert.That(offer.Title, Is.EqualTo("Sample flat 30 m2, first rental"));
            Assert.That(offer.Url, Is.EqualTo(new Uri("https://www.otodom.pl/pl/oferta/sample-flat-1-ID0SAMPLE1")));
            Assert.That(offer.Price, Is.EqualTo(new Money(2200m, Currency.Pln)));
            Assert.That(offer.AreaM2, Is.EqualTo(30m));
            Assert.That(offer.Rooms, Is.EqualTo(1));
            Assert.That((offer.PropertyType, offer.Transaction), Is.EqualTo((PropertyType.Flat, Transaction.Rent)));
        });
    }

    [Test]
    public void Keeps_the_place_level_of_the_location()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"data":{"searchAds":{"items":[
              {"id":1,"title":"Flat","slug":"flat-ID1","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00",
               "location":{"reverseGeocoding":{"locations":[
                 {"id":"pomorskie","locationLevel":"voivodeship","name":"pomorskie"},
                 {"id":"pomorskie/wejherowski/gmina-wiejska--wejherowo/goscicino","locationLevel":"city_or_village","name":"Gościcino"},
                 {"id":"pomorskie/wejherowski/gmina-wiejska--wejherowo/goscicino/osiedle","locationLevel":"district","name":"Osiedle"}]}}},
              {"id":2,"title":"No location","slug":"none-ID2","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00"}
            ]}}}}}
            </script>
            """;

        var (offers, _, _) = OtoDomListingParser.Parse(html, BaseUrl);

        Assert.That(offers.Select(o => (o.ExternalId, o.LocationKey)), Is.EquivalentTo(new[]
        {
            ("1", "pomorskie/wejherowski/gmina-wiejska--wejherowo/goscicino"),
            ("2", (string?)null),
        }));
    }

    [Test]
    public void Skips_items_without_required_fields()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"data":{"searchAds":{"items":[
              {"id":1,"title":"Complete","slug":"complete-ID1","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00"},
              {"id":2,"title":"No slug","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00"},
              {"id":3,"title":"Investment","slug":"inv-ID3","estate":"INVESTMENT","transaction":"SELL","createdAtFirst":"2026-11-19T10:00:00+00:00"},
              {"id":4,"title":"Advert","slug":"advert-ID4","estate":"FLAT","transaction":"RENT","createdAtFirst":"1999-02-29 00:00:01"}
            ]}}}}}
            </script>
            """;

        var (offers, skipped, _) = OtoDomListingParser.Parse(html, BaseUrl);

        Assert.Multiple(() =>
        {
            Assert.That(offers.Select(o => o.ExternalId), Is.EqualTo(new[] { "1" }));
            Assert.That(skipped, Is.EqualTo(3));
        });
    }

    [Test]
    public void Returns_each_offer_once_oldest_first()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"data":{"searchAds":{"items":[
              {"id":3,"title":"Newest","slug":"newest-ID3","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00"},
              {"id":2,"title":"Promoted","slug":"promoted-ID2","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00"},
              {"id":1,"title":"Oldest","slug":"oldest-ID1","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00"},
              {"id":2,"title":"Promoted","slug":"promoted-ID2","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00"}
            ]}}}}}
            </script>
            """;

        var (offers, _, _) = OtoDomListingParser.Parse(html, BaseUrl);

        Assert.That(offers.Select(o => o.ExternalId), Is.EqualTo(new[] { "1", "2", "3" }));
    }

    [Test]
    public void Keeps_an_offer_with_an_unknown_currency_without_its_price()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"data":{"searchAds":{"items":[
              {"id":1,"title":"Flat","slug":"flat-ID1","estate":"FLAT","transaction":"RENT","createdAtFirst":"2026-11-19T10:00:00+00:00",
               "totalPrice":{"value":500,"currency":"EUR"},"areaInSquareMeters":40}
            ]}}}}}
            </script>
            """;

        var (offers, skipped, _) = OtoDomListingParser.Parse(html, BaseUrl);

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Is.Zero);
            Assert.That(offers.Single().Price, Is.Null);
            Assert.That(offers.Single().AreaM2, Is.EqualTo(40m));
        });
    }

    [Test]
    public void Reads_the_portal_dates_in_utc()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"data":{"searchAds":{"items":[
              {"id":1,"title":"Flat","slug":"flat-ID1","estate":"FLAT","transaction":"RENT",
               "createdAtFirst":"2026-11-19T10:00:00Z","pushedUpAt":"2026-11-19T12:30:00+01:00"}
            ]}}}}}
            </script>
            """;

        var (offers, _, _) = OtoDomListingParser.Parse(html, BaseUrl);
        var offer = offers.Single();

        Assert.Multiple(() =>
        {
            Assert.That(offer.ExternalCreatedAt, Is.EqualTo(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero)));
            Assert.That(offer.ExternalUpdatedAt, Is.EqualTo(new DateTimeOffset(2026, 11, 19, 11, 30, 0, TimeSpan.Zero)));
            Assert.That(offer.ExternalUpdatedAt!.Value.Offset, Is.EqualTo(TimeSpan.Zero));
        });
    }

    [TestCase(""","pagination":{"currentPage":1,"totalPages":3}""", false)]
    [TestCase(""","pagination":{"currentPage":3,"totalPages":3}""", true)]
    [TestCase("", true)]
    public void Knows_the_last_results_page(string pagination, bool expected)
    {
        var html = """<script id="__NEXT_DATA__">{"props":{"pageProps":{"data":{"searchAds":{"items":[]"""
            + pagination + "}}}}}</script>";

        var (_, _, isLastPage) = OtoDomListingParser.Parse(html, BaseUrl);

        Assert.That(isLastPage, Is.EqualTo(expected));
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
