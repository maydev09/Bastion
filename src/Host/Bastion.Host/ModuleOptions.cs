namespace Bastion.Host;

internal sealed class ModuleOptions
{
    public const string Section = "Modules";

    public bool Enabled { get; set; }

    public static IConfigurationSection SectionOf(IConfiguration configuration, string moduleName) =>
        configuration.GetSection(Section).GetSection(moduleName);
}
