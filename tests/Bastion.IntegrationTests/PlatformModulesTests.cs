using System.Net.Http.Json;
using Bastion.Host;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bastion.IntegrationTests;

public class PlatformModulesTests
{
    [Test]
    public async Task Lists_enabled_modules_in_host_order()
    {
        await using var factory = TestApp.WithAllModules();

        var modules = await GetModules(factory);

        Assert.That(modules, Is.EqualTo(new[]
        {
            new ModuleInfo("FlatOffers", "flat-offers"),
            new ModuleInfo("JobOffers", "job-offers"),
        }));
    }

    [Test]
    public async Task Disabled_module_is_not_listed_and_app_still_starts()
    {
        await using var factory = TestApp.WithAllModules(b => b.UseSetting("Modules:JobOffers:Enabled", "false"));

        var modules = await GetModules(factory);

        Assert.That(modules.Select(m => m.Name), Is.EqualTo(new[] { "FlatOffers" }));
    }

    [Test]
    public async Task App_starts_with_every_module_disabled()
    {
        await using var factory = TestApp.WithAllModules(b =>
        {
            b.UseSetting("Modules:FlatOffers:Enabled", "false");
            b.UseSetting("Modules:JobOffers:Enabled", "false");
        });

        var modules = await GetModules(factory);

        Assert.That(modules, Is.Empty);
    }

    [Test]
    public void Unknown_module_in_configuration_stops_startup()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("Modules:FlatOfers:Enabled", "true"));

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.That(error.Message, Does.Contain("FlatOfers"));
    }

    private static async Task<ModuleInfo[]> GetModules(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        return (await client.GetFromJsonAsync<ModuleInfo[]>("/api/platform/modules"))!;
    }
}
