using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bastion.Modules.FlatOffers.Domain;

internal enum OfferSource
{
    OtoDom,
}

internal sealed record ListingOffer(
    string ExternalId,
    string Title,
    Uri Url,
    decimal? Price = null,
    string? PriceCurrency = null,
    decimal? AreaM2 = null,
    int? Rooms = null)
{
    // ADR 0016: an offer is updated only when the hash of the stored listing fields changes.
    public string ListingHash()
    {
        var fields = string.Create(CultureInfo.InvariantCulture, $"{Title}\n{Url}\n{Price}\n{PriceCurrency}\n{AreaM2}\n{Rooms}");
        var bytes = Encoding.UTF8.GetBytes(fields);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
