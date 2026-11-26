using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListOffers;

// How many offers each filter choice would show with the other filters as they are.
// A choice of a group that joins its choices with OR (location, place size and role, amenities matched by any) is counted
// without the group's own choices; amenities matched by all are counted on top of the picked ones.
// A value missing from a map matches no offer.
public sealed record OfferFacets
{
    // Place and voivodeship choices form one group.
    public required IReadOnlyDictionary<Voivodeship, int> Voivodeships { get; init; }

    public required int WithoutPlace { get; init; }

    // The districts and neighborhoods of the picked places, by id; a district counts the offers of its neighborhoods too.
    public required IReadOnlyDictionary<int, int> Districts { get; init; }

    public required IReadOnlyDictionary<PlaceSize, int> PlaceSizes { get; init; }

    public required IReadOnlyDictionary<PlaceRole, int> PlaceRoles { get; init; }

    public required IReadOnlyDictionary<Answer, int> Furnished { get; init; }

    public required IReadOnlyDictionary<Feature, int> Features { get; init; }

    public required int Changed { get; init; }

    public required int Unchanged { get; init; }

    public required int PriceCut { get; init; }

    // For each asked moment, in the order asked, how many offers Bastion first saw since then (e.g. the last visit).
    public required IReadOnlyList<int> AddedSince { get; init; }

    // The prices (PLN), prices per m² (PLN) and areas under the other filters, so the chosen range can be seen
    // against all of them; null when no offer has the value.
    public Histogram? Price { get; init; }

    public Histogram? PricePerM2 { get; init; }

    public Histogram? AreaM2 { get; init; }

    // The total monthly costs (PLN) under the other filters.
    public Histogram? TotalCost { get; init; }
}
