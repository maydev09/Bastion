using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Tests;

public class PlaceClassificationTests
{
    private static readonly PlaceSizeOptions Sizes = new();

    [TestCase(PlaceKind.Village, 186, PlaceSize.SmallVillage)]
    [TestCase(PlaceKind.Village, 1_506, PlaceSize.LargeVillage)]
    [TestCase(PlaceKind.Village, 8_808, PlaceSize.VeryLargeVillage)]
    [TestCase(PlaceKind.Town, 314, PlaceSize.VerySmallTown)]
    [TestCase(PlaceKind.Town, 18_768, PlaceSize.SmallTown)]
    [TestCase(PlaceKind.Town, 71_735, PlaceSize.LargerTown)]
    [TestCase(PlaceKind.Town, 1_862_402, PlaceSize.Metropolis)]
    public void Villages_and_towns_have_their_own_size_scales(PlaceKind kind, int population, PlaceSize expected) =>
        Assert.That(Sizes.Of(kind, population), Is.EqualTo(expected));

    [Test]
    public void A_place_of_unknown_kind_has_no_size() =>
        Assert.That(Sizes.Of(null, 4_429), Is.Null);

    [Test]
    public void A_village_gets_every_tier_of_nearby_places()
    {
        var nearby = Nearby.Where(n => n.PlaceId == Bolszewo.Id).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(nearby.Select(n => (n.Tier, n.NearbyId)), Is.EqualTo(new[]
            {
                (NearbyTier.Town, Wejherowo.Id),
                (NearbyTier.CountySeat, Wejherowo.Id),
                (NearbyTier.VoivodeshipCapital, Gdansk.Id),
                (NearbyTier.Metropolis, Gdansk.Id),
                (NearbyTier.BigCity, Gdynia.Id),
                (NearbyTier.BigCity, Gdansk.Id),
            }));
            Assert.That(nearby.Single(n => n.Tier == NearbyTier.BigCity && n.Rank == 1).Km, Is.InRange(24m, 27m));
        });
    }

    [Test]
    public void A_tier_the_place_meets_itself_is_left_out()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Nearby.Where(n => n.PlaceId == Warszawa.Id), Is.Empty);
            Assert.That(Nearby.Where(n => n.PlaceId == Gdansk.Id).Select(n => (n.Tier, n.NearbyId)),
                Is.EqualTo(new[] { (NearbyTier.BigCity, Gdynia.Id) }));
            Assert.That(Nearby.Where(n => n.PlaceId == Gdynia.Id).Select(n => n.Tier), Does.Contain(NearbyTier.Metropolis));
            Assert.That(Nearby.Where(n => n.PlaceId == NoPoint.Id), Is.Empty);
        });
    }

    private static readonly Place Gdansk = new("0933016", "Gdańsk", Voivodeship.Pomorskie, Kind: PlaceKind.Town, Role: PlaceRole.VoivodeshipCapital, Population: 486_022, Latitude: 54.3475m, Longitude: 18.6453m) { Id = 1 };
    private static readonly Place Gdynia = new("0934625", "Gdynia", Voivodeship.Pomorskie, Kind: PlaceKind.Town, Role: PlaceRole.CountySeat, Population: 243_918, Latitude: 54.5189m, Longitude: 18.5306m) { Id = 2 };
    private static readonly Place Wejherowo = new("0934410", "Wejherowo", Voivodeship.Pomorskie, Kind: PlaceKind.Town, Role: PlaceRole.CountySeat, Population: 47_000, Latitude: 54.6058m, Longitude: 18.2353m) { Id = 3 };
    private static readonly Place Bolszewo = new("0176460", "Bolszewo", Voivodeship.Pomorskie, Kind: PlaceKind.Village, Population: 8_808, Latitude: 54.6189m, Longitude: 18.1717m) { Id = 4 };
    private static readonly Place Warszawa = new("0918123", "Warszawa", Voivodeship.Mazowieckie, Kind: PlaceKind.Town, Role: PlaceRole.VoivodeshipCapital, Population: 1_862_402, Latitude: 52.2297m, Longitude: 21.0122m) { Id = 5 };
    private static readonly Place NoPoint = new("0000001", "Nowa Wieś", Voivodeship.Pomorskie, Kind: PlaceKind.Village) { Id = 6 };

    private static readonly IReadOnlyList<PlaceNearby> Nearby =
        NearbyPlaces.Of([Gdansk, Gdynia, Wejherowo, Bolszewo, Warszawa, NoPoint], new PlaceOptions(), Sizes.Town.Metropolis);
}
