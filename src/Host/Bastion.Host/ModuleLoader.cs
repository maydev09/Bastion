using Bastion.Platform.Abstractions;

namespace Bastion.Host;

internal static class ModuleLoader
{
    public static IReadOnlyList<IModule> GetEnabled(IReadOnlyList<IModule> allModules, IConfiguration configuration)
    {
        var knownNames = allModules.Select(m => m.Name).ToHashSet();
        var unknown = configuration.GetSection(ModuleOptions.Section).GetChildren()
            .Select(s => s.Key)
            .Where(name => !knownNames.Contains(name))
            .ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unknown module(s) in configuration: {string.Join(", ", unknown)}. Known modules: {string.Join(", ", knownNames)}.");
        }

        var enabled = allModules.Where(m => IsEnabled(configuration, m)).ToList();
        var enabledNames = enabled.Select(m => m.Name).ToHashSet();
        foreach (var module in enabled)
        {
            foreach (var dependency in module.DependsOn.Where(d => !enabledNames.Contains(d)))
            {
                throw new InvalidOperationException(
                    $"Module '{module.Name}' depends on module '{dependency}', which is disabled or unknown. " +
                    $"Enable '{dependency}' in the [{ModuleOptions.Section}.{dependency}] section or disable '{module.Name}'.");
            }
        }

        return enabled;
    }

    public static void MapModuleEndpoints(this IEndpointRouteBuilder app, IReadOnlyList<IModule> modules)
    {
        foreach (var module in modules)
        {
            var group = app.MapGroup($"/api/{module.RoutePrefix}")
                .WithTags(module.Name)
                .AddEndpointFilter(new ModuleLogScope(module.Name));
            module.MapEndpoints(group);
        }
    }

    private static bool IsEnabled(IConfiguration configuration, IModule module) =>
        ModuleOptions.SectionOf(configuration, module.Name).Get<ModuleOptions>()?.Enabled == true;
}
