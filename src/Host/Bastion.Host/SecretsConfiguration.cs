using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Bastion.Host;

internal static class SecretsConfiguration
{
    public const string Directory = "/run/secrets";

    public static void UseSecretFiles(this ConfigurationManager configuration)
    {
        configuration.AddKeyPerFile(Directory, optional: true);

        var sources = configuration.Sources;
        var environment = sources.OfType<EnvironmentVariablesConfigurationSource>().FirstOrDefault(s => string.IsNullOrEmpty(s.Prefix));
        if (environment is null)
        {
            return;
        }

        var secrets = sources[^1];
        sources.RemoveAt(sources.Count - 1);
        sources.Insert(sources.IndexOf(environment), secrets);
    }
}
