using Tomlyn.Extensions.Configuration;

namespace Bastion.Host;

internal static class ConfigurationSources
{
    public const string SecretsDirectory = "/run/secrets";

    // Replaces the ASP.NET Core defaults (appsettings*.json, user secrets) with our own list.
    // Later sources override earlier ones.
    public static void UseConfigurationSources(this ConfigurationManager configuration, IHostEnvironment environment, string[] args)
    {
        configuration.Sources.Clear();
        configuration
            .AddTomlFile("bastion.toml", optional: false, reloadOnChange: false)
            .AddTomlFile($"bastion.{environment.EnvironmentName}.toml", optional: true, reloadOnChange: false)
            .AddTomlFile("bastion.local.toml", optional: true, reloadOnChange: false)
            .AddKeyPerFile(SecretsDirectory, optional: true)
            .AddEnvironmentVariables("ASPNETCORE_")
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    }
}
