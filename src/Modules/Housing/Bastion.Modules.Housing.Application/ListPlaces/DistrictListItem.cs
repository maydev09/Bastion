namespace Bastion.Modules.Housing.Application.ListPlaces;

// A district of a place, or a neighborhood inside the district ParentId. OfferCount counts its listed offers;
// a district counts the offers of its neighborhoods too.
public sealed record DistrictListItem(int Id, string Name, int? ParentId, int OfferCount);
