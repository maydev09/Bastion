using System.Net;
using System.Text.Json;
using Bastion.Host;

namespace Bastion.IntegrationTests;

public class DatabaseStatusTests
{
    [Test]
    public async Task Missing_connection_string_is_reported_and_app_still_starts()
    {
        await using var factory = TestApp.WithAllModules();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/database");
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(DatabaseStatusEndpoint.NotConfigured));
        });
    }
}
