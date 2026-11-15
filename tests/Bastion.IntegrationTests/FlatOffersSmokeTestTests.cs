using System.Net;
using System.Net.Http.Json;
using Bastion.BuildingBlocks.Scraping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.IntegrationTests;

public class FlatOffersSmokeTestTests
{
    [Test]
    public async Task Refuses_to_run_without_vpn()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IVpnCheck>(new NoVpn())));
        using var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri("/api/flat-offers/smoke-test", UriKind.Relative), null);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(problem!.Extensions["code"]!.ToString(), Is.EqualTo(ScrapingException.VpnRequired));
        });
    }

    [Test]
    public async Task Disabled_module_has_no_endpoints()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("Modules:FlatOffers:Enabled", "false"));
        using var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri("/api/flat-offers/smoke-test", UriKind.Relative), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private sealed class NoVpn : IVpnCheck
    {
        public Task EnsureTunnelAsync(string host, CancellationToken cancellationToken) =>
            throw new ScrapingException(ScrapingException.VpnRequired, "no VPN in tests");
    }
}
