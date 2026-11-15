using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Platform.Abstractions;

public interface IModule
{
    string Name { get; }

    string RoutePrefix { get; }

    IReadOnlyList<string> DependsOn => [];

    void AddServices(IServiceCollection services, IConfiguration moduleSection);

    void MapEndpoints(IEndpointRouteBuilder moduleGroup);
}
