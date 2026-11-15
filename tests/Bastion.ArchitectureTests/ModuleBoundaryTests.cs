using System.Reflection;
using Bastion.Platform.Abstractions;

namespace Bastion.ArchitectureTests;

public class ModuleBoundaryTests
{
    private const string ModulePrefix = "Bastion.Modules.";

    private static readonly Assembly[] ModuleAssemblies = Directory
        .GetFiles(AppContext.BaseDirectory, ModulePrefix + "*.dll")
        .Select(path => Assembly.LoadFrom(path))
        .Where(a => !IsContracts(a.GetName()))
        .ToArray();

    [Test]
    public void Module_assemblies_are_found()
    {
        Assert.That(ModuleAssemblies, Is.Not.Empty);
    }

    [TestCaseSource(nameof(ModuleAssemblies))]
    public void Module_references_other_modules_only_through_contracts(Assembly module)
    {
        var forbidden = module.GetReferencedAssemblies()
            .Where(r => r.Name!.StartsWith(ModulePrefix, StringComparison.Ordinal) && !IsContracts(r))
            .Select(r => r.Name);

        Assert.That(forbidden, Is.Empty);
    }

    [TestCaseSource(nameof(ModuleAssemblies))]
    public void Module_exposes_only_its_IModule_entry_point(Assembly module)
    {
        var publicTypes = module.GetExportedTypes();

        Assert.Multiple(() =>
        {
            Assert.That(publicTypes, Has.Length.EqualTo(1), "Only the entry point may be public; make the rest internal.");
            Assert.That(publicTypes.Single().IsAssignableTo(typeof(IModule)), Is.True);
        });
    }

    [Test]
    public void Platform_does_not_reference_modules()
    {
        var references = typeof(IModule).Assembly.GetReferencedAssemblies()
            .Where(r => r.Name!.StartsWith(ModulePrefix, StringComparison.Ordinal));

        Assert.That(references, Is.Empty);
    }

    [Test]
    public void Building_blocks_do_not_reference_modules()
    {
        var references = typeof(Bastion.BuildingBlocks.Scraping.ScrapingClient).Assembly.GetReferencedAssemblies()
            .Where(r => r.Name!.StartsWith(ModulePrefix, StringComparison.Ordinal));

        Assert.That(references, Is.Empty);
    }

    private static bool IsContracts(AssemblyName name) =>
        name.Name!.EndsWith(".Contracts", StringComparison.Ordinal);
}
