using System.Net;
using System.Text.Json;
using Bastion.Host;

namespace Bastion.IntegrationTests;

public class DatabaseStatusTests
{
    [Test]
    public void Missing_connection_string_stops_startup()
    {
        using var factory = TestApp.WithAllModules(b => b.UseSetting(TestApp.ConnectionStringKey, ""));

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.That(error.Message, Does.Contain("scripts/dev-up.sh"));
    }

    [Test]
    public async Task Unreachable_database_is_reported()
    {
        await using var factory = TestApp.WithAllModules();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/database");
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(problem.GetProperty("code").GetString(), Is.EqualTo(DatabaseStatusEndpoint.Unavailable));
        });
    }
}
