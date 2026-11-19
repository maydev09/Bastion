using Bastion.BuildingBlocks.Jobs;
using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.Housing.Api;
using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.ImportOffers;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Application.ReparseOffers;
using Bastion.Modules.Housing.Infrastructure.Files;
using Bastion.Modules.Housing.Infrastructure.Jobs;
using Bastion.Modules.Housing.Infrastructure.OtoDom;
using Bastion.Modules.Housing.Infrastructure.Repositories;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bastion.Modules.Housing;

public sealed class HousingModule : IModule
{
    private const string Schema = "housing";

    public string Name => "Housing";

    public string RoutePrefix => "housing";

    public void AddServices(IServiceCollection services, IConfiguration moduleSection)
    {
        services.AddScraping();
        services.AddOptions<OtoDomOptions>()
            .Bind(moduleSection.GetSection(OtoDomOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<ImportOptions>()
            .Bind(moduleSection.GetSection(ImportOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<RawCaptureOptions>()
            .Bind(moduleSection.GetSection(RawCaptureOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IOfferSource, OtoDomSource>();
        services.AddSingleton<OfferSources>();

        services.AddSingleton<IOfferRepository, OfferRepository>();
        services.AddSingleton<IRawCaptureRepository, RawCaptureRepository>();
        services.AddSingleton<IRawCaptureFileStorage, RawCaptureFileStorage>();
        services.AddSingleton<RawCaptureService>();
        services.AddTransient<ImportOffersHandler>();
        services.AddTransient<ReparseOffersHandler>();

        services.AddJobs(Schema, moduleSection)
            .Add<ImportOffersJob>(ImportOffersJob.Name)
            .Add<ScanOffersJob>(ScanOffersJob.Name)
            .Add<RawCaptureRetentionJob>(RawCaptureRetentionJob.Name);
    }

    public void MapEndpoints(IEndpointRouteBuilder moduleGroup)
    {
        OfferEndpoints.Map(moduleGroup);
        moduleGroup.MapJobs(Schema);
    }
}
