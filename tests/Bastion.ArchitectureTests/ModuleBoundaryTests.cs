using System.Reflection;
using Bastion.Platform.Abstractions;
using static Bastion.ArchitectureTests.Modules;

namespace Bastion.ArchitectureTests;

public class ModuleBoundaryTests
{
    private static Assembly[] RootAssemblies => Assemblies.Where(a => LayerOf(NameOf(a)) == Root).ToArray();

    [Test]
    public void Module_projects_are_found()
    {
        Assert.That(RootAssemblies, Is.Not.Empty);
    }

    [TestCaseSource(typeof(Modules), nameof(Assemblies))]
    public void Module_uses_other_modules_only_through_contracts(Assembly assembly)
    {
        var module = ModuleOf(NameOf(assembly));

        var forbidden = ReferencedNames(assembly)
            .Where(r => IsModule(r) && ModuleOf(r) != module && LayerOf(r) != "Contracts");

        Assert.That(forbidden, Is.Empty);
    }

    [TestCaseSource(nameof(RootAssemblies))]
    public void Module_root_exposes_only_its_IModule_entry_point(Assembly assembly)
    {
        var publicTypes = assembly.GetExportedTypes();

        Assert.Multiple(() =>
        {
            Assert.That(publicTypes, Has.Length.EqualTo(1), "The module root holds only the entry point; put other code in a layer project.");
            Assert.That(publicTypes.Single().IsAssignableTo(typeof(IModule)), Is.True);
        });
    }

    [Test]
    public void Host_references_only_module_roots()
    {
        var host = Assembly.Load("Bastion.Host");

        var layers = ReferencedNames(host).Where(r => IsModule(r) && LayerOf(r) != Root);

        Assert.That(layers, Is.Empty);
    }

    [Test]
    public void Platform_does_not_reference_modules()
    {
        Assert.That(ReferencedNames(typeof(IModule).Assembly).Where(IsModule), Is.Empty);
    }

    [TestCase(typeof(Bastion.BuildingBlocks.Scraping.ScrapingClient))]
    [TestCase(typeof(Bastion.BuildingBlocks.Jobs.IJob))]
    public void Building_blocks_do_not_reference_modules(Type buildingBlockType)
    {
        Assert.That(ReferencedNames(buildingBlockType.Assembly).Where(IsModule), Is.Empty);
    }
}
