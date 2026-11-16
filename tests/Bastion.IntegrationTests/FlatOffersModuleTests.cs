using System.Net;
using Bastion.Host;

namespace Bastion.IntegrationTests;

public class FlatOffersModuleTests
{
    [TestCase("POST", "/api/flat-offers/import")]
    [TestCase("GET", "/api/flat-offers/offers")]
    public async Task Disabled_module_has_no_endpoints(string method, string path)
    {
        await using var factory = TestApp.WithAllModules(b => b.UseSetting(ModuleOptions.EnabledKey("FlatOffers"), "false"));
        using var client = factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative)));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
