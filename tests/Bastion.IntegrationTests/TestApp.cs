using Bastion.Host;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bastion.IntegrationTests;

internal static class TestApp
{
    public static WebApplicationFactory<Program> WithAllModules(Action<IWebHostBuilder>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach (var module in new[] { "FlatOffers", "JobOffers" })
            {
                builder.UseSetting($"{ModuleOptions.Section}:{module}:Enabled", "true");
            }

            configure?.Invoke(builder);
        });
}
