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
        services.AddOptions<ScrapingOptions>()
            .BindConfiguration(ScrapingOptions.Section)
            .ValidateDataAnnotations()
            .Validate(o => o.MaxDelaySeconds >= o.MinDelaySeconds, "Scraping:MaxDelaySeconds must not be less than MinDelaySeconds.")
            .ValidateOnStart();

        services.AddHttpClient(ScrapingClient.HttpClientName, (sp, http) =>
                http.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<ScrapingOptions>>().Value.TimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IVpnCheck, RouteVpnCheck>();
        services.TryAddSingleton<IUserAgentSource, PlaywrightUserAgentSource>();

        services.TryAddSingleton(sp => new ScrapingClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IVpnCheck>(),
            sp.GetRequiredService<IUserAgentSource>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IOptions<ScrapingOptions>>(),
            sp.GetRequiredService<ILogger<ScrapingClient>>()));
        return services;
    }
}
