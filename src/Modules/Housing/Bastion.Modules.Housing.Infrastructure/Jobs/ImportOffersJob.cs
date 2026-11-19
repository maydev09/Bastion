using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.ImportOffers;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

public sealed class ImportOffersJob(OfferSources sources, ImportOffersHandler handler)
    : ImportJob(sources, handler, ImportMode.NewOffers)
{
    public const string Name = "ImportOffers";
}
