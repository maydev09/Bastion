using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bastion.BuildingBlocks.Scraping;
using Bastion.BuildingBlocks.Jobs;
using Bastion.Host;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.FetchDetails;
using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Application.ListPlaces;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Application.ReparseOffers;
using Bastion.Modules.Housing.Domain;
using Bastion.Modules.Housing.Infrastructure.OtoDom;
using Bastion.Platform.Abstractions;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ZstdSharp;

namespace Bastion.IntegrationTests.Database;

public class HousingImportTests
{
    private const string Import = "/api/housing/jobs/ImportOffers";
    private const string Scan = "/api/housing/jobs/ScanOffers";
    private const string FetchDetails = "/api/housing/jobs/FetchDetails";
    private const string ImportPlaces = "/api/housing/jobs/ImportPlaces";
    private const string LinkPlaces = "/api/housing/jobs/LinkPlaces";

    private const string WarszawaKey = "mazowieckie/warszawa/warszawa/warszawa";
    private const string GoscicinoKey = "pomorskie/wejherowski/gmina-wiejska--wejherowo/goscicino";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly Offer Small = new(Source.OtoDom, "1", "Small flat", new Uri("https://www.otodom.pl/pl/oferta/small-ID1"), PropertyType.Flat, Transaction.Rent, At(12), new Money(2200m, Currency.Pln), 30m, 1);
    private static readonly Offer Medium = new(Source.OtoDom, "3", "Medium flat", new Uri("https://www.otodom.pl/pl/oferta/medium-ID3"), PropertyType.Flat, Transaction.Rent, At(11), new Money(3000m, Currency.Pln), 50m, 2);
    private static readonly Offer Large = new(Source.OtoDom, "2", "Large flat", new Uri("https://www.otodom.pl/pl/oferta/large-ID2"), PropertyType.Flat, Transaction.Rent, At(10), new Money(4100m, Currency.Pln), 72.5m, 3);

    private static readonly Place Warszawa = new("0918123", "Warszawa", Voivodeship.Mazowieckie, null, null, PlaceKind.Town, PlaceRole.VoivodeshipCapital, 1_863_056, 2021, PlaceSource.Wikidata, 52.2297m, 21.0122m);
    private static readonly Place Goscicino = new("0176503", "Gościcino", Voivodeship.Pomorskie, "wejherowski", "Wejherowo", PlaceKind.Village, null, 6_134, 2021, PlaceSource.Wikidata, 54.6011m, 18.2369m);

    private FakeOfferSource _source = new();
    private IPlaceSource _places = Substitute.For<IPlaceSource>();
    private string _rawPath = "";

    [SetUp]
    public async Task ClearData()
    {
        _source = new FakeOfferSource();
        _places = Substitute.For<IPlaceSource>();
        _places.FetchAsync(Arg.Any<CancellationToken>()).Returns(new PlaceFetchResult([Warszawa, Goscicino]));
        _rawPath = Directory.CreateTempSubdirectory("bastion-raw-").FullName;
        await Sql("DELETE FROM housing.job_run; DELETE FROM housing.offer; DELETE FROM housing.raw_capture; DELETE FROM housing.place;");
    }

    [TearDown]
    public void DeleteRawFiles() => Directory.Delete(_rawPath, recursive: true);

