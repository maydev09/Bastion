using Bastion.Host;
using System.Net;
using System.Net.Http.Json;
using Bastion.BuildingBlocks.Scraping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Bastion.IntegrationTests;

public class FlatOffersSmokeTestTests
{
    [Test]
    public async Task Refuses_to_run_without_vpn()
    {
        var noVpn = Substitute.For<IVpnCheck>();
        noVpn.EnsureTunnelAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new ScrapingException(ScrapingException.VpnRequired, "no VPN in tests"));
        await using var factory = TestApp.WithAllModules(b =>
            b.ConfigureTestServices(s => s.AddSingleton(noVpn)));
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
        await using var factory = TestApp.WithAllModules(b => b.UseSetting(ModuleOptions.EnabledKey("FlatOffers"), "false"));
        using var client = factory.CreateClient();

        var response = await client.PostAsync(new Uri("/api/flat-offers/smoke-test", UriKind.Relative), null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

}
