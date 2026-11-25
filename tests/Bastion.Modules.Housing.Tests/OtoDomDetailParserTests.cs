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
            {"props":{"pageProps":{"ad":{"id":68656528,"description":"<p>Apartment with a view.</p>",
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
            Assert.That((details.RentAmount, details.RentCurrency, details.DepositAmount, details.DepositCurrency),
                Is.EqualTo(((decimal?)830m, (Currency?)Currency.Pln, (decimal?)4000m, (Currency?)Currency.Pln)));
            Assert.That((details.Floor, details.BuildingFloors, details.BuildYear), Is.EqualTo((5, 5, 2008)));
            Assert.That((details.BuildingType, details.Heating, details.Condition),
                Is.EqualTo((BuildingType.Block, Heating.District, Condition.ReadyToUse)));
            Assert.That(details.AvailableFrom, Is.EqualTo(new DateOnly(2026, 11, 23)));
            Assert.That((details.Latitude, details.Longitude, details.LocationRadiusM), Is.EqualTo((52.389378m, 16.912691m, 0)));
            Assert.That(details.Description, Is.EqualTo("<p>Apartment with a view.</p>"));
            Assert.That(details.RejectedValues, Is.Null);
            Assert.That(details.Features, Is.EquivalentTo(new[]
            {
                Feature.Lift, Feature.Balcony, Feature.Parking, Feature.AirConditioning, Feature.Stove, Feature.Monitoring,
            }));
            Assert.That(details.Furnished, Is.EqualTo(Answer.Yes));
        });
    }

    [Test]
    public void Leaves_unknown_and_missing_values_empty()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":1,"characteristics":[
              {"key":"floor_no","value":"ground_floor"},
              {"key":"build_year","value":""},
              {"key":"building_type","value":"castle"},
              {"key":"heating","value":"coal"}]}}}}
            </script>
            """;

        var (_, details) = OtoDomDetailParser.Parse(html);

        Assert.Multiple(() =>
        {
            Assert.That(details.Floor, Is.Zero);
            Assert.That((details.BuildingType, details.Heating, details.RentAmount, details.Latitude, details.Description),
                Is.EqualTo(((BuildingType?)null, (Heating?)null, (decimal?)null, (decimal?)null, (string?)null)));
            Assert.That(details.Features, Is.Null);
            Assert.That(details.Furnished, Is.Null);
            Assert.That((details.BuildYear, details.RejectedValues), Is.EqualTo(((int?)null, (IReadOnlyDictionary<string, string>?)null)));
        });
    }

    // Every value seen on production detail pages (2026-11-24).
    [TestCase("block", "urban", BuildingType.Block, Heating.District)]
    [TestCase("apartment", "gas", BuildingType.Apartment, Heating.Gas)]
    [TestCase("tenement", "electrical", BuildingType.Tenement, Heating.Electric)]
    [TestCase("house", "boiler_room", BuildingType.House, Heating.BoilerRoom)]
    [TestCase("loft", "tiled_stove", BuildingType.Loft, Heating.TiledStove)]
    [TestCase("ribbon", "other", BuildingType.Terraced, Heating.Other)]
    [TestCase("infill", "other", BuildingType.Infill, Heating.Other)]
    [TestCase("other", "other", BuildingType.Other, Heating.Other)]
    public void Reads_building_type_and_heating(string buildingType, string heating, BuildingType expectedType, Heating expectedHeating)
    {
        var html = $$"""
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":1,"characteristics":[
              {"key":"building_type","value":"{{buildingType}}"},
              {"key":"heating","value":"{{heating}}"}]} } } }
            </script>
            """;

        var (_, details) = OtoDomDetailParser.Parse(html);

        Assert.That((details.BuildingType, details.Heating), Is.EqualTo(((BuildingType?)expectedType, (Heating?)expectedHeating)));
    }

    [Test]
    public void Keeps_implausible_numbers_only_as_rejected_values()
    {
        const string html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":1,"characteristics":[
              {"key":"build_year","value":"195014"},
              {"key":"building_floors_num","value":"250"},
              {"key":"floor_no","value":"floor_3"}],
              "location":{"mapDetails":{"radius":1500.5}}}}}}
            </script>
            """;

        var (_, details) = OtoDomDetailParser.Parse(html);

        Assert.Multiple(() =>
        {
            Assert.That((details.BuildYear, details.BuildingFloors, details.LocationRadiusM, details.Floor),
                Is.EqualTo(((int?)null, (int?)null, (int?)null, (int?)3)));
            Assert.That(details.RejectedValues, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["build_year"] = "195014",
                ["building_floors"] = "250",
                ["location_radius_m"] = "1500.5",
            }));
        });
    }

    // OtoDom has no furnished field: only "furniture" among the ticked equipment.
    [TestCase("\"equipment_types::fridge\",\"equipment_types::furniture\"", Answer.Yes)]
    [TestCase("\"equipment_types::fridge\",\"equipment_types::stove\"", Answer.No)]
    [TestCase("", Answer.NotStated)]
    public void Reads_furnished_from_the_ticked_equipment(string equipment, Answer expected)
    {
        var html = """
            <script id="__NEXT_DATA__" type="application/json">
            {"props":{"pageProps":{"ad":{"id":1,"additionalInformation":[
              {"label":"extras_types","values":["extras_types::balcony"]},
              {"label":"equipment_types","values":[EQUIPMENT]}]}}}}
            </script>
            """.Replace("EQUIPMENT", equipment, StringComparison.Ordinal);

        Assert.That(OtoDomDetailParser.Parse(html).Details.Furnished, Is.EqualTo(expected));
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
