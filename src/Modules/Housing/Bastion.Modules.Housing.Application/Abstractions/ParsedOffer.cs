using Bastion.Modules.Housing.Domain;

namespace Bastion.Modules.Housing.Application.Abstractions;

// An offer read from a listing page, with what the page gives but the offer does not store: the portal's names of
// its district and of the neighborhood inside it. PlaceResolver turns them into Offer.DistrictId.
public sealed record ParsedOffer(Offer Offer, string? DistrictName = null, string? NeighborhoodName = null);
