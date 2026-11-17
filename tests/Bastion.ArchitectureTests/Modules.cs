using System.Reflection;

namespace Bastion.ArchitectureTests;

// A module is a set of projects named Bastion.Modules.<Module>[.<Layer>], e.g. Bastion.Modules.Housing.Application.
// The project without a layer is the module's root: it holds only the IModule entry point.
internal static class Modules
{
    public const string Prefix = "Bastion.Modules.";

    public const string Root = "";

    public static readonly Assembly[] Assemblies = Directory
        .GetFiles(AppContext.BaseDirectory, Prefix + "*.dll")
        .Select(Assembly.LoadFrom)
        .ToArray();

    public static bool IsModule(string assemblyName) => assemblyName.StartsWith(Prefix, StringComparison.Ordinal);

    public static string ModuleOf(string assemblyName) => assemblyName[Prefix.Length..].Split('.')[0];

    public static string LayerOf(string assemblyName)
    {
        var parts = assemblyName[Prefix.Length..].Split('.');
        return parts.Length > 1 ? parts[1] : Root;
    }

    public static string NameOf(Assembly assembly) => assembly.GetName().Name!;

    // Only assemblies whose types are really used end up as references in the compiled assembly.
    public static string[] ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(r => r.Name!).ToArray();
}
