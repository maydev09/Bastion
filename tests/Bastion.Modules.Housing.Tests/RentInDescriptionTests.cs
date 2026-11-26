using Bastion.Modules.Housing.Application.MonthlyCosts;

namespace Bastion.Modules.Housing.Tests;

// The texts follow real OtoDom descriptions, shortened and without personal data.
public class RentInDescriptionTests
{
    [TestCase("<p>Czynsz administracyjny: 544,08 zł miesięcznie.</p>", 544.08)]
    [TestCase("Cena 2500 zł + czynsz 900 zł + media wg zużycia.", 900)]
    [TestCase("czynsz najmu 2500 zł + czynsz administracyjny: 544,08 zł", 544.08)]
    [TestCase("Opłaty 2200 zł czynsz najmu + 750 czynsz administracyjny + energia", 750)]
    [TestCase("Cena najmu: - 3700 zł czynsz najmu, - 950 zł czynsz do wspólnoty, - prąd według zużycia", 950)]
    [TestCase("Stosunkowo niskie opłaty eksploatacyjne, około 750 zł miesięcznie.", 750)]
    [TestCase("Czynsz dla Spółdzielni Mieszkaniowej - około 600 zł", 600)]
    [TestCase("Dodatkowe opłaty to czynsz adm. 1000 zł i prąd wg licznika.", 1000)]
    [TestCase("- Czynsz najmu 3300 zł - Czynsz administracyjny 700 zł - Zaliczka na prąd 150 zł", 700)]
    [TestCase("Czynsz administracyjny w cenie 2000 PLN. Układ funkcjonalny.", 2000)]
    [TestCase("Opłaty eksploatacyjne wynoszą ok. 1 070 zł.", 1070)]
    [TestCase("Opłata eksploatacyjna wynosi 870 zł.", 870)]
    [TestCase("Czynsz do wspólnoty w wysokości 1.250 zł", 1250)]
    [TestCase("czynsz dla spółdzielni (aktualnie) 480 PLN", 480)]
    [TestCase("Czynsz&nbsp;administracyjny&nbsp;– 600,- płatny do 10. dnia", 600)]
    public void Reads_the_amount_after_a_label_of_the_admin_rent(string description, decimal amount)
    {
        var reading = RentInDescription.Read(null, description);

        Assert.That(reading.Amounts.Select(m => m.Amount), Is.EqualTo(new[] { amount }));
    }

    [TestCase("Kaucja 3000 zł czynsz administracyjny według zużycia")]
    [TestCase("Kaucja: 3 500 zł czynsz administracyjny według zużycia")]
    public void The_deposit_before_the_label_is_no_rent(string description)
    {
        var reading = RentInDescription.Read(null, description);

        Assert.That(reading.Amounts, Is.Empty);
    }

    [Test]
    public void The_rent_of_the_lease_is_the_price_not_the_admin_rent()
    {
        var reading = RentInDescription.Read(null, "Czynsz najmu: 2 800 zł. Kaucja 3000 zł.");

        Assert.That((reading.Amounts, reading.Included), Is.EqualTo((Array.Empty<RentInDescription.Mention>(), false)));
    }

