using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bastion.Modules.Housing.Domain;

// Table housing.offer.
public sealed record Offer(
    Source SourceId,
    string ExternalId,
    string Title,
    Uri Url,
    PropertyType PropertyType,
    Transaction Transaction,
    Money? Price = null,
    decimal? AreaM2 = null,
    int? Rooms = null)
{
    // ADR 0016: an offer is updated only when the hash of the stored listing fields changes.
    public string ListingHash()
    {
        var fields = string.Create(CultureInfo.InvariantCulture,
            $"{Title}\n{Url}\n{PropertyType}\n{Transaction}\n{Price?.Amount}\n{Price?.Currency}\n{AreaM2}\n{Rooms}");
        var bytes = Encoding.UTF8.GetBytes(fields);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
