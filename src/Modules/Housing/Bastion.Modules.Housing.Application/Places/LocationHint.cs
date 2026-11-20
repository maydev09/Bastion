using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Places;

// A portal's location of an offer in a portal-neutral form; names as the portal writes them.
public sealed record LocationHint(
    string Voivodeship,
    string? County,
    string? Municipality,
    string Place,
    PlaceKind? Kind = null);