    [Test]
    public async Task Importing_twice_stores_each_offer_once()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];

        var first = await RunImport(client);
        var second = await RunImport(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((first.Added, first.Updated, first.Unchanged), Is.EqualTo((2, 0, 0)));
            Assert.That((second.Added, second.Updated, second.Unchanged), Is.EqualTo((0, 0, 2)));
            Assert.That(list!.TotalCount, Is.EqualTo(2));
            Assert.That(list.Items.Select(o => o.Title), Is.EquivalentTo(new[] { "Small flat", "Large flat" }));
        });
    }

    [Test]
    public async Task Stored_offers_are_linked_to_imported_places_by_their_own_job()
    {
        await using var factory = App(WithFakes);
        using var client = factory.CreateClient();
        _source.Pages = [[Small with { LocationKey = WarszawaKey }, Medium with { LocationKey = "podlaskie/moniecki/krypno/krypno-koscielne" }, Large with { LocationKey = GoscicinoKey }]];
        await RunImport(client);
        var before = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        var first = await JobRuns.Run(client, ImportPlaces);
        var second = await JobRuns.Run(client, ImportPlaces);
        var afterImport = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);
        var link = await JobRuns.Run(client, LinkPlaces);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);
        var small = list!.Items.Single(o => o.Title == "Small flat");

        Assert.Multiple(() =>
        {
            Assert.That(before!.Items.Select(o => o.PlaceName), Is.All.Null);
            Assert.That(first.Summary!.Counts, Is.EquivalentTo(new Dictionary<string, long>
            {
                ["places"] = 2, ["added"] = 2, ["updated"] = 0,
            }));
            Assert.That(afterImport!.Items.Select(o => o.PlaceName), Is.All.Null, "importing places links no stored offer");
            Assert.That(link.Summary!.Counts, Is.EquivalentTo(new Dictionary<string, long>
            {
                ["linkedOffers"] = 2, ["unlinkedOffers"] = 1,
            }));
            Assert.That((second.Summary!.Counts["added"], second.Summary.Counts["updated"]), Is.EqualTo((0, 0)));
            Assert.That(list.Items.Select(o => (o.Title, o.PlaceName)), Is.EquivalentTo(new[]
            {
                ("Small flat", "Warszawa"), ("Medium flat", null), ("Large flat", "Gościcino"),
            }));
            Assert.That((small.PlaceKind, small.PlaceSize, small.PlaceRole, small.Voivodeship, small.Population, small.PopulationYear),
                Is.EqualTo(((PlaceKind?)PlaceKind.Town, (PlaceSize?)PlaceSize.Metropolis, (PlaceRole?)PlaceRole.VoivodeshipCapital,
                    (Voivodeship?)Voivodeship.Mazowieckie, (int?)1_863_056, (int?)2021)));
        });
    }

    [Test]
    public async Task Offers_imported_after_the_places_get_their_place_at_once()
    {
        await using var factory = App(WithFakes);
        using var client = factory.CreateClient();
        await JobRuns.Run(client, ImportPlaces);
        _source.Pages = [[Large with { LocationKey = GoscicinoKey }]];

        await RunImport(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        var offer = list!.Items.Single();
        var detail = await client.GetFromJsonAsync<OfferListItem>($"/api/housing/offers/{offer.Id}", Json);
        var missing = await client.GetAsync(new Uri($"/api/housing/offers/{offer.Id + 1000}", UriKind.Relative));

        Assert.Multiple(() =>
        {
            Assert.That((offer.PlaceName, offer.County), Is.EqualTo(("Gościcino", "wejherowski")));
            Assert.That((detail!.PlaceName, detail.PlaceSize), Is.EqualTo(("Gościcino", (PlaceSize?)PlaceSize.VeryLargeVillage)));
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(offer.Nearby.Select(n => (n.Tier, n.Name)), Is.EqualTo(new[]
            {
                (NearbyTier.Town, "Warszawa"), (NearbyTier.CountySeat, "Warszawa"),
                (NearbyTier.VoivodeshipCapital, "Warszawa"), (NearbyTier.Metropolis, "Warszawa"),
            }));
            Assert.That(offer.Nearby[0].Km, Is.InRange(300m, 340m));
        });
    }

    [Test]
    public async Task Stored_offer_gets_its_location_without_counting_as_changed()
    {
        await using var factory = App(WithFakes);
        using var client = factory.CreateClient();
        await JobRuns.Run(client, ImportPlaces);
        _source.Pages = [[Large]];
        await RunImport(client);

        _source.Pages = [[Large with { LocationKey = GoscicinoKey }]];
        var result = await RunImport(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((result.Added, result.Updated, result.Unchanged), Is.EqualTo((0, 0, 1)));
            Assert.That(list!.Items.Single().PlaceName, Is.EqualTo("Gościcino"));
        });
    }

    [Test]
    public async Task Failed_place_import_shows_why()
    {
        _places.FetchAsync(Arg.Any<CancellationToken>()).Returns(PlaceFetchResult.Failed(
            new Failure("housing.placesUnavailable", "Wikidata timed out", FailureKind.Unavailable)));
        await using var factory = App(WithFakes);
        using var client = factory.CreateClient();

        var run = await JobRuns.Run(client, ImportPlaces);

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(run.ErrorCode, Is.EqualTo("housing.placesUnavailable"));
        });
    }

    [Test]
    public async Task Changed_offer_is_updated()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];
        await RunImport(client);

        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }, Large]];
        var result = await RunImport(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((result.Added, result.Updated, result.Unchanged), Is.EqualTo((0, 1, 1)));
            Assert.That(list!.Items.Single(o => o.Title == "Small flat").Price, Is.EqualTo(2000m));
        });
    }

    [Test]
    public async Task List_is_paged()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];
        await RunImport(client);

        var page = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers?page=2&pageSize=1", Json);
        var tooLarge = await client.GetAsync(new Uri($"/api/housing/offers?pageSize={PageRequest.MaxPageSize + 1}", UriKind.Relative));
        var farAway = await client.GetFromJsonAsync<PagedResult<OfferListItem>>($"/api/housing/offers?page={int.MaxValue}&pageSize={PageRequest.MaxPageSize}", Json);

        Assert.Multiple(() =>
        {
            Assert.That(page!.Items, Has.Count.EqualTo(1));
            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(tooLarge.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(farAway!.Items, Is.Empty);
        });
    }

    [Test]
    public async Task List_is_sorted_by_the_chosen_column_with_missing_values_last()
    {
        await using var factory = App(WithFakes);
        using var client = factory.CreateClient();
        var noPrice = new Offer(Source.OtoDom, "4", "Flat without price", new Uri("https://www.otodom.pl/pl/oferta/none-ID4"), PropertyType.Flat, Transaction.Rent, At(13), AreaM2: 40m, Rooms: 2);
        _source.Pages = [[noPrice, Small, Medium, Large]];
        await RunImport(client);

        var cheapest = await ListedTitles(client, "?sort=Price&direction=Ascending");
        var dearest = await ListedTitles(client, "?sort=Price&direction=Descending");
        var perM2 = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers?sort=PricePerM2&direction=Ascending", Json);

        Assert.Multiple(() =>
        {
            Assert.That(cheapest, Is.EqualTo(new[] { "Small flat", "Medium flat", "Large flat", "Flat without price" }));
            Assert.That(dearest, Is.EqualTo(new[] { "Large flat", "Medium flat", "Small flat", "Flat without price" }));
            Assert.That(perM2!.Items.Select(o => (o.Title, o.PricePerM2)), Is.EqualTo(new[]
            {
                ("Large flat", (decimal?)56.55m), ("Medium flat", 60m), ("Small flat", 73.33m), ("Flat without price", null),
            }));
        });
    }

    [Test]
    public async Task List_is_filtered_by_price_area_rooms_and_features()
    {
        await using var factory = App(WithFakes);
        using var client = factory.CreateClient();
        var noPrice = new Offer(Source.OtoDom, "4", "Flat without price", new Uri("https://www.otodom.pl/pl/oferta/none-ID4"), PropertyType.Flat, Transaction.Rent, At(13), AreaM2: 50m, Rooms: 2);
        _source.Pages = [[
            noPrice,
            Small with { Features = [Feature.Balcony] },
            Medium with { Features = [Feature.Balcony, Feature.Lift] },
            Large with { Features = [Feature.Lift] },
        ]];
        await RunImport(client);

        var byPrice = await ListedTitles(client, "?priceMin=3000&priceMax=4100&sort=Price&direction=Ascending");
        var byAreaAndRooms = await ListedTitles(client, "?areaMin=30.5&areaMax=50&roomsMin=2&roomsMax=2");
        var byFeatures = await ListedTitles(client, "?feature=Lift&feature=Balcony");
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers?feature=Lift&pageSize=1", Json);

        Assert.Multiple(() =>
        {
            Assert.That(byPrice, Is.EqualTo(new[] { "Medium flat", "Large flat" }));
            Assert.That(byAreaAndRooms, Is.EquivalentTo(new[] { "Medium flat", "Flat without price" }));
            Assert.That(byFeatures, Is.EqualTo(new[] { "Medium flat" }));
            Assert.That((list!.Items.Count, list.TotalCount), Is.EqualTo((1, 2)));
        });
    }

    [Test]
    public async Task List_is_filtered_by_place_and_by_its_size_under_the_configured_thresholds()
    {
        await using var factory = App(WithFakes, ("Modules:Housing:PlaceSizes:Town:Metropolis", "2000000"));
        using var client = factory.CreateClient();
        await JobRuns.Run(client, ImportPlaces);
        _source.Pages = [[Small with { LocationKey = WarszawaKey }, Medium, Large with { LocationKey = GoscicinoKey }]];
        await RunImport(client);
        var warszawa = (await client.GetFromJsonAsync<PlaceListItem[]>("/api/housing/places?search=warszawa", Json))!.Single();

        var inPomorskie = await ListedTitles(client, "?voivodeship=Pomorskie");
        var inPomorskieOrWarszawa = await ListedTitles(client, $"?voivodeship=Pomorskie&place={warszawa.Id}");
        var metropolis = await ListedTitles(client, "?size=Metropolis");
        var bigCityOrVillage = await ListedTitles(client, "?size=VeryLargeCity&size=VeryLargeVillage");

        Assert.Multiple(() =>
        {
            Assert.That(inPomorskie, Is.EqualTo(new[] { "Large flat" }));
            Assert.That(inPomorskieOrWarszawa, Is.EquivalentTo(new[] { "Small flat", "Large flat" }));
            Assert.That(metropolis, Is.Empty);
            Assert.That(bigCityOrVillage, Is.EquivalentTo(new[] { "Small flat", "Large flat" }));
        });
    }

    [Test]
    public async Task Places_with_offers_are_found_by_the_start_of_a_word_in_their_name()
    {
        var bielsko = new Place("0943000", "Bielsko-Biała", Voivodeship.Slaskie, Kind: PlaceKind.Town, Population: 168_000);
        _places.FetchAsync(Arg.Any<CancellationToken>()).Returns(new PlaceFetchResult([Warszawa, Goscicino, bielsko]));
        await using var factory = App(WithFakes);
        using var client = factory.CreateClient();
        await JobRuns.Run(client, ImportPlaces);
        _source.Pages = [[Small with { LocationKey = "slaskie/bielsko--biala/bielsko--biala/bielsko--biala" }, Large with { LocationKey = GoscicinoKey }]];
        await RunImport(client);

        var byWord = await client.GetFromJsonAsync<PlaceListItem[]>("/api/housing/places?search=Biała", Json);
        var withoutOffers = await client.GetFromJsonAsync<PlaceListItem[]>("/api/housing/places?search=warsz", Json);
        var gościcino = (await client.GetFromJsonAsync<PlaceListItem[]>("/api/housing/places?search=gości", Json))!.Single();
        var byId = await client.GetFromJsonAsync<PlaceListItem>($"/api/housing/places/{gościcino.Id}", Json);
        var missing = await client.GetAsync(new Uri($"/api/housing/places/{gościcino.Id + 1000}", UriKind.Relative));

        Assert.Multiple(() =>
        {
            Assert.That(byWord!.Select(p => (p.Name, p.OfferCount)), Is.EqualTo(new[] { ("Bielsko-Biała", 1) }));
            Assert.That(withoutOffers, Is.Empty);
            Assert.That((byId!.Name, byId.County, byId.Kind, byId.OfferCount), Is.EqualTo(("Gościcino", "wejherowski", (PlaceKind?)PlaceKind.Village, 1)));
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task List_is_filtered_by_recorded_changes_and_price_cuts()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Medium, Large]];
        await RunImport(client);
        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }, Medium with { Price = new Money(3200m, Currency.Pln) }, Large]];
        await RunImport(client);

        var cut = await ListedTitles(client, "?priceCut=true");
        var changed = await ListedTitles(client, "?changed=true");
        var unchanged = await ListedTitles(client, "?changed=false");

        Assert.Multiple(() =>
        {
            Assert.That(cut, Is.EqualTo(new[] { "Small flat" }));
            Assert.That(changed, Is.EquivalentTo(new[] { "Small flat", "Medium flat" }));
            Assert.That(unchanged, Is.EqualTo(new[] { "Large flat" }));
        });
    }

    [Test]
    public async Task Furnished_comes_from_the_detail_page_is_filtered_and_its_changes_are_recorded()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Medium, Large]];
        _source.Details["1"] = new OfferDetails(Furnished: Answer.Yes);
        _source.Details["3"] = new OfferDetails(Furnished: Answer.No);
        await RunImport(client);
        await RunDetails(client);

        var furnished = await ListedTitles(client, "?furnished=Yes");
        var unfurnished = await ListedTitles(client, "?furnished=No");
        var small = (await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers?furnished=Yes", Json))!.Items.Single();

        _source.Pages = [[Small with { Price = new Money(2100m, Currency.Pln) }, Medium, Large]];
        _source.Details["1"] = new OfferDetails(Furnished: Answer.No);
        await RunImport(client);
        await RunDetails(client);
        var card = await client.GetFromJsonAsync<OfferListItem>($"/api/housing/offers/{small.Id}", Json);

        Assert.Multiple(() =>
        {
            Assert.That(furnished, Is.EqualTo(new[] { "Small flat" }));
            Assert.That(unfurnished, Is.EqualTo(new[] { "Medium flat" }));
            Assert.That(card!.Furnished, Is.EqualTo(Answer.No));
            var change = card.Changes.SelectMany(c => c.Changes).Single(c => c.Field == "furnished");
            Assert.That((change.Before.ToString(), change.After?.ToString()), Is.EqualTo(("Yes", "No")));
        });
    }

    [Test]
    public async Task Import_refuses_to_run_without_vpn()
    {
        var noVpn = Substitute.For<IVpnCheck>();
        noVpn.EnsureTunnelAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new ScrapingException(ScrapingException.VpnRequired, "no VPN in tests"));
        await using var factory = App(s => s.AddSingleton(noVpn));
        using var client = factory.CreateClient();

        var run = await JobRuns.Run(client, Import);

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(run.ErrorCode, Is.EqualTo(ScrapingException.VpnRequired));
        });
    }

    [Test]
    public async Task Import_keeps_the_raw_page_and_links_offers_to_it()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];

        await RunImport(client);
        var captureId = await Scalar<int>("SELECT id FROM housing.raw_capture");
        var linkedId = await Scalar<int?>("SELECT raw_capture_id FROM housing.offer");
        var file = Directory.GetFiles(_rawPath, "*.zst", SearchOption.AllDirectories).Single();

        Assert.Multiple(() =>
        {
            Assert.That(linkedId, Is.EqualTo(captureId));
            Assert.That(Path.GetFileName(file), Is.EqualTo($"{captureId}.zst"));
        });
    }

    [Test]
    public async Task Import_stores_and_parses_the_anonymized_page()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small with { Title = "Flat of Jan Kowalski" }]];

        await RunImport(client);
        var file = Directory.GetFiles(_rawPath, "*.zst", SearchOption.AllDirectories).Single();
        await using var zstd = new DecompressionStream(File.OpenRead(file));
        var stored = await new StreamReader(zstd).ReadToEndAsync();
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Does.Not.Contain("Kowalski"));
            Assert.That(list!.Items.Single().Title, Is.EqualTo("Flat of Jan ***"));
        });
    }

    [Test]
    public async Task Page_the_parser_cannot_read_is_kept()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.ParseFailure = new Failure("housing.parseFailed", "no data in the page", FailureKind.Failed);

        var run = await JobRuns.Run(client, Import);
        var captures = await Scalar<int>("SELECT count(*)::int FROM housing.raw_capture");
        var files = Directory.GetFiles(_rawPath, "*.zst", SearchOption.AllDirectories);

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(run.ErrorCode, Is.EqualTo("housing.parseFailed"));
            Assert.That(captures, Is.EqualTo(1));
            Assert.That(files, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task Import_stops_after_the_page_with_an_offer_known_from_an_earlier_import()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Large]];
        await RunImport(client);

        _source.Pages = [[Medium, Large], [Small]];
        var result = await RunImport(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((result.Pages, result.Added, result.Unchanged), Is.EqualTo((1, 1, 1)));
            Assert.That(list!.Items.Select(o => o.Title), Is.EquivalentTo(new[] { "Medium flat", "Large flat" }));
        });
    }

    [Test]
    public async Task Offer_pushed_onto_the_next_page_during_an_import_does_not_stop_it()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small], [Small, Medium], [Large]];

        var result = await RunImport(client);

        Assert.That((result.Pages, result.Added), Is.EqualTo((3, 3)));
    }

    [Test]
    public async Task Offers_are_listed_by_the_portal_date_newest_first()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small], [Medium], [Large]];

        var result = await RunImport(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That(result.Pages, Is.EqualTo(3));
            Assert.That(list!.Items.Select(o => o.Title), Is.EqualTo(new[] { "Small flat", "Medium flat", "Large flat" }));
        });
    }

    [Test]
    public async Task Import_never_reads_more_pages_than_the_limit()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)))
            .WithWebHostBuilder(b => b.UseSetting("Modules:Housing:Import:MaxPages", "2"));
        using var client = factory.CreateClient();
        _source.Pages = [[Small], [Medium], [Large]];

        var result = await RunImport(client);

        Assert.That((result.Pages, result.Added), Is.EqualTo((2, 2)));
    }

    [Test]
    public async Task Full_scan_reads_past_known_offers_to_the_last_page()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small], [Large]];
        await RunImport(client);

        _source.Pages = [[Medium, Small], [Large]];
        var result = await RunScan(client);

        Assert.That((result.Pages, result.Added, result.Missed), Is.EqualTo((2, 1, 0)));
    }

    [Test]
    public async Task Offer_missing_from_two_complete_scans_is_removed_and_comes_back_when_it_reappears()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];
        await RunScan(client);

        _source.Pages = [[Small]];
        var firstMiss = await RunScan(client);
        var afterFirstMiss = await ListedTitles(client);
        var secondMiss = await RunScan(client);
        var afterSecondMiss = await ListedTitles(client);

        _source.Pages = [[Small, Large]];
        await RunImport(client);
        var afterReturn = await ListedTitles(client);

        Assert.Multiple(() =>
        {
            Assert.That((firstMiss.Missed, firstMiss.Removed), Is.EqualTo((1, 0)));
            Assert.That(afterFirstMiss, Is.EquivalentTo(new[] { "Small flat", "Large flat" }));
            Assert.That((secondMiss.Missed, secondMiss.Removed), Is.EqualTo((1, 1)));
            Assert.That(afterSecondMiss, Is.EqualTo(new[] { "Small flat" }));
            Assert.That(afterReturn, Is.EquivalentTo(new[] { "Small flat", "Large flat" }));
        });
    }

    [Test]
    public async Task Scan_stopped_by_the_page_limit_fails_and_marks_nothing()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)))
            .WithWebHostBuilder(b => b.UseSetting("Modules:Housing:Import:FullScanMaxPages", "1"));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];
        await RunScan(client);

        _source.Pages = [[Small], [Medium]];
        var first = await JobRuns.Run(client, Scan);
        await JobRuns.Run(client, Scan);
        var listed = await ListedTitles(client);
        var pagesRead = await Scalar<int>("SELECT count(*)::int FROM housing.raw_capture");

        Assert.Multiple(() =>
        {
            Assert.That(first.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(first.ErrorCode, Is.EqualTo("housing.pageLimitReached"));
            Assert.That(listed, Is.EquivalentTo(new[] { "Small flat", "Large flat" }));
            Assert.That(pagesRead, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Reparse_rebuilds_offers_from_raw_pages()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];
        await RunImport(client);
        await Sql("DELETE FROM housing.offer");

        var response = await client.PostAsync(new Uri("/api/housing/reparse", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ReparseResult>(Json);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((result!.CaptureCount, result.FailedCount, result.AddedCount), Is.EqualTo((1, 0, 2)));
            Assert.That(list!.Items.Select(o => o.Title), Is.EquivalentTo(new[] { "Small flat", "Large flat" }));
        });
    }

    [Test]
    public async Task Reparse_keeps_the_newest_data_of_each_offer()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        await RunImport(client);
        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }]];
        await RunImport(client);

        var response = await client.PostAsync(new Uri("/api/housing/reparse", UriKind.Relative), null);
        var result = await response.Content.ReadFromJsonAsync<ReparseResult>(Json);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((result!.CaptureCount, result.UpdatedCount, result.UnchangedCount), Is.EqualTo((2, 0, 2)));
            Assert.That(list!.Items.Single().Price, Is.EqualTo(2000m));
        });
    }

    [Test]
    public async Task Details_are_fetched_for_new_and_changed_offers_but_not_bumped_ones()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];
        _source.Details["1"] = new OfferDetails(Rent: new Money(500m, Currency.Pln), Floor: 3, Latitude: 52.2297m, Longitude: 21.0122m,
            LocationRadiusM: 0, Description: "<p>Small</p>", Features: [Feature.Balcony, Feature.Lift]);
        _source.Details["2"] = new OfferDetails(Floor: 0);

        await RunImport(client);
        var first = await RunDetails(client);
        var second = await RunDetails(client);
        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }, Large]];
        await RunImport(client);
        var changed = await RunDetails(client);
        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln), ExternalUpdatedAt = At(13) }, Large]];
        var bump = await RunImport(client);
        var bumped = await RunDetails(client);

        var small = await Scalar<string>("""
            SELECT concat_ws('|', rent, rent_currency, floor, latitude, location_radius_m, description)
            FROM housing.offer WHERE external_id = '1'
            """);
        var features = await Scalar<string>("SELECT string_agg(feature::text, ',' ORDER BY feature) FROM housing.offer_feature");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new Fetched(2, 0)));
            Assert.That(second, Is.EqualTo(new Fetched(0, 0)));
            Assert.That(changed, Is.EqualTo(new Fetched(1, 0)));
            Assert.That((bump.Updated, bumped.Count), Is.EqualTo((1, 0)));
            Assert.That(_source.DetailRequests, Is.EqualTo(new[] { "1", "2", "1" }));
            Assert.That(small, Is.EqualTo("500.00|1|3|52.229700|0|<p>Small</p>"));
            Assert.That(features, Is.EqualTo($"{(int)Feature.Balcony},{(int)Feature.Lift}"));
        });
    }

    [Test]
    public async Task New_details_never_clear_known_values_and_replace_features()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(Rent: new Money(500m, Currency.Pln), Floor: 3, Features: [Feature.Balcony]);
        await RunImport(client);
        await RunDetails(client);

        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }]];
        _source.Details["1"] = new OfferDetails(Floor: 4, Features: [Feature.Lift]);
        await RunImport(client);
        await RunDetails(client);

        var stored = await Scalar<string>("SELECT concat_ws('|', rent, floor) FROM housing.offer");
        var features = await Scalar<string>("SELECT string_agg(feature::text, ',') FROM housing.offer_feature");

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.EqualTo("500.00|4"));
            Assert.That(features, Is.EqualTo($"{(int)Feature.Lift}"));
        });
    }

    [Test]
    public async Task A_rejected_value_clears_the_field_and_the_history_shows_both_steps()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(BuildYear: 1950);
        await RunImport(client);
        await RunDetails(client);

        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }]];
        _source.Details["1"] = new OfferDetails(RejectedValues: new Dictionary<string, string> { ["build_year"] = "195014" });
        await RunImport(client);
        var rejectedRun = await RunDetails(client);
        var rejected = await Scalar<string>("SELECT concat_ws('|', build_year, rejected_values->>'build_year') FROM housing.offer");

        _source.Pages = [[Small with { Price = new Money(2100m, Currency.Pln) }]];
        _source.Details["1"] = new OfferDetails(BuildYear: 1960);
        await RunImport(client);
        await RunDetails(client);
        var corrected = await Scalar<string>("SELECT concat_ws('|', build_year, rejected_values) FROM housing.offer");
        var id = (await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json))!.Items.Single().Id;
        using var card = await client.GetFromJsonAsync<JsonDocument>($"/api/housing/offers/{id}", Json);
        var yearChanges = card!.RootElement.GetProperty("changes").EnumerateArray()
            .SelectMany(o => o.GetProperty("changes").EnumerateArray())
            .Where(c => c.GetProperty("field").GetString() == "buildYear")
            .Select(c => (Before: c.GetProperty("before").ToString(), After: c.GetProperty("after").ToString()))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rejectedRun, Is.EqualTo(new Fetched(1, 0)));
            Assert.That(rejected, Is.EqualTo("195014"));
            Assert.That(corrected, Is.EqualTo("1960"));
            Assert.That(yearChanges, Is.EqualTo(new[] { ("", "1960"), ("1950", "") }), "newest first; the first detail page is no change");
        });
    }

    [Test]
    public async Task The_first_detail_page_is_no_change_but_a_later_new_value_is()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(Heating: Heating.Gas, BuildYear: 1950, Features: [Feature.Lift]);
        await RunImport(client);
        await RunDetails(client);
        var afterFirstPage = await Scalar<long>("SELECT count(*) FROM housing.offer_change");

        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }]];
        _source.Details["1"] = new OfferDetails(Heating: Heating.Gas, BuildYear: 1950, Condition: Condition.ReadyToUse, Features: [Feature.Lift]);
        await RunImport(client);
        await RunDetails(client);
        var logged = await Scalar<string>("SELECT string_agg(key, ',' ORDER BY id, key) FROM housing.offer_change, jsonb_object_keys(changes) AS key");

        Assert.Multiple(() =>
        {
            Assert.That(afterFirstPage, Is.Zero);
            Assert.That(logged, Is.EqualTo("price,condition"));
        });
    }

    [Test]
    public async Task Listing_fields_are_kept_and_details_complete_them()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small with { Rent = new Money(400m, Currency.Pln), Floor = 2, Features = [Feature.Balcony] }]];
        _source.Details["1"] = new OfferDetails(Deposit: new Money(3000m, Currency.Pln), Features: [Feature.Balcony, Feature.Dishwasher]);

        await RunImport(client);
        await RunDetails(client);

        var stored = await Scalar<string>("SELECT concat_ws('|', rent, floor, deposit) FROM housing.offer");
        var features = await Scalar<string>("SELECT string_agg(feature::text, ',' ORDER BY feature) FROM housing.offer_feature");

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.EqualTo("400.00|2|3000.00"));
            Assert.That(features, Is.EqualTo($"{(int)Feature.Balcony},{(int)Feature.Dishwasher}"));
        });
    }

    [Test]
    public async Task The_offer_card_shows_the_details()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(Deposit: new Money(3000m, Currency.Pln), AvailableFrom: new DateOnly(2026, 12, 1),
            Floor: 2, BuildingType: BuildingType.Block, Heating: Heating.Gas, Description: "<p>Quiet</p>", Features: [Feature.Lift]);
        await RunImport(client);
        var id = (await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json))!.Items.Single().Id;
        var before = await client.GetFromJsonAsync<OfferListItem>($"/api/housing/offers/{id}", Json);

        await RunDetails(client);
        var card = await client.GetFromJsonAsync<OfferListItem>($"/api/housing/offers/{id}", Json);

        Assert.Multiple(() =>
        {
            Assert.That(before!.DetailsFetchedAt, Is.Null);
            Assert.That(card!.DetailsFetchedAt, Is.Not.Null);
            Assert.That((card.Deposit, card.DepositCurrency, card.AvailableFrom, card.Floor),
                Is.EqualTo(((decimal?)3000m, (Currency?)Currency.Pln, (DateOnly?)new DateOnly(2026, 12, 1), (int?)2)));
            Assert.That((card.BuildingType, card.Heating, card.Description),
                Is.EqualTo(((BuildingType?)BuildingType.Block, (Heating?)Heating.Gas, "<p>Quiet</p>")));
            Assert.That(card.Features, Is.EqualTo(new[] { Feature.Lift }));
        });
    }

    [Test]
    public async Task The_offer_card_shows_the_changes_newest_first_with_enum_names()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(Heating: Heating.Gas, Features: [Feature.Lift]);
        await RunImport(client);
        await RunDetails(client);
        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }]];
        _source.Details["1"] = new OfferDetails(Heating: Heating.District, Features: [Feature.Lift, Feature.Balcony]);
        await RunImport(client);
        await RunDetails(client);
        var id = (await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json))!.Items.Single().Id;

        using var card = await client.GetFromJsonAsync<JsonDocument>($"/api/housing/offers/{id}", Json);
        var changes = card!.RootElement.GetProperty("changes").EnumerateArray()
            .Select(o => o.GetProperty("changes").EnumerateArray()
                .Select(c => (Field: c.GetProperty("field").GetString(), Before: c.GetProperty("before").ToString(), After: c.GetProperty("after").ToString()))
                .ToList())
            .ToList();

        Assert.That(changes, Is.EqualTo(new[]
        {
            new[] { ("features", """["Lift"]""", """["Balcony","Lift"]"""), ("heating", "Gas", "District") },
            new[] { ("price", "2200.00", "2000.00") },
        }));
    }

    [Test]
    public async Task A_short_outage_is_retried_and_the_run_goes_on()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)), QuickRetry);
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];
        _source.Details["1"] = new OfferDetails(Floor: 1);
        _source.Details["2"] = new OfferDetails(Floor: 2);
        await RunImport(client);
        _source.DetailTimeouts = 2;

        var run = await JobRuns.Run(client, FetchDetails);

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded), run.ErrorCode);
            Assert.That(run.Summary!.Counts["fetched"], Is.EqualTo(2));
            Assert.That(run.Summary.Counts["retried"], Is.EqualTo(2));
        });
    }

    [Test]
    public async Task A_retry_that_would_pass_the_time_limit_ends_the_run()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)),
            QuickRetry, ("Modules:Housing:Details:MaxMinutes", "1"));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(Floor: 1);
        await RunImport(client);
        _source.DetailTimeouts = 1;

        var run = await JobRuns.Run(client, FetchDetails);

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(_source.DetailRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task A_rate_limited_portal_is_not_retried()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)), QuickRetry);
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(Floor: 1);
        await RunImport(client);
        _source.DetailsRateLimited = true;

        var run = await JobRuns.Run(client, FetchDetails);

        Assert.Multiple(() =>
        {
            Assert.That((run.Status, run.ErrorCode), Is.EqualTo((JobRunStatus.Failed, (string?)ScrapingException.RateLimited)));
            Assert.That(_source.DetailRequests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task An_unreachable_portal_fails_the_run_and_blames_no_offer()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)),
            QuickRetry, ("Modules:Housing:Details:Retries", "1"));
        using var client = factory.CreateClient();
        _source.Pages = [[Small, Large]];
        _source.Details["1"] = new OfferDetails(Floor: 1);
        _source.Details["2"] = new OfferDetails(Floor: 2);
        await RunImport(client);
        _source.DetailsUnavailable = true;

        var failedRuns = new List<JobRun>();
        for (var run = 0; run < 3; run++)
        {
            failedRuns.Add(await JobRuns.Run(client, FetchDetails));
        }

        var attempts = await Scalar<long>("SELECT count(*) FROM housing.offer_detail_fetch");
        _source.DetailsUnavailable = false;
        var later = await RunDetails(client);

        Assert.Multiple(() =>
        {
            Assert.That(failedRuns.Select(r => (r.Status, r.ErrorCode)),
                Is.All.EqualTo((JobRunStatus.Failed, (string?)ScrapingException.VpnRequired)));
            Assert.That(attempts, Is.Zero);
            Assert.That(later, Is.EqualTo(new Fetched(2, 0)));
        });
    }

    [Test]
    public async Task A_portal_refusing_every_page_stops_the_run_and_blames_no_offer()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [Enumerable.Range(1, FetchDetailsHandler.MaxFetchFailuresInRow + 2)
            .Select(i => Small with { ExternalId = $"{i}", Url = new Uri($"https://www.otodom.pl/pl/oferta/flat-ID{i}") })
            .ToList()];
        await RunImport(client);
        _source.DetailsRefused = true;

        var run = await JobRuns.Run(client, FetchDetails);
        var blamed = await Scalar<long>("SELECT count(*) FROM housing.offer_detail_fetch");

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(blamed, Is.Zero);
        });
    }

    [Test]
    public async Task A_detail_page_saying_the_offer_is_removed_removes_it_without_stopping_the_run()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        // Newer than Large, so first in the queue; more than any limit of failures in a row.
        var removed = Enumerable.Range(1, FetchDetailsHandler.MaxMissingPagesInRow + 2)
            .Select(i => Small with { ExternalId = $"removed-{i}", Url = new Uri($"https://www.otodom.pl/pl/oferta/removed-ID{i}") })
            .ToList();
        _source.Pages = [[.. removed, Large]];
        _source.Details["2"] = new OfferDetails(Floor: 2);
        _source.RemovedOffers.UnionWith(removed.Select(o => o.ExternalId));
        await RunImport(client);

        var run = await JobRuns.Run(client, FetchDetails);
        _source.DetailRequests.Clear();
        await RunDetails(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded), run.ErrorCode);
            Assert.That(run.Summary!.Counts["removed"], Is.EqualTo(removed.Count));
            Assert.That(run.Summary.Counts["fetched"], Is.EqualTo(1));
            Assert.That(list!.TotalCount, Is.EqualTo(1));
            Assert.That(_source.DetailRequests, Is.Empty);
        });
    }

    [Test]
    public async Task Missing_pages_at_the_head_of_the_queue_do_not_stop_the_run()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        // Newer than Large, so first in the queue; without detail pages they answer 404.
        var missing = Enumerable.Range(1, FetchDetailsHandler.MaxFetchFailuresInRow + 2)
            .Select(i => Small with { ExternalId = $"missing-{i}", Url = new Uri($"https://www.otodom.pl/pl/oferta/missing-ID{i}") })
            .ToList();
        _source.Pages = [[.. missing, Large]];
        _source.Details["2"] = new OfferDetails(Floor: 2);
        await RunImport(client);

        var fetched = await RunDetails(client);

        Assert.That(fetched, Is.EqualTo(new Fetched(1, missing.Count)));
    }

    [Test]
    public async Task A_portal_answering_404_to_every_page_stops_the_run_and_blames_no_offer()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        // No detail pages: every fetch answers 404, as after the portal changed its URLs.
        _source.Pages = [Enumerable.Range(1, FetchDetailsHandler.MaxMissingPagesInRow + 2)
            .Select(i => Small with { ExternalId = $"{i}", Url = new Uri($"https://www.otodom.pl/pl/oferta/flat-ID{i}") })
            .ToList()];
        await RunImport(client);

        var run = await JobRuns.Run(client, FetchDetails);
        var blamed = await Scalar<long>("SELECT count(*) FROM housing.offer_detail_fetch");

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(JobRunStatus.Failed));
            Assert.That(blamed, Is.Zero);
        });
    }

    [Test]
    public async Task Listing_tags_do_not_come_back_after_the_details_replaced_them()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small with { Features = [Feature.Garden] }]];
        _source.Details["1"] = new OfferDetails(Features: []);
        await RunImport(client);
        await RunDetails(client);

        await RunImport(client);

        Assert.That(await Scalar<long>("SELECT count(*) FROM housing.offer_feature"), Is.Zero);
    }

    [Test]
    public async Task A_failing_detail_page_is_retried_until_the_limit()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];

        await RunImport(client);
        var runs = new List<Fetched>();
        for (var run = 0; run < 4; run++)
        {
            runs.Add(await RunDetails(client));
        }

        var lastError = await Scalar<string?>("SELECT last_error FROM housing.offer_detail_fetch");

        Assert.Multiple(() =>
        {
            Assert.That(runs.Select(f => f.Failed), Is.EqualTo(new[] { 1, 1, 1, 0 }));
            Assert.That(lastError, Is.Not.Null);
        });
    }

    [Test]
    public async Task Reparse_restores_details_from_raw_pages()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(Floor: 3, Features: [Feature.Garden]);
        await RunImport(client);
        await RunDetails(client);
        await Sql("UPDATE housing.offer SET floor = NULL; DELETE FROM housing.offer_feature;");

        var response = await client.PostAsync(new Uri("/api/housing/reparse", UriKind.Relative), null);
        var result = await response.Content.ReadFromJsonAsync<ReparseResult>(Json);

        var floor = await Scalar<int>("SELECT floor FROM housing.offer");
        var feature = await Scalar<int>("SELECT feature FROM housing.offer_feature");

        Assert.Multiple(() =>
        {
            Assert.That((result!.CaptureCount, result.FailedCount, result.DetailCount), Is.EqualTo((2, 0, 1)));
            Assert.That((floor, feature), Is.EqualTo((3, (int)Feature.Garden)));
        });
    }

    [Test]
    public async Task Content_changes_are_recorded_but_the_first_detail_page_and_bumps_are_not()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small with { Features = [Feature.Garden] }]];
        _source.Details["1"] = new OfferDetails(Floor: 3, AvailableFrom: new DateOnly(2026, 12, 1), Features: [Feature.Balcony]);
        await RunImport(client);
        await RunDetails(client);

        var cheaper = Small with { Price = new Money(2000m, Currency.Pln), Rent = new Money(500m, Currency.Pln) };
        _source.Pages = [[cheaper]];
        _source.Details["1"] = new OfferDetails(Floor: 4, AvailableFrom: new DateOnly(2027, 1, 1), Features: [Feature.Lift, Feature.Balcony]);
        await RunImport(client);
        await RunDetails(client);
        _source.Pages = [[cheaper with { ExternalUpdatedAt = At(13) }]];
        await RunImport(client);
        _source.Pages = [[cheaper with { ExternalUpdatedAt = At(13), Price = null }]];
        await RunImport(client);

        var logged = await Scalar<string>("""
            SELECT string_agg(key, ',' ORDER BY id, key)
            FROM housing.offer_change, jsonb_object_keys(changes) AS key
            """);
        var price = await Scalar<bool>("""
            SELECT array_agg(changes -> 'price' ORDER BY id) = ARRAY['[2200, 2000]', '[2000, null]']::jsonb[]
            FROM housing.offer_change WHERE changes ? 'price'
            """);
        var availableFrom = await Scalar<bool>("""
            SELECT changes -> 'available_from' = '["2026-12-01", "2027-01-01"]'
            FROM housing.offer_change WHERE changes ? 'available_from'
            """);
        var newFeatures = string.Join(",", new[] { (int)Feature.Balcony, (int)Feature.Lift }.Order());
        var features = await Scalar<bool>($"""
            SELECT changes -> 'features' = '[[{(int)Feature.Balcony}], [{newFeatures}]]'
            FROM housing.offer_change WHERE changes ? 'features'
            """);

        Assert.Multiple(() =>
        {
            Assert.That(logged, Is.EqualTo("price,rent,rent_currency,available_from,features,floor,price,price_currency"));
            Assert.That((price, availableFrom, features), Is.EqualTo((true, true, true)));
        });
    }

    [Test]
    public async Task Reparse_records_no_changes()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Pages = [[Small]];
        _source.Details["1"] = new OfferDetails(Floor: 3);
        await RunImport(client);
        await RunDetails(client);
        _source.Pages = [[Small with { Price = new Money(2000m, Currency.Pln) }]];
        _source.Details["1"] = new OfferDetails(Floor: 4);
        await RunImport(client);
        await RunDetails(client);
        var before = await Scalar<long>("SELECT count(*) FROM housing.offer_change");

        var response = await client.PostAsync(new Uri("/api/housing/reparse", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();

        Assert.Multiple(async () =>
        {
            Assert.That(before, Is.EqualTo(2));
            Assert.That(await Scalar<long>("SELECT count(*) FROM housing.offer_change"), Is.EqualTo(before));
        });
    }

    [Test]
    public async Task Import_of_an_unknown_source_is_not_found()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri("/api/housing/import/99", UriKind.Relative), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private void WithFakes(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source));
        services.Replace(ServiceDescriptor.Singleton(_places));
    }

    private WebApplicationFactory<Program> App(Action<IServiceCollection> services, params (string Key, string Value)[] settings) =>
        TestApp.WithAllModules(b =>
        {
            b.UseSetting($"ConnectionStrings:{DatabaseStatusEndpoint.ConnectionStringName}", DatabaseSetup.AppConnectionString)
                .UseSetting("Modules:Housing:RawCaptures:Path", _rawPath)
                .ConfigureTestServices(services);
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }
        });

    private static readonly (string, string) QuickRetry = ("Modules:Housing:Details:FirstRetrySeconds", "1");

    private static async Task Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(DatabaseSetup.AppConnectionString);
        await connection.ExecuteAsync(sql);
    }

    private static async Task<T> Scalar<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(DatabaseSetup.AppConnectionString);
        return (await connection.ExecuteScalarAsync<T>(sql))!;
    }

    private static async Task<Imported> RunImport(HttpClient client)
    {
        var run = await JobRuns.Run(client, Import);
        Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded), run.ErrorCode);
        var counts = run.Summary!.Counts;
        return new Imported((int)counts["pages"], (int)counts["added"], (int)counts["updated"], (int)counts["unchanged"]);
    }

    private static async Task<Fetched> RunDetails(HttpClient client)
    {
        var run = await JobRuns.Run(client, FetchDetails);
        Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded), run.ErrorCode);
        return new Fetched((int)run.Summary!.Counts["fetched"], (int)run.Summary.Counts["failed"]);
    }

    private static async Task<Scanned> RunScan(HttpClient client)
    {
        var run = await JobRuns.Run(client, Scan);
        Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded), run.ErrorCode);
        var counts = run.Summary!.Counts;
        return new Scanned((int)counts["pages"], (int)counts["added"], (int)counts["missed"], (int)counts["removed"]);
    }

    private static async Task<IReadOnlyList<string>> ListedTitles(HttpClient client, string query = "")
    {
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>($"/api/housing/offers{query}", Json);
        return list!.Items.Select(o => o.Title).ToList();
    }

    private static DateTimeOffset At(int hour) => new(2026, 11, 19, hour, 0, 0, TimeSpan.Zero);

    private sealed record Imported(int Pages, int Added, int Updated, int Unchanged);

    private sealed record Scanned(int Pages, int Added, int Missed, int Removed);

    private sealed record Fetched(int Count, int Failed);

    private sealed record FakePage(IReadOnlyList<Offer> Offers, bool IsLast);

    private sealed record FakeDetailPage(string ExternalId, OfferDetails Details);

    private sealed class FakeOfferSource : IOfferSource
    {
        // Each inner list is one results page, newest offers first.
        public IReadOnlyList<IReadOnlyList<Offer>> Pages { get; set; } = [[]];

        public Failure? ParseFailure { get; set; }

        public Source Source => Source.OtoDom;

        public Task<FetchResult> FetchListingPageAsync(int number, CancellationToken cancellationToken) =>
            Task.FromResult(new FetchResult(new RawPage(
                new Uri($"https://www.otodom.pl/pl/wyniki?page={number}"),
                CaptureKind.ListingPage,
                "application/json",
                200,
                JsonSerializer.SerializeToUtf8Bytes(new FakePage(Pages[number - 1], number == Pages.Count), Json))));

        public RawPage Anonymize(RawPage page) =>
            page with { Content = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(page.Content).Replace("Kowalski", "***", StringComparison.Ordinal)) };

        public ParseResult Parse(RawPage page)
        {
            if (ParseFailure is not null)
            {
                return ParseResult.Failed(ParseFailure);
            }

            var parsed = JsonSerializer.Deserialize<FakePage>(page.Content, Json)!;
            return new ParseResult(parsed.Offers, 0, parsed.IsLast);
        }

        public LocationHint? LocationHintOf(string locationKey) => OtoDomLocation.HintOf(locationKey);

        // Detail pages by external id; an offer without an entry, or no longer listed, returns HTTP 404.
        public Dictionary<string, OfferDetails> Details { get; } = [];

        public List<string> DetailRequests { get; } = [];

        // The portal out of reach, e.g. no VPN.
        public bool DetailsUnavailable { get; set; }

        // The next detail requests that time out, before the portal answers again.
        public int DetailTimeouts { get; set; }

        // HTTP 429 on every detail request.
        public bool DetailsRateLimited { get; set; }

        // HTTP 403 on every detail request, as when the portal blocks us.
        public bool DetailsRefused { get; set; }

        // External ids whose detail page answers HTTP 410: the portal removed the offer.
        public HashSet<string> RemovedOffers { get; } = [];

        public Task<FetchResult> FetchDetailPageAsync(Uri url, CancellationToken cancellationToken)
        {
            var externalId = Pages.SelectMany(p => p).FirstOrDefault(o => o.Url == url)?.ExternalId ?? url.ToString();
            DetailRequests.Add(externalId);
            if (DetailTimeouts > 0)
            {
                DetailTimeouts--;
                return Task.FromResult(FetchResult.Failed(new Failure(ScrapingException.NetworkError, "Timed out.", FailureKind.Unavailable)));
            }

            if (DetailsRateLimited)
            {
                return Task.FromResult(FetchResult.Failed(new Failure(ScrapingException.RateLimited, "HTTP 429.", FailureKind.RateLimited)));
            }

            if (DetailsUnavailable)
            {
                return Task.FromResult(FetchResult.Failed(new Failure(ScrapingException.VpnRequired, "No VPN.", FailureKind.Unavailable)));
            }

            if (DetailsRefused)
            {
                return Task.FromResult(FetchResult.Failed(new Failure(ScrapingException.HttpError, $"{url} returned HTTP 403.", FailureKind.Failed)));
            }

            if (RemovedOffers.Contains(externalId))
            {
                return Task.FromResult(FetchResult.Failed(new Failure(ScrapingException.HttpError, $"{url} returned HTTP 410.", FailureKind.Removed)));
            }

            if (!Details.TryGetValue(externalId, out var details))
            {
                return Task.FromResult(FetchResult.Failed(new Failure(ScrapingException.HttpError, $"{url} returned HTTP 404.", FailureKind.Missing)));
            }

            return Task.FromResult(new FetchResult(new RawPage(
                url, CaptureKind.DetailPage, "application/json", 200,
                JsonSerializer.SerializeToUtf8Bytes(new FakeDetailPage(externalId, details), Json))));
        }

        public DetailsParseResult ParseDetails(RawPage page)
        {
            var parsed = JsonSerializer.Deserialize<FakeDetailPage>(page.Content, Json)!;
            return new DetailsParseResult(parsed.ExternalId, parsed.Details);
        }
    }
}
