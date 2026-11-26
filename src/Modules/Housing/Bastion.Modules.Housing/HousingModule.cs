using System.Net;
using Bastion.BuildingBlocks.Jobs;
using Bastion.BuildingBlocks.Scraping;
using Bastion.Modules.Housing.Api;
using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.Abstractions;
using Bastion.Modules.Housing.Application.FetchDetails;
using Bastion.Modules.Housing.Application.ImportOffers;
using Bastion.Modules.Housing.Application.ImportPlaces;
using Bastion.Modules.Housing.Application.LinkPlaces;
using Bastion.Modules.Housing.Application.ListOffers;
using Bastion.Modules.Housing.Application.ListPlaces;
using Bastion.Modules.Housing.Application.MonthlyCosts;
using Bastion.Modules.Housing.Application.Places;
using Bastion.Modules.Housing.Application.RawCaptures;
using Bastion.Modules.Housing.Application.ReparseOffers;
using Bastion.Modules.Housing.Infrastructure.Files;
using Bastion.Modules.Housing.Infrastructure.Jobs;
using Bastion.Modules.Housing.Infrastructure.OtoDom;
using Bastion.Modules.Housing.Infrastructure.Repositories;
using Bastion.Modules.Housing.Infrastructure.Wikidata;
using Bastion.Platform.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddOptions<DetailsOptions>()
            .Bind(moduleSection.GetSection(DetailsOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<RawCaptureOptions>()
            .Bind(moduleSection.GetSection(RawCaptureOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<WikidataOptions>()
            .Bind(moduleSection.GetSection(WikidataOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<PlaceOptions>()
            .Bind(moduleSection.GetSection(PlaceOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<PlaceSizeOptions>()
            .Bind(moduleSection.GetSection(PlaceSizeOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<MonthlyCostOptions>()
            .Bind(moduleSection.GetSection(MonthlyCostOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IOfferSource, OtoDomSource>();
        services.AddSingleton<OfferSources>();

        services.AddHttpClient<WikidataClient>((provider, http) =>
        {
            var wikidata = provider.GetRequiredService<IOptions<WikidataOptions>>().Value;
            http.BaseAddress = wikidata.QueryUrl;
            http.Timeout = TimeSpan.FromSeconds(wikidata.TimeoutSeconds);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(wikidata.UserAgent);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });
        services.AddTransient<IPlaceSource, WikidataPlaceSource>();

        services.AddSingleton<IOfferRepository, OfferRepository>();
        services.AddSingleton<IPlaceRepository, PlaceRepository>();
        services.AddSingleton<IRawCaptureRepository, RawCaptureRepository>();
        services.AddSingleton<IMonthlyCostRepository, MonthlyCostRepository>();
        services.AddSingleton<IRawCaptureFileStorage, RawCaptureFileStorage>();
        services.AddSingleton<RawCaptureService>();
        services.AddTransient<MonthlyCostService>();
        services.AddTransient<ImportOffersHandler>();
        services.AddTransient<FetchDetailsHandler>();
        services.AddTransient<ReparseOffersHandler>();
        services.AddTransient<ImportPlacesHandler>();
        services.AddTransient<LinkPlacesHandler>();
        services.AddTransient<ListOffersHandler>();
        services.AddTransient<ListPlacesHandler>();

        services.AddJobs(Schema, moduleSection)
            .Add<ImportOffersJob>(ImportOffersJob.Name)
            .Add<ScanOffersJob>(ScanOffersJob.Name)
            .Add<FetchDetailsJob>(FetchDetailsJob.Name)
            .Add<RawCaptureRetentionJob>(RawCaptureRetentionJob.Name)
            .Add<ImportPlacesJob>(ImportPlacesJob.Name)
            .Add<LinkPlacesJob>(LinkPlacesJob.Name)
            .Add<CalculateMonthlyCostsJob>(CalculateMonthlyCostsJob.Name);
    }

    public void MapEndpoints(IEndpointRouteBuilder moduleGroup)
    {
        OfferEndpoints.Map(moduleGroup);
        moduleGroup.MapJobs(Schema);
    }
}
