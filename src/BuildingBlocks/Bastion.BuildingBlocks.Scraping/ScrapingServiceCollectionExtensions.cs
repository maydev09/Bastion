using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bastion.BuildingBlocks.Scraping;

public static class ScrapingServiceCollectionExtensions
{
    public static IServiceCollection AddScraping(this IServiceCollection services)
    {
        services.AddOptions<ScrapingOptions>().BindConfiguration(ScrapingOptions.Section);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IVpnCheck, RouteVpnCheck>();
        services.TryAddSingleton<IUserAgentSource, PlaywrightUserAgentSource>();
        services.TryAddSingleton(sp => new ScrapingClient(
            new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                AutomaticDecompression = DecompressionMethods.All,
            },
            sp.GetRequiredService<IVpnCheck>(),
            sp.GetRequiredService<IUserAgentSource>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IOptions<ScrapingOptions>>(),
            sp.GetRequiredService<ILogger<ScrapingClient>>()));
        return services;
    }
}