    [TestCase("Czynsz administracyjny wliczony w cenę.")]
    [TestCase("czynsz administracyjny w cenie najmu, media osobno")]
    [TestCase("Opłaty administracyjne po stronie właściciela.")]
    [TestCase("2800 PLN/mc (czynsz administracyjny wliczony w cenę najmu)")]
    [TestCase("Czynsz administracyjny zawarty w cenie.")]
    [TestCase("Cena najmu: 3000 zł (w tym wliczony jest już czynsz administracyjny) + media.")]
    [TestCase("Cena obejmuje czynsz najmu oraz czynsz administracyjny do spółdzielni.")]
    [TestCase("W cenie najmu zawarte są: czynsz administracyjny oraz miejsce postojowe.")]
    [TestCase("Czynsz administracyjny: jest już wliczony w czynsz najmu.")]
    [TestCase("czynsz najmu: 1 750 zł, czynsz administracyjny: 0 zł, media wg zużycia")]
    [TestCase("Nowe, bezczynszowe mieszkanie z ogródkiem.")]
    [TestCase("Brak dodatkowego czynszu administracyjnego.")]
    [TestCase("Nie ma czynszu do spółdzielni ani wspólnoty.")]
    public void Reads_that_the_admin_rent_is_included_or_there_is_none(string description)
    {
        var reading = RentInDescription.Read(null, description);

        Assert.That((reading.Amounts.Count, reading.Included), Is.EqualTo((0, true)));
        Assert.That(reading.IncludedQuote, Is.Not.Empty);
    }

    [TestCase("Czynsz administracyjny 550 zł (2 os.), w tym śmieci.")]
    [TestCase("czynsz administracyjny: 300 zł/os.")]
    [TestCase("Czynsz administracyjny 900 zł, w tym zaliczka na ogrzewanie i wodę.")]
    [TestCase("czynsz administracyjny 1300 zł - ryczałt z mediami")]
    [TestCase("Czynsz administracyjny: ok. 500 zł/miesiąc (zawiera m.in. zaliczki na wodę).")]
    [TestCase("Czynsz administracyjny: ok. 849 zł (w przypadku 1 osoby) lub 920 zł (2 osoby).")]
    [TestCase("Opłaty eksploatacyjne (media + Wi-Fi): 500 zł")]
    [TestCase("Czynsz do spółdzielni wynosi 870zł (woda, c.o., śmieci, winda).")]
    [TestCase("dodatkowo opłaty eksploatacyjne w kwocie 9,42 zł/mkw. oraz media")]
    [TestCase("Opłaty administracyjne ok. 380zł przy jednej osobie + prąd.")]
    public void Leaves_out_an_amount_per_person_or_with_utilities(string description)
    {
        var reading = RentInDescription.Read(null, description);

        Assert.That(reading.Amounts, Is.Empty);
    }

    [Test]
    [TestCase("Cena 3000 zł. W czynszu administracyjnym zawarte są: zaliczka na wodę i śmieci.")]
    [TestCase("W czynsz administracyjny wliczone są zaliczki na wodę zimną i ciepłą.")]
    [TestCase("Czynsz administracyjny (wliczone zaliczki na wodę i ogrzewanie) płatny osobno.")]
    [TestCase("Miejsce postojowe wliczone w cenę najmu. Czynsz administracyjny płatny osobno.")]
    [TestCase("Akceptujemy zwierzęta bez dodatkowych opłat.")]
    [TestCase("Cena nie obejmuje czynszu administracyjnego płatnego przez najemcę.")]
    [TestCase("Koszt: 1500 zł czynszu najmu plus opłaty administracyjne według rozliczenia.")]
    public void What_the_admin_rent_contains_does_not_mean_it_is_included(string description)
    {
        var reading = RentInDescription.Read(null, description);

        Assert.That(reading.Included, Is.False);
    }

    [Test]
    public void Reads_every_amount_and_quotes_the_words_around_it()
    {
        var reading = RentInDescription.Read("Mieszkanie 2 pokoje", "Czynsz administracyjny 400 zł. Później: czynsz do wspólnoty 450 zł.");

        Assert.That(reading.Amounts.Select(m => m.Amount), Is.EqualTo(new[] { 400m, 450m }));
        Assert.That(reading.Amounts[0].Quote, Does.StartWith("Czynsz administracyjny 400 zł"));
    }

    [Test]
    public void Reads_the_title_too()
    {
        var reading = RentInDescription.Read("Kawalerka 2000 zł + czynsz 350 zł", "Bez zwierząt.");

        Assert.That(reading.Amounts.Single().Amount, Is.EqualTo(350m));
    }
}
