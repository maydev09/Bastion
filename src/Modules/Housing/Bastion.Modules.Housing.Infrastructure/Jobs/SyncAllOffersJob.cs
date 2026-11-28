using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.DerivedData;
using Bastion.Modules.Housing.Application.FetchDetails;
using Bastion.Modules.Housing.Application.ImportOffers;
using Microsoft.Extensions.Logging;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// The database brought in line with the whole portal: every listing page, offers missing from it marked removed, then the
// detail pages of changed offers.
public sealed class SyncAllOffersJob(
    OfferSources sources,
    ImportOffersHandler listing,
    FetchDetailsHandler details,
    DerivedDataService derivedData,
    ILogger<ImportJob> logger)
    : ImportJob(sources, listing, details, derivedData, logger, ImportMode.FullScan)
{
    public const string Name = "SyncAllOffers";
}
