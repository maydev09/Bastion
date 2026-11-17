using System.ComponentModel.DataAnnotations;

namespace Bastion.Modules.Housing.Infrastructure.OtoDom;

public sealed class OtoDomOptions
{
    public const string Section = "OtoDom";

    [Required]
    public Uri BaseUrl { get; set; } = new("https://www.otodom.pl");

    [Required]
    public Uri ListingUrl { get; set; } = null!;
}
