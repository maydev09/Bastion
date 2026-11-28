using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.ImportOffers;

// The job's own settings in [Modules.Housing.Jobs.SyncAllOffers], next to its schedule and time limit.
public sealed class SyncAllOffersOptions
{
    public const string Section = "Jobs:SyncAllOffers";

    // Only a safety limit: the job reads to the last page; one stopped by it marks no offer removed.
    [Range(1, 10_000)]
    public int MaxPages { get; set; } = 1_000;
}
