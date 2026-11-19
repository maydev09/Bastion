using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bastion.BuildingBlocks.Scraping;
using Bastion.BuildingBlocks.Jobs;
using Bastion.Host;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Application.ReparseOffers;
using Bastion.Modules.Housing.Domain;
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

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly Offer Small = new(Source.OtoDom, "1", "Small flat", new Uri("https://www.otodom.pl/pl/oferta/small-ID1"), PropertyType.Flat, Transaction.Rent, At(12), new Money(2200m, Currency.Pln), 30m, 1);
    private static readonly Offer Medium = new(Source.OtoDom, "3", "Medium flat", new Uri("https://www.otodom.pl/pl/oferta/medium-ID3"), PropertyType.Flat, Transaction.Rent, At(11), new Money(3000m, Currency.Pln), 50m, 2);
    private static readonly Offer Large = new(Source.OtoDom, "2", "Large flat", new Uri("https://www.otodom.pl/pl/oferta/large-ID2"), PropertyType.Flat, Transaction.Rent, At(10), new Money(4100m, Currency.Pln), 72.5m, 3);

    private FakeOfferSource _source = new();
    private string _rawPath = "";

    [SetUp]
    public async Task ClearData()
    {
        _source = new FakeOfferSource();
        _rawPath = Directory.CreateTempSubdirectory("bastion-raw-").FullName;
        await Sql("DELETE FROM housing.job_run; DELETE FROM housing.offer; DELETE FROM housing.raw_capture;");
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

    private static async Task<Imported> RunImport(HttpClient client)
    {
        var run = await JobRuns.Run(client, Import);
        Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded), run.ErrorCode);
        var counts = run.Summary!.Counts;
        return new Imported((int)counts["pages"], (int)counts["added"], (int)counts["updated"], (int)counts["unchanged"]);
    }

    private static async Task<Scanned> RunScan(HttpClient client)
    {
        var run = await JobRuns.Run(client, Scan);
        Assert.That(run.Status, Is.EqualTo(JobRunStatus.Succeeded), run.ErrorCode);
        var counts = run.Summary!.Counts;
        return new Scanned((int)counts["pages"], (int)counts["added"], (int)counts["missed"], (int)counts["removed"]);
    }

    private static async Task<IReadOnlyList<string>> ListedTitles(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/housing/offers", Json);
        return list!.Items.Select(o => o.Title).ToList();
    }

    private static DateTimeOffset At(int hour) => new(2026, 11, 19, hour, 0, 0, TimeSpan.Zero);

    private sealed record Imported(int Pages, int Added, int Updated, int Unchanged);

    private sealed record Scanned(int Pages, int Added, int Missed, int Removed);

    private sealed record FakePage(IReadOnlyList<Offer> Offers, bool IsLast);

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
    }
}
