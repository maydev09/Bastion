using Bastion.Modules.Housing.Domain;
using Bastion.Modules.Housing.Infrastructure.Wikidata;

namespace Bastion.Modules.Housing.Tests;

public class WikidataPlacesTests
{
    private static readonly Dictionary<string, string> Units = new()
    {
        ["Q1"] = "Wejherowo",
        ["Q2"] = "powiat wejherowski",
        ["Q3"] = "województwo pomorskie",
        ["Q4"] = "Polska",
        ["Q5"] = "powiat pruszkowski",
        ["Q6"] = "województwo mazowieckie",
        ["Q7"] = "województwo olsztyńskie",
        ["Q8"] = "Gmina Raków",
        ["Q10"] = "Gmina Raków (województwo kieleckie)",
    };

    [Test]
    public void Finds_municipality_county_and_voivodeship_at_any_level()
    {
        var places = WikidataPlaces.Build(Rows(
            simcs: [("Q100", "0176503"), ("Q200", "0921510")],
            types: [("Q100", "Q3558970"), ("Q200", "Q515")],
            parents: [("Q100", "Q1"), ("Q1", "Q2"), ("Q2", "Q3"), ("Q3", "Q4"), ("Q200", "Q5"), ("Q5", "Q6")]));

        Assert.That(places, Is.EquivalentTo(new[]
        {
            new Place("0176503", "Gościcino", Voivodeship.Pomorskie, "wejherowski", "Wejherowo", PlaceKind.Village),
            new Place("0921510", "Pruszków", Voivodeship.Mazowieckie, "pruszkowski", null, PlaceKind.Town, PlaceRole.MunicipalitySeat),
        }));
    }

    [Test]
    public void Keeps_only_the_name_of_a_current_municipality()
    {
        var places = WikidataPlaces.Build(Rows(
            simcs: [("Q100", "0176503")],
            parents: [("Q100", "Q10"), ("Q100", "Q8"), ("Q8", "Q2"), ("Q2", "Q3")]));

        Assert.That(places.Single().Municipality, Is.EqualTo("Raków"));
    }

    [Test]
    public void Gives_the_highest_current_seat()
    {
        var places = WikidataPlaces.Build(Rows(
            simcs: [("Q100", "0176503"), ("Q200", "0921510"), ("Q300", "0176444"), ("Q400", "0933016")],
            types: [("Q100", "Q3558970"), ("Q200", "Q515"), ("Q300", "Q3558970"), ("Q400", "Q515")],
            parents: [("Q100", "Q1"), ("Q1", "Q2"), ("Q2", "Q3"), ("Q200", "Q5"), ("Q5", "Q6"), ("Q300", "Q1"), ("Q400", "Q3")],
            seats: [("Q100", "Gmina Biała (województwo opolskie)"), ("Q200", "powiat pruszkowski"), ("Q400", "województwo pomorskie")]));

        Assert.That(places.Select(p => (p.Name, p.Role)), Is.EquivalentTo(new (string, PlaceRole?)[]
        {
            ("Gościcino", null),
            ("Pruszków", PlaceRole.CountySeat),
            ("Wejherowo", PlaceRole.MunicipalitySeat),
            ("Gdańsk", PlaceRole.VoivodeshipCapital),
        }));
    }

    [Test]
    public void Takes_the_newest_population_with_its_year()
    {
        var places = WikidataPlaces.Build(Rows(
            simcs: [("Q200", "0921510")],
            parents: [("Q200", "Q5"), ("Q5", "Q6")],
            populations: [("Q200", 55_387m, At(2006)), ("Q200", 65_283m, At(2021)), ("Q200", 61_237m, null)]));

        Assert.That((places[0].Population, places[0].PopulationYear, places[0].PopulationSource),
            Is.EqualTo(((int?)65_283, (int?)2021, (PlaceSource?)PlaceSource.Wikidata)));
    }

    [Test]
    public void Leaves_out_a_place_without_a_current_voivodeship()
    {
        var places = WikidataPlaces.Build(Rows(simcs: [("Q100", "0176503")], parents: [("Q100", "Q7")]));

        Assert.That(places, Is.Empty);
    }

    private static WikidataRows Rows(
        IReadOnlyList<(string, string)> simcs,
        IReadOnlyList<(string, string)>? types = null,
        IReadOnlyList<(string, string)>? parents = null,
        IReadOnlyList<(string, decimal, DateTimeOffset?)>? populations = null,
        IReadOnlyList<(string, string)>? seats = null) =>
        new(simcs,
            new Dictionary<string, string> { ["Q100"] = "Gościcino", ["Q200"] = "Pruszków", ["Q300"] = "Wejherowo", ["Q400"] = "Gdańsk" },
            types ?? [],
            parents ?? [],
            Units,
            populations ?? [],
            [],
            seats ?? []);

    private static DateTimeOffset At(int year) => new(year, 3, 31, 0, 0, 0, TimeSpan.Zero);
}
