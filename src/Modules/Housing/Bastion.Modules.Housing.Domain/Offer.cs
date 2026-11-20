using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bastion.Modules.Housing.Domain;

// Table housing.offer: the fields a listing page shows. A detail page adds OfferDetails.
// Rent, Floor and Features come from both pages; a null never clears a stored value.
public sealed record Offer(
    Source SourceId,
    string ExternalId,
    string Title,
    Uri Url,
    PropertyType PropertyType,
    Transaction Transaction,
    DateTimeOffset ExternalCreatedAt,
    Money? Price = null,
    decimal? AreaM2 = null,
    int? Rooms = null,
    DateTimeOffset? ExternalUpdatedAt = null,
    string? LocationKey = null,
    int? PlaceId = null,
    Money? Rent = null,
    int? Floor = null,
    IReadOnlyList<Feature>? Features = null)
{
    // An offer is updated only when the hash of the stored listing fields changes.
    // The location (LocationKey, PlaceId) is not hashed: it is kept current without counting the offer as changed.
    public string ListingHash() => Hash($"{Content()}\n{ExternalCreatedAt:O}\n{ExternalUpdatedAt:O}");

    // The listing fields without the portal's dates: a bump (ExternalUpdatedAt) moves the offer up
    // without changing it, so it does not make the detail page due again.
    public string ContentHash() => Hash(Content());

    private string Content()
    {
        var features = Features is null ? "" : string.Join(',', Features.Order());
        return string.Create(CultureInfo.InvariantCulture,
            $"{Title}\n{Url}\n{PropertyType}\n{Transaction}\n{Price?.Amount}\n{Price?.Currency}\n{AreaM2}\n{Rooms}\n{Rent?.Amount}\n{Rent?.Currency}\n{Floor}\n{features}");
    }

    private static string Hash(string fields) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fields)));
}
