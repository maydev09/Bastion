using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.ImportOffers;

// The job's own settings in [Modules.Housing.Jobs.ImportNewOffers], next to its schedule and time limit.
public sealed class ImportNewOffersOptions
{
    public const string Section = "Jobs:ImportNewOffers";

    // The import reads pages while they show new offers, at most this many; more new offers are left to SyncAllOffers.
    [Range(1, 100)]
    public int MaxPages { get; set; } = 5;
}
