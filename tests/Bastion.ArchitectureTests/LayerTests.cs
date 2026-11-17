using System.Reflection;
using static Bastion.ArchitectureTests.Modules;

namespace Bastion.ArchitectureTests;

public class LayerTests
{
    private static readonly Dictionary<string, string[]> AllowedLayers = new()
    {
        [Root] = ["Domain", "Application", "Infrastructure", "Api"],
        ["Domain"] = [],
        ["Application"] = ["Domain"],
        ["Infrastructure"] = ["Domain", "Application"],
        ["Api"] = ["Domain", "Application"],
        ["Contracts"] = [],
    };

    private static readonly string[] Frameworks = ["Microsoft.AspNetCore", "Npgsql", "Dapper", "Bastion.BuildingBlocks"];

    private static readonly string[] InfrastructureLibraries = ["Npgsql", "Dapper", "Bastion.BuildingBlocks"];

    private static Assembly[] ApiAssemblies => Assemblies.Where(a => LayerOf(NameOf(a)) == "Api").ToArray();

    private static Assembly[] CoreAssemblies => Assemblies.Where(a => LayerOf(NameOf(a)) is "Domain" or "Application").ToArray();

    [TestCaseSource(typeof(Modules), nameof(Assemblies))]
    public void Project_name_uses_a_known_layer(Assembly assembly)
    {
        Assert.That(AllowedLayers.Keys, Does.Contain(LayerOf(NameOf(assembly))));
    }

    [TestCaseSource(typeof(Modules), nameof(Assemblies))]
    public void Layer_uses_only_allowed_layers_of_its_module(Assembly assembly)
    {
        var name = NameOf(assembly);
        var allowed = AllowedLayers.GetValueOrDefault(LayerOf(name), []);

        var forbidden = ReferencedNames(assembly)
            .Where(r => IsModule(r) && ModuleOf(r) == ModuleOf(name) && !allowed.Contains(LayerOf(r)));

        Assert.That(forbidden, Is.Empty);
    }

    [TestCaseSource(nameof(CoreAssemblies))]
    public void Domain_and_application_do_not_use_frameworks(Assembly assembly)
    {
        var forbidden = ReferencedNames(assembly)
            .Where(r => Frameworks.Any(f => r == f || r.StartsWith(f + ".", StringComparison.Ordinal)));

        Assert.That(forbidden, Is.Empty);
    }

    [TestCaseSource(nameof(ApiAssemblies))]
    public void Api_does_not_use_infrastructure_libraries(Assembly assembly)
    {
        var forbidden = ReferencedNames(assembly)
            .Where(r => InfrastructureLibraries.Any(l => r == l || r.StartsWith(l + ".", StringComparison.Ordinal)));

        Assert.That(forbidden, Is.Empty);
    }
}
