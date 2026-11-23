using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.ListPlaces;

// A place to filter offers by; OfferCount counts its listed offers.
public sealed record PlaceListItem(
    int Id, string Name, Voivodeship Voivodeship, string? County, string? Municipality, PlaceKind? Kind, int OfferCount);
