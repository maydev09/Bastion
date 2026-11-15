using Microsoft.Extensions.Configuration.Json;
using Tomlyn.Extensions.Configuration;

namespace Bastion.Host;

internal static class TomlConfiguration
{
    public static void UseTomlConfiguration(this ConfigurationManager configuration, IHostEnvironment environment)
    {
        var sources = configuration.Sources;
        var jsonSources = sources.OfType<JsonConfigurationSource>().ToList();
        var index = jsonSources.Count > 0 ? sources.IndexOf(jsonSources[0]) : 0;
        foreach (var source in jsonSources)
        {
            sources.Remove(source);
        }

        string[] files = ["bastion.toml", $"bastion.{environment.EnvironmentName}.toml", "bastion.local.toml"];
        foreach (var (file, i) in files.Select((f, i) => (f, i)))
        {
            sources.Insert(index + i, new TomlConfigurationSource
            {
                Path = file,
                Optional = i > 0,
                ReloadOnChange = false,
                FileProvider = environment.ContentRootFileProvider,
            });
        }
    }
}
