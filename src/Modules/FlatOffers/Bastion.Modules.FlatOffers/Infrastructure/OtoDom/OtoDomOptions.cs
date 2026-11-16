namespace Bastion.Modules.FlatOffers.Infrastructure.OtoDom;

internal sealed class OtoDomOptions
{
    public const string Section = "OtoDom";

    public Uri BaseUrl { get; set; } = new("https://www.otodom.pl");

    public Uri? ListingUrl { get; set; }
}
