using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.ImportOffers;

public sealed class ImportOptions
{
    public const string Section = "Import";

    [Range(1, 100)]
    public int MaxPages { get; set; } = 5;

    // Only a safety limit: a full scan reads to the last page.
    [Range(1, 10_000)]
    public int FullScanMaxPages { get; set; } = 1_000;
}
