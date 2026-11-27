using Bastion.Modules.Housing.Application.StatisticsExclusions;

namespace Bastion.Modules.Housing.Tests;

// The titles and texts follow real OtoDom offers (2026-11-27), shortened and without personal data.
public class OfferTextTests
{
    [TestCase("Pokój do wynajęcia, obok Punktu 44")]
    [TestCase("Od zaraz pokój  w mieszkaniu 3 pokojowym!")]
    [TestCase("Komfortowy jednoosobowy pokój do wynajęcia nr 2")]
    [TestCase("Pokoje na wynajem, idealne dla studentów!")]
    [TestCase("Wynajmę pokój w centrum Szczecina – 12m²")]
    [TestCase("Pokój w apartamentowcu blisko UŁ")]
    public void A_room_in_a_shared_flat_is_a_room(string title) => Assert.That(OfferText.IsRoom(title), Is.True);

    [TestCase("3 pokoje na wynajem | Po remoncie | Od zaraz")]
    [TestCase("Pokój z kuchnią po remoncie przy parku")]
    [TestCase("Pokój z osobną kuchną w centrum")]
    [TestCase("Pokój/Kawalerka Andersa")]
    [TestCase("Mieszkanie 2 pokoje, balkon")]
    public void A_flat_is_no_room(string title) => Assert.That(OfferText.IsRoom(title), Is.False);

    [TestCase("Miejsce postojowe w garażu podziemnym, ul. Wschodu Słońca")]
    [TestCase("Podwójne Miejsce parkingowe w hali")]
    [TestCase("Jaśminowy Mokotów | garaż 12.5m | miejsce postojowe")]
    public void A_parking_space_is_parking(string title) => Assert.That(OfferText.IsParking(title), Is.True);

    [TestCase("Mieszkanie 2-pokojowe z balkonem i garażem")]
    [TestCase("Nowe studio Bemowo | balkon | miejsce postojowe")]
    [TestCase("Kamienica | 3 pokoje | Miejsce postojowe")]
    public void A_flat_with_a_parking_space_is_no_parking(string title) => Assert.That(OfferText.IsParking(title), Is.False);

    [TestCase("<p><b>Minimalna kwota wierzytelności do licytacji:   12 700  zł</b></p>")]
    [TestCase("Spółka ogłasza konkurs na najem lokalu mieszkalnego. Wadium: 2 600 zł.")]
    public void An_auction_or_a_tender_is_an_auction(string description) =>
        Assert.That(OfferText.IsAuction(description), Is.True);

    [TestCase("Mieszkanie dla firm Głogów ul. Jagiellońska /35zł od osoby", null)]
    [TestCase("Mieszkanie 3 pokojowe", "Najem pracowniczy dla 9 osób.")]
    [TestCase("Mieszkanie 2 pokoje", "<p>Cena 650&nbsp;zł/os. z opłatami.</p>")]
    public void Workers_quarters_or_a_price_per_person_is_per_person(string title, string? description) =>
        Assert.That(OfferText.IsPerPerson(title, description), Is.True);

    [Test]
    public void Utilities_per_person_alone_are_no_price_per_person() =>
        Assert.That(OfferText.IsPerPerson("Mieszkanie 2 pokoje", "Cena 2500 zł, media według zużycia."), Is.False);

    [TestCase("Mieszkanie przy ul. Wolskiego - MIESZKANIE ZA REMONT", null)]
    [TestCase("BEZCZYNSZOWE", "Mieszkanie wymagające prac remontowych w zamian za obniżony czynsz.")]
    public void A_flat_let_for_renovating_it_is_rent_for_renovation(string title, string? description) =>
        Assert.That(OfferText.IsRentForRenovation(title, description), Is.True);
}
