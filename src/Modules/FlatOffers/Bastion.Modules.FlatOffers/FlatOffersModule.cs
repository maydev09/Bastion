using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.FlatOffers.Api;
using Bastion.Modules.FlatOffers.Application.Abstractions;
using Bastion.Modules.FlatOffers.Application.ImportOffers;
using Bastion.Modules.FlatOffers.Infrastructure.OtoDom;
using Bastion.Modules.FlatOffers.Infrastructure.Storage;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Modules.FlatOffers;

public sealed class FlatOffersModule : IModule
{
    public string Name => "FlatOffers";

    public string RoutePrefix => "flat-offers";

    public void AddServices(IServiceCollection services, IConfiguration moduleSection)
    {
        services.AddScraping();
        services.Configure<OtoDomOptions>(moduleSection.GetSection(OtoDomOptions.Section));
        services.AddSingleton<IListingSource, OtoDomListingSource>();
        services.AddSingleton<IOfferStore, OfferStore>();
        services.AddTransient<ImportOffersHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder moduleGroup)
    {
        OfferEndpoints.Map(moduleGroup);
    }
}
