namespace Bastion.Modules.FlatOffers.Infrastructure.OtoDom;

internal sealed class OtoDomOptions
{
    public Uri BaseUrl { get; set; } = new("https://www.otodom.pl");

    public Uri? SmokeTestUrl { get; set; }
}
