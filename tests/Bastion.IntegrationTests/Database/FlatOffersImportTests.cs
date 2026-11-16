using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bastion.BuildingBlocks.Scraping;
using Bastion.Host;
using Bastion.Modules.FlatOffers.Application.Abstractions;
using Bastion.Modules.FlatOffers.Application.ImportOffers;
using Bastion.Modules.FlatOffers.Application.ListOffers;
using Bastion.Modules.FlatOffers.Domain;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bastion.IntegrationTests.Database;

public class FlatOffersImportTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly ListingOffer Small = new("1", "Small flat", new Uri("https://www.otodom.pl/pl/oferta/small-ID1"), 2200m, "PLN", 30m, 1);
    private static readonly ListingOffer Large = new("2", "Large flat", new Uri("https://www.otodom.pl/pl/oferta/large-ID2"), 4100m, "PLN", 72.5m, 3);

    private readonly FakeListingSource _source = new();

    [SetUp]
    public async Task ClearOffers()
    {
        await using var connection = new NpgsqlConnection(DatabaseSetup.AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM flat_offers.offer", connection);
        await command.ExecuteNonQueryAsync();
    }

    [Test]
    public async Task Importing_twice_stores_each_offer_once()
    {
        await using var factory = App(s => s.AddSingleton<IListingSource>(_source));
        using var client = factory.CreateClient();
        _source.Offers = [Small, Large, Small];

        var first = await Import(client);
        var second = await Import(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/flat-offers/offers", Json);

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
        await using var factory = App(s => s.AddSingleton<IListingSource>(_source));
        using var client = factory.CreateClient();
        _source.Offers = [Small, Large];
        await Import(client);

        _source.Offers = [Small with { Price = 2000m }, Large];
        var result = await Import(client);
        var list = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/flat-offers/offers", Json);

        Assert.Multiple(() =>
        {
            Assert.That((result.AddedCount, result.UpdatedCount, result.UnchangedCount), Is.EqualTo((0, 1, 1)));
            Assert.That(list!.Items.Single(o => o.Title == "Small flat").Price, Is.EqualTo(2000m));
        });
    }

    [Test]
    public async Task List_is_paged()
    {
        await using var factory = App(s => s.AddSingleton<IListingSource>(_source));
        using var client = factory.CreateClient();
        _source.Offers = [Small, Large];
        await Import(client);

        var page = await client.GetFromJsonAsync<PagedResult<OfferListItem>>("/api/flat-offers/offers?page=2&pageSize=1", Json);
        var tooLarge = await client.GetAsync(new Uri($"/api/flat-offers/offers?pageSize={PageRequest.MaxPageSize + 1}", UriKind.Relative));

        Assert.Multiple(() =>
        {
            Assert.That(page!.Items, Has.Count.EqualTo(1));
            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(tooLarge.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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

        var response = await client.PostAsync(new Uri("/api/flat-offers/import", UriKind.Relative), null);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(problem!.Extensions["code"]!.ToString(), Is.EqualTo(ScrapingException.VpnRequired));
        });
    }

    private static WebApplicationFactory<Program> App(Action<IServiceCollection> services) =>
        TestApp.WithAllModules(b => b
            .UseSetting($"ConnectionStrings:{DatabaseStatusEndpoint.ConnectionStringName}", DatabaseSetup.AppConnectionString)
            .ConfigureTestServices(services));

    private static async Task<ImportResult> Import(HttpClient client)
    {
        var response = await client.PostAsync(new Uri("/api/flat-offers/import", UriKind.Relative), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ImportResult>(Json))!;
    }

    private sealed class FakeListingSource : IListingSource
    {
        public IReadOnlyList<ListingOffer> Offers { get; set; } = [];

        public OfferSource Source => OfferSource.OtoDom;

        public Task<ListingFetchResult> FetchListingPageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ListingFetchResult(Offers, 0));
    }
}
