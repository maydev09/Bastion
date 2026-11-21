using Bastion.BuildingBlocks.Jobs;
using Bastion.Host;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bastion.IntegrationTests;

internal static class TestApp
{
    public const string ConnectionStringKey = $"ConnectionStrings:{DatabaseStatusEndpoint.ConnectionStringName}";
    public const string ScheduledKey = $"{JobsOptions.Section}:{nameof(JobsOptions.Scheduled)}";

    public static WebApplicationFactory<Program> WithAllModules(Action<IWebHostBuilder>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach (var module in new[] { "Housing", "JobOffers" })
            {
                builder.UseSetting(ModuleOptions.EnabledKey(module), "true");
            }

            // Tests run as Development, which turns the schedules off; they test the production ones.
            builder.UseSetting(ScheduledKey, "true");

            // Nothing listens here; database tests set the real connection string.
            builder.UseSetting(ConnectionStringKey, "Host=localhost;Port=1;Database=none");

            configure?.Invoke(builder);
        });
}
