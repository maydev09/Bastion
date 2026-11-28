namespace Bastion.Modules.Housing.Domain;

// Table housing.price_fence: the usual prices per m² of one group of listed offers (Basis, by Rooms: 1, 2, 3, 4 for four and
// more, null for not known), from LowPerM2 to HighPerM2 in Currency, made of SampleSize offers. The keys that do not belong
// to the basis are null; PlaceSize is the size of the places under the configured thresholds. Id is 0 until it is stored.
public sealed record PriceFence(
    int Id,
    PriceFenceBasis Basis,
    int? Rooms,
    int? PlaceId,
    Voivodeship? Voivodeship,
    PlaceSize? PlaceSize,
    decimal LowPerM2,
    decimal HighPerM2,
    Currency Currency,
    int SampleSize,
    DateTimeOffset CalculatedAt);
