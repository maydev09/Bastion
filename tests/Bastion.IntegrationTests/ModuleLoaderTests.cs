using Bastion.Host;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.IntegrationTests;

public class ModuleLoaderTests
{
    [Test]
    public void Fails_when_an_enabled_module_depends_on_a_disabled_one()
    {
        IModule[] modules = [new FakeModule("A"), new FakeModule("B", dependsOn: "A")];
        var configuration = Config(("Modules:A:Enabled", "false"), ("Modules:B:Enabled", "true"));

        var error = Assert.Throws<InvalidOperationException>(() => ModuleLoader.GetEnabled(modules, configuration));

        Assert.That(error.Message, Does.Contain("'B' depends on module 'A'"));
    }

    [Test]
    public void Missing_enabled_setting_means_disabled()
    {
        IModule[] modules = [new FakeModule("A"), new FakeModule("B")];
        var configuration = Config(("Modules:B:Enabled", "true"));

        var enabled = ModuleLoader.GetEnabled(modules, configuration);

        Assert.That(enabled.Select(m => m.Name), Is.EqualTo(new[] { "B" }));
    }

    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private sealed class FakeModule(string name, params string[] dependsOn) : IModule
    {
        public string Name => name;

        public string RoutePrefix => name.ToLowerInvariant();

        public IReadOnlyList<string> DependsOn => dependsOn;

        public void AddServices(IServiceCollection services, IConfiguration moduleSection)
        {
        }

        public void MapEndpoints(IEndpointRouteBuilder moduleGroup)
        {
        }
    }
}
