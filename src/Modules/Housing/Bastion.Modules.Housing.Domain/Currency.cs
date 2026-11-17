using System.Text.Json.Serialization;

namespace Bastion.Modules.Housing.Domain;

// Stored as integer in housing.offer.price_currency; never change the values.
public enum Currency
{
    [JsonStringEnumMemberName("PLN")]
    Pln = 1,
}
