using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.ImportOffers;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

public sealed class ScanOffersJob(OfferSources sources, ImportOffersHandler handler)
    : ImportJob(sources, handler, ImportMode.FullScan)
{
    public const string Name = "ScanOffers";
}
