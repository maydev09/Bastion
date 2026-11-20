using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;
using Bastion.Modules.Housing.Infrastructure.OtoDom;

namespace Bastion.Modules.Housing.Tests;

// Hints come from real OtoDom location keys, so the tests also cover how OtoDom spells places.
public class PlaceMatcherTests
{
    private static readonly Place PruszkowTown = new("0921510", "Pruszków", Voivodeship.Mazowieckie, "pruszkowski", null, PlaceKind.Town, null, 65_283);
    private static readonly Place PruszkowLaski = new("0712350", "Pruszków", Voivodeship.Lodzkie, "łaski", "Sędziejowice", PlaceKind.Village, null, 427);
    private static readonly Place PruszkowSkierniewicki = new("0737692", "Pruszków", Voivodeship.Lodzkie, "skierniewicki", "Skierniewice", PlaceKind.Village, null, 20);
    private static readonly Place KoninCity = new("0948667", "Konin", Voivodeship.Wielkopolskie, null, null, PlaceKind.Town, null, 69_858);
    private static readonly Place KoninVillage = new("0592673", "Konin", Voivodeship.Wielkopolskie, "szamotulski", "Pniewy", PlaceKind.Village, null, 300);
    private static readonly Place Goscicino = new("0176503", "Gościcino", Voivodeship.Pomorskie, "wejherowski", "Wejherowo", PlaceKind.Village, null, 6_134);
    private static readonly Place BielskoBiala = new("0937191", "Bielsko-Biała", Voivodeship.Slaskie, null, null, PlaceKind.Town, null, 168_319);
    private static readonly Place BakowoVillage = new("0155001", "Bąkowo", Voivodeship.Pomorskie, "gdański", "Kolbudy", PlaceKind.Village, null, 1_200);
    private static readonly Place BakowoSettlement = new("0155002", "Bąkowo", Voivodeship.Pomorskie, "gdański", "Kolbudy", PlaceKind.Village, null, 40);

    private static readonly Place[] Places =
        [PruszkowTown, PruszkowLaski, PruszkowSkierniewicki, KoninCity, KoninVillage, Goscicino, BielskoBiala, BakowoVillage, BakowoSettlement];

    [TestCaseSource(nameof(Matches))]
    public void Finds_the_place_of_an_offer(string locationKey, Place expected) =>
        Assert.That(PlaceMatcher.Match(OtoDomLocation.HintOf(locationKey)!, Places), Is.EqualTo(expected));

    private static IEnumerable<TestCaseData> Matches()
    {
        yield return new TestCaseData("mazowieckie/pruszkowski/pruszkow/pruszkow", PruszkowTown).SetName("Same name, other voivodeship");
        yield return new TestCaseData("lodzkie/laski/sedziejowice/pruszkow", PruszkowLaski).SetName("Same name, same voivodeship, other county");
        yield return new TestCaseData("wielkopolskie/konin/konin/konin", KoninCity).SetName("City with county rights");
        yield return new TestCaseData("pomorskie/wejherowski/gmina-wiejska--wejherowo/goscicino", Goscicino).SetName("Rural municipality named like its town");
        yield return new TestCaseData("slaskie/bielsko--biala/bielsko--biala/bielsko--biala", BielskoBiala).SetName("Hyphen in the name");
        yield return new TestCaseData("pomorskie/gdanski/kolbudy/bakowo", BakowoVillage).SetName("Same name in one municipality, the larger place");
    }

    [Test]
    public void Finds_nothing_for_an_unknown_place()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlaceMatcher.Match(OtoDomLocation.HintOf("podlaskie/moniecki/krypno/krypno-koscielne")!, Places), Is.Null);
            Assert.That(PlaceMatcher.Match(new LocationHint("unknown", null, null, "Pruszków"), Places), Is.Null);
        });
    }

    [Test]
    public void Finds_nothing_when_two_places_cannot_be_told_apart()
    {
        Place[] twins = [BakowoVillage, BakowoSettlement with { Population = BakowoVillage.Population }];

        Assert.That(PlaceMatcher.Match(OtoDomLocation.HintOf("pomorskie/gdanski/kolbudy/bakowo")!, twins), Is.Null);
    }
}
