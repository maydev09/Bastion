using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Application.ImportOffers;

public sealed class ImportOptions
{
    public const string Section = "Import";

    [Range(1, 100)]
    public int MaxPages { get; set; } = 5;
}
