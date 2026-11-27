using Bastion.Modules.Housing.Application;
using Bastion.Modules.Housing.Application.DerivedData;
using Bastion.Modules.Housing.Application.FetchDetails;
using Bastion.Modules.Housing.Application.ImportOffers;

namespace Bastion.Modules.Housing.Infrastructure.Jobs;

// New offers, quickly and often: the listing from the newest offer while its pages show new ones.
public sealed class ImportNewOffersJob(
    OfferSources sources, ImportOffersHandler listing, FetchDetailsHandler details, DerivedDataService derivedData)
    : ImportJob(sources, listing, details, derivedData, ImportMode.NewOffers)
{
    public const string Name = "ImportNewOffers";
}
