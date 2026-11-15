using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Modules.JobOffers;

public sealed class JobOffersModule : IModule
{
    public string Name => "JobOffers";

    public string RoutePrefix => "job-offers";

    public void AddServices(IServiceCollection services, IConfiguration moduleSection)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder moduleGroup)
    {
    }
}
