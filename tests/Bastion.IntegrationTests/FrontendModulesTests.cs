using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Bastion.Host;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bastion.IntegrationTests;

public partial class FrontendModulesTests
{
    [Test]
    public async Task Frontend_defines_the_same_modules_as_the_backend()
    {
        await using var factory = TestApp.WithAllModules();
        using var client = factory.CreateClient();
        var backend = await client.GetFromJsonAsync<ModuleInfo[]>("/api/platform/modules");

        var frontend = Directory.GetDirectories(Path.Combine(RepositoryRoot(), "src", "Web", "Modules"))
            .Select(dir => File.ReadAllText(Path.Combine(dir, "index.tsx")))
            .Select(source => new ModuleInfo(
                NameRegex().Match(source).Groups[1].Value,
                RoutePrefixRegex().Match(source).Groups[1].Value));

        Assert.That(frontend, Is.EquivalentTo(backend!));
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "Bastion.slnx")))
        {
            dir = dir.Parent ?? throw new InvalidOperationException("Bastion.slnx not found.");
        }

        return dir.FullName;
    }

    [GeneratedRegex("""name: "([^"]+)"\s*,""")]
    private static partial Regex NameRegex();

    [GeneratedRegex("""routePrefix: "([^"]+)"\s*,""")]
    private static partial Regex RoutePrefixRegex();
}
