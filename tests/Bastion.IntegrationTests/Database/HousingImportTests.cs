using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bastion.BuildingBlocks.Scraping;
using Bastion.Host;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.ImportOffers;
using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Application.ReparseOffers;
using Bastion.Modules.Housing.Domain;
using Bastion.Platform.Abstractions;
using Dapper;
using Microsoft.AspNetCore.Mvc;
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
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly Offer Small = new(Source.OtoDom, "1", "Small flat", new Uri("https://www.otodom.pl/pl/oferta/small-ID1"), PropertyType.Flat, Transaction.Rent, new Money(2200m, Currency.Pln), 30m, 1);
    private static readonly Offer Large = new(Source.OtoDom, "2", "Large flat", new Uri("https://www.otodom.pl/pl/oferta/large-ID2"), PropertyType.Flat, Transaction.Rent, new Money(4100m, Currency.Pln), 72.5m, 3);

    private FakeOfferSource _source = new();
    private string _rawPath = "";

    [SetUp]
    public async Task ClearData()
    {
        _source = new FakeOfferSource();
        _rawPath = Directory.CreateTempSubdirectory("bastion-raw-").FullName;
        await Sql("DELETE FROM housing.offer; DELETE FROM housing.raw_capture;");
    }

    [TearDown]
    public void DeleteRawFiles() => Directory.Delete(_rawPath, recursive: true);

    [Test]
    public async Task Importing_twice_stores_each_offer_once()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Offers = [Small, Large];

        var first = await Import(client);
        var second = await Import(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((first.AddedCount, first.UpdatedCount, first.UnchangedCount), Is.EqualTo((2, 0, 0)));
            Assert.That((second.AddedCount, second.UpdatedCount, second.UnchangedCount), Is.EqualTo((0, 0, 2)));
            Assert.That(list!.TotalCount, Is.EqualTo(2));
            Assert.That(list.Items.Select(o => o.Title), Is.EquivalentTo(new[] { "Small flat", "Large flat" }));
        });
    }

    [Test]
    public async Task Changed_offer_is_updated()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Offers = [Small, Large];
        await Import(client);

        _source.Offers = [Small with { Price = new Money(2000m, Currency.Pln) }, Large];
        var result = await Import(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((result.AddedCount, result.UpdatedCount, result.UnchangedCount), Is.EqualTo((0, 1, 1)));
            Assert.That(list!.Items.Single(o => o.Title == "Small flat").Price, Is.EqualTo(2000m));
        });
    }

    [Test]
    public async Task List_is_paged()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Offers = [Small, Large];
        await Import(client);

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
    public async Task Import_refuses_to_run_without_vpn()
    {
        var noVpn = Substitute.For<IVpnCheck>();
        noVpn.EnsureTunnelAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new ScrapingException(ScrapingException.VpnRequired, "no VPN in tests"));
        await using var factory = App(s => s.AddSingleton(noVpn));
        using var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri("/api/housing/import/OtoDom", UriKind.Relative), null);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(problem!.Extensions["code"]!.ToString(), Is.EqualTo(ScrapingException.VpnRequired));
        });
    }

    [Test]
    public async Task Import_keeps_the_raw_page_and_links_offers_to_it()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Offers = [Small];

        await Import(client);
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
        _source.Offers = [Small with { Title = "Flat of Jan Kowalski" }];

        await Import(client);
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

        var response = await client.PostAsync(new Uri("/api/housing/import/OtoDom", UriKind.Relative), null);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        var captures = await Scalar<int>("SELECT count(*)::int FROM housing.raw_capture");
        var files = Directory.GetFiles(_rawPath, "*.zst", SearchOption.AllDirectories);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
            Assert.That(problem!.Extensions["code"]!.ToString(), Is.EqualTo("housing.parseFailed"));
            Assert.That(captures, Is.EqualTo(1));
            Assert.That(files, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task Reparse_rebuilds_offers_from_raw_pages()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();
        _source.Offers = [Small, Large];
        await Import(client);
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
        _source.Offers = [Small];
        await Import(client);
        _source.Offers = [Small with { Price = new Money(2000m, Currency.Pln) }];
        await Import(client);

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
    public async Task Import_of_an_unknown_source_is_not_found()
    {
        await using var factory = App(s => s.Replace(ServiceDescriptor.Singleton<IOfferSource>(_source)));
        using var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri("/api/housing/import/99", UriKind.Relative), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private WebApplicationFactory<Program> App(Action<IServiceCollection> services) =>
        TestApp.WithAllModules(b => b
            .UseSetting($"ConnectionStrings:{DatabaseStatusEndpoint.ConnectionStringName}", DatabaseSetup.AppConnectionString)
            .UseSetting("Modules:Housing:RawCaptures:Path", _rawPath)
            .ConfigureTestServices(services));

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

    private static async Task<ImportResult> Import(HttpClient client)
    {
        var response = await client.PostAsync(new Uri("/api/housing/import/OtoDom", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ImportResult>(Json))!;
    }

    private sealed class FakeOfferSource : IOfferSource
    {
        public IReadOnlyList<Offer> Offers { get; set; } = [];

        public Failure? ParseFailure { get; set; }

        public Source Source => Source.OtoDom;

        public Task<FetchResult> FetchAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new FetchResult([
                new RawPage(new Uri("https://www.otodom.pl/pl/wyniki"), CaptureKind.ListingPage, "application/json", 200, JsonSerializer.SerializeToUtf8Bytes(Offers, Json)),
            ]));

        public RawPage Anonymize(RawPage page) =>
            page with { Content = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(page.Content).Replace("Kowalski", "***", StringComparison.Ordinal)) };

        public ParseResult Parse(RawPage page) => ParseFailure is null
            ? new(JsonSerializer.Deserialize<List<Offer>>(page.Content, Json)!, 0)
            : ParseResult.Failed(ParseFailure);
    }
}
