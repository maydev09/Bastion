using Bastion.Modules.Housing.Domain;
using Bastion.Modules.Housing.Infrastructure.OtoDom;

namespace Bastion.Modules.Housing.Tests;

public class OtoDomDetailParserTests
{
    [Test]
    public void Reads_the_fields_only_the_detail_page_shows()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":68656528,"description":"<p>Flat with a view.</p>",
              "characteristics":[
                {"key":"price","value":"2200","currency":"PLN"},
                {"key":"rent","value":"830","currency":"PLN"},
                {"key":"deposit","value":"4000","currency":"PLN"},
                {"key":"building_type","value":"block"},
                {"key":"floor_no","value":"floor_5"},
                {"key":"building_floors_num","value":"5"},
                {"key":"build_year","value":"2008"},
                {"key":"heating","value":"urban"},
                {"key":"construction_status","value":"ready_to_use"},
                {"key":"free_from","value":"2026-11-23"}],
              "additionalInformation":[
                {"label":"extras_types","values":["extras_types::lift","extras_types::balcony","extras_types::garage"]},
                {"label":"equipment_types","values":["equipment_types::furniture","extras_types::air_conditioning","equipment_types::stove"]},
                {"label":"security_types","values":["security_types-102::monitoring"]}],
              "location":{"coordinates":{"latitude":52.389378,"longitude":16.912691},"mapDetails":{"radius":0}}}}}}
            </script>
            """;

        var (externalId, details) = OtoDomDetailParser.Parse(html);

        Assert.Multiple(() =>
        {
            Assert.That(externalId, Is.EqualTo("68656528"));
            Assert.That((details.Rent, details.Deposit), Is.EqualTo((new Money(830m, Currency.Pln), new Money(4000m, Currency.Pln))));
            Assert.That((details.Floor, details.BuildingFloors, details.BuildYear), Is.EqualTo((5, 5, 2008)));
            Assert.That((details.BuildingType, details.Heating, details.Condition),
                Is.EqualTo((BuildingType.Block, Heating.District, Condition.ReadyToUse)));
            Assert.That(details.AvailableFrom, Is.EqualTo(new DateOnly(2026, 11, 23)));
            Assert.That((details.Latitude, details.Longitude, details.LocationRadiusM), Is.EqualTo((52.389378m, 16.912691m, 0)));
            Assert.That(details.Description, Is.EqualTo("<p>Flat with a view.</p>"));
            Assert.That(details.RejectedValues, Is.Null);
            Assert.That(details.Features, Is.EquivalentTo(new[]
            {
                Feature.Lift, Feature.Balcony, Feature.Parking, Feature.Furnished, Feature.AirConditioning,
            }));
        });
    }

    [Test]
    public void Leaves_unknown_and_missing_values_empty()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":1,"characteristics":[
              {"key":"floor_no","value":"ground_floor"},
              {"key":"building_type","value":"castle"},
              {"key":"heating","value":"other"}]}}}}
            </script>
            """;

        var (_, details) = OtoDomDetailParser.Parse(html);

        Assert.Multiple(() =>
        {
            Assert.That(details.Floor, Is.Zero);
            Assert.That((details.BuildingType, details.Heating, details.Rent, details.Latitude, details.Description),
                Is.EqualTo(((BuildingType?)null, (Heating?)null, (Money?)null, (decimal?)null, (string?)null)));
            Assert.That(details.Features, Is.Null);
        });
    }

    [Test]
    public void Keeps_implausible_numbers_only_as_rejected_values()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":1,"characteristics":[
              {"key":"build_year","value":"195014"},
              {"key":"building_floors_num","value":"250"},
              {"key":"floor_no","value":"floor_3"}]}}}}
            </script>
            """;

        var (_, details) = OtoDomDetailParser.Parse(html);

        Assert.Multiple(() =>
        {
            Assert.That((details.BuildYear, details.BuildingFloors, details.Floor), Is.EqualTo(((int?)null, (int?)null, (int?)3)));
            Assert.That(details.RejectedValues, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["build_year"] = "195014",
                ["building_floors"] = "250",
            }));
        });
    }

    [Test]
    public void Keeps_features_unknown_without_feature_lists_and_empty_with_empty_ones()
    {
        const string without = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":1,"additionalInformation":[{"label":"windows_type","values":["windows_type::plastic"]}]}}}}
            </script>
            """;
        const string empty = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":1,"additionalInformation":[{"label":"extras_types","values":[]}]}}}}
            </script>
            """;

        Assert.Multiple(() =>
        {
            Assert.That(OtoDomDetailParser.Parse(without).Details.Features, Is.Null);
            Assert.That(OtoDomDetailParser.Parse(empty).Details.Features, Is.Empty);
        });
    }

    [Test]
    public void Refuses_the_page_of_an_ended_offer()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{"ad":{"id":1,"status":"inactive"}}}}</script>
            """;

        Assert.Throws<OtoDomParseException>(() => OtoDomDetailParser.Parse(html));
    }

    [Test]
    public void Fails_on_a_page_without_an_offer()
    {
        const string html = """<script id="__NEXT_DATA__" type="application/json">{"props":{"pageProps":{}}}</script>""";

        Assert.Throws<OtoDomParseException>(() => OtoDomDetailParser.Parse(html));
    }
}
