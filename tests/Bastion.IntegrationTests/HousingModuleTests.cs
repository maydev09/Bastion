using System.Net;
using Bastion.Host;

namespace Bastion.IntegrationTests;

public class HousingModuleTests
{
    [TestCase("POST", "/api/housing/import/OtoDom")]
    [TestCase("POST", "/api/housing/reparse")]
    [TestCase("GET", "/api/housing/offers")]
    [TestCase("POST", "/api/housing/jobs/RawCaptureRetention/runs")]
    [TestCase("GET", "/api/housing/jobs/RawCaptureRetention/runs/latest")]
    public async Task Disabled_module_has_no_endpoints(string method, string path)
    {
        await using var factory = TestApp.WithAllModules(b => b.UseSetting(ModuleOptions.EnabledKey("Housing"), "false"));
        using var client = factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative)));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
