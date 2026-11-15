using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Bastion.ArchitectureTests;

public class LayerTests
{
    private static readonly System.Reflection.Assembly[] ModuleAssemblies = Directory
        .GetFiles(AppContext.BaseDirectory, "Bastion.Modules.*.dll")
        .Select(System.Reflection.Assembly.LoadFrom)
        .Where(a => !a.GetName().Name!.EndsWith(".Contracts", StringComparison.Ordinal))
        .ToArray();

    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader().LoadAssemblies(ModuleAssemblies).Build();

    private static string[] ModuleNames => ModuleAssemblies.Select(a => a.GetName().Name!).ToArray();

    [TestCaseSource(nameof(ModuleNames))]
    public void Domain_depends_on_no_other_layer(string module)
    {
        Types().That().ResideInNamespaceMatching(Layer(module, "Domain"))
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(Layer(module, "(Application|Infrastructure|Api)")))
            .AndShould().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^(Microsoft\.AspNetCore|Bastion\.BuildingBlocks)(\..*)?$"))
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [TestCaseSource(nameof(ModuleNames))]
    public void Application_does_not_know_infrastructure_api_or_frameworks(string module)
    {
        Types().That().ResideInNamespaceMatching(Layer(module, "Application"))
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(Layer(module, "(Infrastructure|Api)")))
            .AndShould().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^(Microsoft\.AspNetCore|Npgsql|Dapper|Bastion\.BuildingBlocks)(\..*)?$"))
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [TestCaseSource(nameof(ModuleNames))]
    public void Api_does_not_use_infrastructure(string module)
    {
        Types().That().ResideInNamespaceMatching(Layer(module, "Api"))
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(Layer(module, "Infrastructure")))
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    private static string Layer(string module, string layer) => $@"^{module.Replace(".", @"\.", StringComparison.Ordinal)}\.{layer}(\..*)?$";
}
