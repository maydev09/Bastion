using Tomlyn.Extensions.Configuration;

namespace Bastion.Host;

internal static class ConfigurationSources
{
    public const string SecretsDirectory = "/run/secrets";

    // Replaces the ASP.NET Core defaults with our own list, in the same order. Later sources win.
    public static void UseConfigurationSources(this ConfigurationManager configuration, IHostEnvironment environment, string[] args)
    {
        configuration.Sources.Clear();
        configuration
            .AddTomlFile("bastion.toml", optional: false, reloadOnChange: false)
            .AddTomlFile($"bastion.{environment.EnvironmentName}.toml", optional: true, reloadOnChange: false)
            .AddTomlFile("bastion.local.toml", optional: true, reloadOnChange: false)
            .AddKeyPerFile(SecretsDirectory, optional: true);

        if (environment.IsDevelopment())
        {
            configuration.AddUserSecrets<Program>(optional: true);
        }

        configuration
            .AddEnvironmentVariables("ASPNETCORE_")
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    }
}
